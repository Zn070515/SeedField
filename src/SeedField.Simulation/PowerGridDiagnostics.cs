namespace SeedField.Simulation;

/// <summary>Read-only power snapshot for one region and civilization.</summary>
public sealed record RegionPowerSnapshot(
    string RegionId,
    long GridEnergy,
    long GenerationPerTick,
    long LocalEnergy,
    long LocalCapacity,
    long ComponentEnergy,
    int ComponentRegionCount,
    bool IsBlackedOut,
    IReadOnlyList<string> ConnectedRegions);

public sealed record PowerGridDiagnostics(
    string CivilizationId,
    long TotalGridEnergy,
    long TotalLocalEnergy,
    long TotalCapacity,
    long TotalGenerationPerTick,
    IReadOnlyList<RegionPowerSnapshot> Regions);

/// <summary>
/// Derived diagnostics only. It never mutates the world and does not change simulation
/// results; the host UI and headless diagnostics share this view.
/// </summary>
public static class PowerGridDiagnosticsBuilder
{
    public static PowerGridDiagnostics Build(
        WorldState world,
        string civilizationId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrEmpty(civilizationId);

        var components = PowerGridTopology.BuildComponents(world);
        var componentByRegion = new Dictionary<string, IReadOnlyList<string>>(
            StringComparer.Ordinal);
        foreach (var component in components)
        {
            foreach (var regionId in component)
            {
                componentByRegion[regionId] = component;
            }
        }

        var snapshots = new List<RegionPowerSnapshot>();
        var totalGrid = 0L;
        var totalLocal = 0L;
        var totalCapacity = 0L;
        var totalGeneration = 0L;

        foreach (var region in world.Regions.Values)
        {
            var component = componentByRegion[region.Id];
            var componentEnergy = component.Sum(regionId =>
                world.Regions[regionId].GetEnergy(civilizationId));
            var componentHasPower = component.Any(regionId =>
                world.Regions[regionId].Entities.Values.Any(entity =>
                    string.Equals(
                        entity.CivilizationId,
                        civilizationId,
                        StringComparison.Ordinal) &&
                    entity.Power is not null));

            var generation = region.Entities.Values
                .Where(entity =>
                    string.Equals(
                        entity.CivilizationId,
                        civilizationId,
                        StringComparison.Ordinal) &&
                    entity.Power is not null)
                .Sum(entity => (long)entity.Power!.EnergyPerTick);
            var localEnergy = region.Entities.Values
                .Where(entity => string.Equals(
                    entity.CivilizationId,
                    civilizationId,
                    StringComparison.Ordinal))
                .Sum(entity => entity.Energy);
            var localCapacity = region.Entities.Values
                .Where(entity => string.Equals(
                    entity.CivilizationId,
                    civilizationId,
                    StringComparison.Ordinal))
                .Sum(entity => entity.EnergyCapacity);
            var connected = component
                .Where(regionId =>
                    !string.Equals(regionId, region.Id, StringComparison.Ordinal))
                .ToList();

            snapshots.Add(new RegionPowerSnapshot(
                region.Id,
                region.GetEnergy(civilizationId),
                generation,
                localEnergy,
                localCapacity,
                componentEnergy,
                component.Count,
                componentEnergy == 0 && componentHasPower,
                connected));

            totalGrid += region.GetEnergy(civilizationId);
            totalLocal += localEnergy;
            totalCapacity += localCapacity;
            totalGeneration += generation;
        }

        return new PowerGridDiagnostics(
            civilizationId,
            totalGrid,
            totalLocal,
            totalCapacity,
            totalGeneration,
            snapshots);
    }
}
