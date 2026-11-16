using System.Text.Json;
using Jint;
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
public sealed class JsController : IDisposable
{
    private const string Harness = """
        function __seedfield_run(json) {
            var result = decide(JSON.parse(json));
            if (result === undefined || result === null) {
                throw new Error("decide() must return an intent array");
            }
            return JSON.stringify(result);
        }
        """;

    private readonly Engine _engine;

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
            _engine.Execute(script);
            _engine.Execute(Harness);

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

        var observationJson = ObservationJson.Serialize(observation);

        try
        {
            var result = _engine.Invoke("__seedfield_run", observationJson);
            var resultJson = result.AsString();
            return ParseDecision(resultJson, CivilizationId);
        }
        catch (TimeoutException exception)
        {
            return ControllerDecision.Fault(ScriptFaultCodes.Timeout, exception.Message);
        }
        catch (StatementsCountOverflowException exception)
        {
            return ControllerDecision.Fault(ScriptFaultCodes.StatementLimit, exception.Message);
        }
        catch (MemoryLimitExceededException exception)
        {
            return ControllerDecision.Fault(ScriptFaultCodes.MemoryLimit, exception.Message);
        }
        catch (JavaScriptException exception)
        {
            return ControllerDecision.Fault(ScriptFaultCodes.Fault, exception.Message);
        }
        catch (Exception exception)
        {
            return ControllerDecision.Fault(ScriptFaultCodes.Fault, exception.Message);
        }
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

    private static class ObservationJson
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static string Serialize(Observation observation) =>
            JsonSerializer.Serialize(ToDto(observation), Options);

        private static ObservationDto ToDto(Observation observation) =>
            new(
                observation.Tick,
                observation.CivilizationId,
                [.. observation.Entities.Select(entity => new EntityDto(
                    entity.EntityId,
                    entity.CivilizationId,
                    entity.RegionId,
                    entity.Position.X,
                    entity.Position.Y,
                    entity.Health,
                    entity.MaxHealth,
                    entity.IsSelf,
                    entity.HasSensor,
                    entity.SensorRangeMm,
                    entity.HasWeapon,
                    entity.WeaponRangeMm,
                    entity.HasDrive,
                    entity.HasComm))],
                [.. observation.Messages.Select(message => new MessageDto(
                    message.MessageId,
                    message.FromEntityId,
                    message.FromCivilizationId,
                    message.TargetRegionId,
                    message.Topic,
                    message.Payload,
                    message.SentTick,
                    message.DeliveredTick))],
                [.. observation.Shipments.Select(shipment => new ShipmentDto(
                    shipment.ShipmentId,
                    shipment.FromRegionId,
                    shipment.ToRegionId,
                    shipment.Amount,
                    shipment.SentTick,
                    shipment.ArriveTick,
                    shipment.Route))],
                [.. observation.EnergyShipments.Select(shipment => new EnergyShipmentDto(
                    shipment.ShipmentId,
                    shipment.FromRegionId,
                    shipment.ToRegionId,
                    shipment.Amount,
                    shipment.SentTick,
                    shipment.ArriveTick,
                    shipment.Route))],
                observation.ViewerEntityId,
                [.. (observation.ControlPoints ?? Array.Empty<ObservedControlPoint>())
                    .Select(point => new ControlPointDto(
                        point.Id,
                        point.RegionId,
                        point.Position.X,
                        point.Position.Y,
                        point.RadiusMm,
                        point.HoldTicksRequired,
                        point.HoldingCivilizationId,
                        point.HoldTicks,
                        point.OwnerCivilizationId))],
                observation.Match is null
                    ? null
                    : new MatchDto(
                        observation.Match.CapturedPointsToWin,
                        observation.Match.WinnerCivilizationId));

        private sealed record ObservationDto(
            long Tick,
            string CivilizationId,
            List<EntityDto> Entities,
            List<MessageDto> Messages,
            List<ShipmentDto> Shipments,
            List<EnergyShipmentDto> EnergyShipments,
            long? ViewerEntityId,
            List<ControlPointDto> ControlPoints,
            MatchDto? Match);

        private sealed record MatchDto(
            int CapturedPointsToWin,
            string? WinnerCivilizationId);

        private sealed record ControlPointDto(
            string Id,
            string RegionId,
            int X,
            int Y,
            int RadiusMm,
            int HoldTicksRequired,
            string? HoldingCivilizationId,
            int HoldTicks,
            string? OwnerCivilizationId);

        private sealed record ShipmentDto(
            long ShipmentId,
            string FromRegionId,
            string ToRegionId,
            long Amount,
            long SentTick,
            long ArriveTick,
            string Route);

        private sealed record EnergyShipmentDto(
            long ShipmentId,
            string FromRegionId,
            string ToRegionId,
            long Amount,
            long SentTick,
            long ArriveTick,
            string Route);

        private sealed record EntityDto(
            long EntityId,
            string CivilizationId,
            string RegionId,
            int X,
            int Y,
            int Health,
            int MaxHealth,
            bool IsSelf,
            bool HasSensor,
            int SensorRangeMm,
            bool HasWeapon,
            int WeaponRangeMm,
            bool HasDrive,
            bool HasComm);

        private sealed record MessageDto(
            long MessageId,
            long FromEntityId,
            string FromCivilizationId,
            string TargetRegionId,
            string Topic,
            string Payload,
            long SentTick,
            long DeliveredTick);
    }
}
