using System.Collections.Concurrent;
using System.Text.Json;
using Acornima.Ast;
using Jint;
using Jint.Native;
using Jint.Runtime;
using SeedField.Simulation;

namespace SeedField.Runtime.Js;

/// <summary>
/// Runs one civilization's controller script in Jint 4.18.0.
///
/// Contract: the script defines <c>function decide(observation)</c> and returns an
/// array of intent objects. Observation is handed over as JSON text and intents come
/// back as JSON text, so the script never receives a CLR object reference.
/// </summary>
public sealed class JsController : IController
{
    public const int MaxObservationBytes = 4 * 1024 * 1024;

    private static readonly string Harness = ObservationExpander.Script + """

        function __seedfield_run(json) {
            var result = decide(__seedfield_expand(JSON.parse(json)));
            if (result === undefined || result === null) {
                throw new Error("decide() must return an intent array");
            }
            return JSON.stringify(result);
        }

        function __seedfield_run_obj(obs) {
            var result = decide(obs);
            if (result === undefined || result === null) {
                throw new Error("decide() must return an intent array");
            }
            return JSON.stringify(result);
        }
        """;

    private static readonly ConcurrentDictionary<(string Source, bool Strict), Prepared<Script>>
        PreparedScripts = new();

    private readonly Engine _engine;
    private JsValue _runFunction = JsValue.Undefined;
    private JsValue _runDirect = JsValue.Undefined;

    public JsController(string civilizationId, string script, JsControllerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(civilizationId);
        ArgumentNullException.ThrowIfNull(script);

        options ??= new JsControllerOptions();
        options.Validate();

        CivilizationId = civilizationId;

        _engine = new Engine(engineOptions =>
        {
            engineOptions.TimeoutInterval(options.Timeout);
            engineOptions.MaxStatements(options.MaxStatements);
            engineOptions.LimitMemory(options.MemoryLimitBytes);
            engineOptions.Strict = true;
            engineOptions.Host.StringCompilationAllowed = false;
        });

        try
        {
            _engine.Execute(GetPreparedScript(script + "\n" + Harness));
            _runFunction = _engine.Evaluate("__seedfield_run");
            _runDirect = _engine.Evaluate("__seedfield_run_obj");

            var hasDecide = _engine.Evaluate("typeof decide === 'function'").AsBoolean();
            if (!hasDecide)
            {
                throw new JsControllerLoadException(
                    "Controller script must define function decide(observation).");
            }
        }
        catch (JsControllerLoadException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new JsControllerLoadException("Controller script failed to load.", exception);
        }
    }

    public string CivilizationId { get; }

    public void Dispose() => _engine.Dispose();

    public ControllerDecision Decide(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (!string.Equals(observation.CivilizationId, CivilizationId, StringComparison.Ordinal))
        {
            return ControllerDecision.Fault(
                ScriptFaultCodes.Fault,
                "Observation belongs to a different civilization.");
        }

        var run = RunInvocation(
            () => _engine.Invoke(_runDirect, ObservationJsValue.Build(_engine, observation)));
        return run.Success
            ? ParseDecision(run.IntentsJson!, CivilizationId)
            : ControllerDecision.Fault(run.FaultCode!, run.FaultMessage!);
    }

    /// <summary>Compact controller payload; the trusted harness expands it.</summary>
    public static string SerializeObservation(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return ObservationJson.Serialize(observation);
    }

    /// <summary>Object-shaped JSON for tests and tooling.</summary>
    public static string SerializeFullObservation(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return ObservationJson.SerializeFull(observation);
    }

    public static ControllerDecision ParseDecisionJson(string json, string civilizationId) =>
        ParseDecision(json, civilizationId);

