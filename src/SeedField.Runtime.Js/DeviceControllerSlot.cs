using SeedField.Simulation;

namespace SeedField.Runtime.Js;

/// <summary>
/// One script instance bound to one entity. It receives a device-scoped observation and
/// may only submit intents for that same entity; cross-device intents are discarded.
/// </summary>
public sealed class DeviceControllerSlot : IDisposable
{
    private readonly ControllerSlot _slot;

    public DeviceControllerSlot(
        EntityId entityId,
        string civilizationId,
        string initialScript,
        JsControllerOptions? options = null,
        int version = 1)
    {
        EntityId = entityId;
        _slot = new ControllerSlot(civilizationId, initialScript, options, version);
    }

    public EntityId EntityId { get; }

    public string CivilizationId => _slot.CivilizationId;

    public int Version => _slot.Version;

    public string DeployedScript => _slot.DeployedScript;

    public ControllerDeploymentResult Deploy(string script) => _slot.Deploy(script);

    public ControllerDecision Decide(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (observation.ViewerEntityId != EntityId.Value)
        {
            return ControllerDecision.Fault(
                ScriptFaultCodes.DeviceScope,
                $"Observation is not scoped to device '{EntityId}'.");
        }

        var decision = _slot.Decide(observation);
        var intents = new List<Intent>();
        var faults = new List<ControllerFault>(decision.Faults);

        foreach (var intent in decision.Intents)
        {
            if (intent.EntityId != EntityId)
            {
                faults.Add(new ControllerFault(
                    ScriptFaultCodes.DeviceScope,
                    $"Device '{EntityId}' attempted to command entity '{intent.EntityId}'."));
                continue;
            }

            intents.Add(intent);
        }

        return new ControllerDecision(intents, faults);
    }

    public void Dispose() => _slot.Dispose();
}
