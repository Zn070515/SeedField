namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    /// <summary>Unified host entry for wreck salvage.</summary>
    public WreckCommandResult ExecuteWreckCommand(WreckCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        InvalidateObservationCache();

        var events = new List<SimulationEvent>();
        return command switch
        {
            RecoverWreckCommand recover => RecoverWreck(recover, events),
            _ => new WreckCommandResult(
                false,
                SimulationErrorCode.InvalidAction,
                "Unknown wreck command.",
                events),
        };
    }

    private WreckCommandResult RecoverWreck(
        RecoverWreckCommand command,
        List<SimulationEvent> events)
    {
        if (!World.Wrecks.TryGetValue(command.WreckId, out var wreck))
        {
            return new WreckCommandResult(
                false,
                SimulationErrorCode.WreckNotFound,
                $"Wreck '{command.WreckId}' does not exist.",
                events);
        }

        if (!World.Regions.TryGetValue(wreck.RegionId, out var region))
        {
            return new WreckCommandResult(
                false,
                SimulationErrorCode.WreckNotFound,
                $"Wreck region '{wreck.RegionId}' does not exist.",
                events);
        }

        var isOwner = string.Equals(
            wreck.OwnerCivilizationId,
            command.CivilizationId,
            StringComparison.Ordinal);
        var hasPresence = region.Entities.Values.Any(entity =>
            string.Equals(
                entity.CivilizationId,
                command.CivilizationId,
                StringComparison.Ordinal));
        if (!isOwner && !hasPresence)
        {
            return new WreckCommandResult(
                false,
                SimulationErrorCode.WreckUnavailable,
                "Only the owner or a civilization present in the region can salvage this wreck.",
                events);
        }

        region.AddMaterial(command.CivilizationId, wreck.MaterialRecoverable);
        World.Statistics.MaterialSalvagedTotal += wreck.MaterialRecoverable;
        World.Wrecks.Remove(wreck.Id);

        events.Add(new WreckSalvaged(
            World.Tick,
            wreck.Id,
            command.CivilizationId,
            region.Id,
            wreck.MaterialRecoverable));
        return new WreckCommandResult(
            true,
            SimulationErrorCode.None,
            $"Salvaged {wreck.MaterialRecoverable} M from wreck {wreck.Id}.",
            events);
    }
}