    /// <summary>
    /// Runs the script against already-serialized observation JSON. Used by the worker
    /// process, which must never receive a WorldState or CLR observation object.
    /// </summary>
    public ControllerRunResult RunJson(string observationJson)
    {
        if (string.IsNullOrWhiteSpace(observationJson))
        {
            return ControllerRunResult.Fault(
                ScriptFaultCodes.InvalidIntentSchema,
                "Observation JSON is empty.");
        }

        // UTF-8 never needs fewer bytes than UTF-16 code units, and never more than
        // four per unit. Only the ambiguous middle band pays for a full count.
        var length = observationJson.Length;
        if (length > MaxObservationBytes ||
            (length * 4L > MaxObservationBytes &&
             System.Text.Encoding.UTF8.GetByteCount(observationJson) > MaxObservationBytes))
        {
            return ControllerRunResult.Fault(
                ScriptFaultCodes.Fault,
                $"Observation exceeds {MaxObservationBytes} bytes.");
        }

        return RunInvocation(() => _engine.Invoke(_runFunction, observationJson));
    }

    private ControllerRunResult RunInvocation(Func<JsValue> invoke)
    {
        try
        {
            return ControllerRunResult.Ok(invoke().AsString());
        }
        catch (TimeoutException exception)
        {
            return ControllerRunResult.Fault(ScriptFaultCodes.Timeout, exception.Message);
        }
        catch (StatementsCountOverflowException exception)
        {
            return ControllerRunResult.Fault(ScriptFaultCodes.StatementLimit, exception.Message);
        }
        catch (MemoryLimitExceededException exception)
        {
            return ControllerRunResult.Fault(ScriptFaultCodes.MemoryLimit, exception.Message);
        }
        catch (JavaScriptException exception)
        {
            return ControllerRunResult.Fault(ScriptFaultCodes.Fault, exception.Message);
        }
        catch (Exception exception)
        {
            return ControllerRunResult.Fault(ScriptFaultCodes.Fault, exception.Message);
        }
    }

    /// <summary>
    /// Parses and analyzes a controller source once and shares the result across all
    /// engines. Prepared scripts are thread-safe; the bound cache keeps repeated
    /// hot deployments from growing without limit.
    /// </summary>
    private static Prepared<Script> GetPreparedScript(string source)
    {
        if (PreparedScripts.Count > 128)
        {
            PreparedScripts.Clear();
        }

        return PreparedScripts.GetOrAdd((source, true), key =>
            Engine.PrepareScript(
                key.Source,
                source: "seedfield-controller.js",
                strict: key.Strict));
    }

