namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    /// <summary>Unified host entry for offline ruin import.</summary>
    public RuinCommandResult ExecuteRuinCommand(RuinCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var events = new List<SimulationEvent>();
        return command switch
        {
            ImportRuinCommand import => ImportRuin(import, events),
            _ => new RuinCommandResult(
                false,
                SimulationErrorCode.InvalidAction,
                "Unknown ruin command.",
                events),
        };
    }

    private RuinCommandResult ImportRuin(
        ImportRuinCommand command,
        List<SimulationEvent> events)
    {
        if (!World.Regions.TryGetValue(command.RegionId, out var region))
        {
            return Fail($"Region '{command.RegionId}' does not exist.");
        }

        if (region.IsCoreRegion)
        {
            return Fail("Ruin snapshots cannot be imported into a protected core region.");
        }

        if (command.RadiusMm <= 0 ||
            command.AgingPercent is < 0 or > 90 ||
            command.LootPercent is < 0 or > 100 ||
            !region.Contains(new Vec2(command.CenterX, command.CenterY)))
        {
            return Fail("Ruin import options are outside their allowed range.");
        }

        RuinSnapshotData snapshot;
        try
        {
            snapshot = RuinSnapshotSerializer.Load(command.SnapshotJson);
        }
        catch (RuinSnapshotException exception)
        {
            return Fail($"Invalid ruin snapshot [{exception.Code}]: {exception.Message}");
        }

        var sliceX = World.Random.NextInt(Math.Max(1, snapshot.WidthMm));
        var sliceY = World.Random.NextInt(Math.Max(1, snapshot.HeightMm));
        var radiusSquared = (long)command.RadiusMm * command.RadiusMm;
        var entriesInSlice = 0;
        var wrecksCreated = 0;
        var materialTotal = 0L;

        foreach (var entry in snapshot.Entries)
        {
            var dx = entry.X - sliceX;
            var dy = entry.Y - sliceY;
            if ((long)dx * dx + (long)dy * dy > radiusSquared)
            {
                continue;
            }

            entriesInSlice++;
            var targetX = command.CenterX + dx;
            var targetY = command.CenterY + dy;
            if (!region.Contains(new Vec2(targetX, targetY)))
            {
                continue;
            }

            var material = entry.MaterialValue;
            material = material * (100 - command.AgingPercent) / 100;
            material = material * (100 - command.LootPercent) / 100;
            if (material <= 0)
            {
                continue;
            }

            var wreckId = World.NextWreckId++;
            World.Wrecks.Add(
                wreckId,
                new WreckState(
                    wreckId,
                    region.Id,
                    new Vec2(targetX, targetY),
                    snapshot.SourceCivilizationId,
                    entry.BlueprintId,
                    entry.BlueprintVersion,
                    material,
                    World.Tick));
            wrecksCreated++;
            materialTotal += material;
        }

        events.Add(new RuinImported(
            World.Tick,
            command.CivilizationId,
            region.Id,
            snapshot.SourceCivilizationId,
            entriesInSlice,
            wrecksCreated,
            materialTotal));

        return new RuinCommandResult(
            true,
            SimulationErrorCode.None,
            $"Imported {wrecksCreated} ruin(s) with {materialTotal} M from {entriesInSlice} entr(ies).",
            events);

        RuinCommandResult Fail(string message) =>
            new(false, SimulationErrorCode.RuinImportRejected, message, events);
    }
}