    private static ControllerDecision ParseDecision(string json, string civilizationId)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            return ControllerDecision.Fault(ScriptFaultCodes.InvalidIntentSchema, exception.Message);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array)
            {
                return ControllerDecision.Fault(
                    ScriptFaultCodes.InvalidIntentSchema,
                    "decide() must return an array of intents.");
            }

            var intents = new List<Intent>();
            var faults = new List<ControllerFault>();

            foreach (var element in root.EnumerateArray())
            {
                if (TryParseIntent(element, civilizationId, out var intent, out var error))
                {
                    intents.Add(intent!);
                }
                else
                {
                    faults.Add(new ControllerFault(ScriptFaultCodes.InvalidIntentSchema, error!));
                }
            }

            return new ControllerDecision(intents, faults);
        }
    }

    private static bool TryParseIntent(
        JsonElement element,
        string civilizationId,
        out Intent? intent,
        out string? error)
    {
        intent = null;
        error = null;

        if (element.ValueKind != JsonValueKind.Object)
        {
            error = "Each intent must be an object.";
            return false;
        }

        if (!TryGetString(element, "type", out var type) || string.IsNullOrWhiteSpace(type))
        {
            error = "Intent is missing string field 'type'.";
            return false;
        }

        if (!TryGetInt64(element, "entityId", out var entityIdValue) || entityIdValue <= 0)
        {
            error = "Intent is missing positive integer field 'entityId'.";
            return false;
        }

        var entityId = new EntityId(entityIdValue);

        switch (type)
        {
            case "move":
                if (!TryGetTarget(element, "target", out var target))
                {
                    error = "Move intent needs integer 'target': { x, y }.";
                    return false;
                }

                intent = new MoveIntent(entityId, target)
                {
                    IssuerCivilizationId = civilizationId,
                };
                return true;

            case "attack":
                if (!TryGetInt64(element, "targetId", out var targetId) || targetId <= 0)
                {
                    error = "Attack intent needs positive integer 'targetId'.";
                    return false;
                }

                intent = new AttackIntent(entityId, new EntityId(targetId))
                {
                    IssuerCivilizationId = civilizationId,
                };
                return true;

            case "send":
                if (!TryGetString(element, "toCivilizationId", out var toCiv) || string.IsNullOrEmpty(toCiv))
                {
                    error = "Send intent needs string 'toCivilizationId'.";
                    return false;
                }

                if (!TryGetString(element, "topic", out var topic) || string.IsNullOrWhiteSpace(topic))
                {
                    error = "Send intent needs string 'topic'.";
                    return false;
                }

                var payload = TryGetString(element, "payload", out var payloadValue)
                    ? payloadValue
                    : string.Empty;
                var targetRegionId = TryGetString(element, "targetRegionId", out var targetRegion)
                    ? targetRegion
                    : null;

                intent = new SendMessageIntent(entityId, toCiv, topic, payload, targetRegionId)
                {
                    IssuerCivilizationId = civilizationId,
                };
                return true;

            case "mine":
                intent = new MineIntent(entityId)
                {
                    IssuerCivilizationId = civilizationId,
                };
                return true;

            case "transfer":
                if (!TryGetString(element, "targetRegionId", out var transferRegion) ||
                    string.IsNullOrWhiteSpace(transferRegion))
                {
                    error = "Transfer intent needs string 'targetRegionId'.";
                    return false;
                }

                if (!TryGetInt64(element, "amount", out var amount) || amount <= 0)
                {
                    error = "Transfer intent needs positive integer 'amount'.";
                    return false;
                }

                intent = new TransferMaterialIntent(entityId, transferRegion, amount)
                {
                    IssuerCivilizationId = civilizationId,
                };
                return true;

            case "sendEnergy":
                if (!TryGetInt64(element, "amount", out var energyAmount) || energyAmount <= 0)
                {
                    error = "sendEnergy intent needs positive integer 'amount'.";
                    return false;
                }

                var energyTarget = TryGetString(element, "targetRegionId", out var energyTargetRegion)
                    ? energyTargetRegion
                    : null;
                intent = new SendEnergyIntent(entityId, energyAmount, energyTarget)
                {
                    IssuerCivilizationId = civilizationId,
                };
                return true;

            case "drawEnergy":
                if (!TryGetInt64(element, "amount", out var drawAmount) || drawAmount <= 0)
                {
                    error = "drawEnergy intent needs positive integer 'amount'.";
                    return false;
                }

                intent = new DrawEnergyIntent(entityId, drawAmount)
                {
                    IssuerCivilizationId = civilizationId,
                };
                return true;

            case "wait":
                intent = new WaitIntent(entityId)
                {
                    IssuerCivilizationId = civilizationId,
                };
                return true;

            default:
                error = $"Unknown intent type '{type}'.";
                return false;
        }
    }

    private static bool TryGetString(JsonElement element, string property, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(property, out var propertyValue) ||
            propertyValue.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = propertyValue.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryGetInt64(JsonElement element, string property, out long value)
    {
        value = 0;
        if (!element.TryGetProperty(property, out var propertyValue) ||
            propertyValue.ValueKind != JsonValueKind.Number ||
            !propertyValue.TryGetInt64(out value))
        {
            return false;
        }

        return true;
    }

    private static bool TryGetTarget(JsonElement element, string property, out Vec2 target)
    {
        target = default;
        if (!element.TryGetProperty(property, out var propertyValue) ||
            propertyValue.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryGetInt32(propertyValue, "x", out var x) || !TryGetInt32(propertyValue, "y", out var y))
        {
            return false;
        }

        target = new Vec2(x, y);
        return true;
    }

    private static bool TryGetInt32(JsonElement element, string property, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(property, out var propertyValue) ||
            propertyValue.ValueKind != JsonValueKind.Number ||
            !propertyValue.TryGetInt32(out value))
        {
            return false;
        }

        return true;
    }

}
