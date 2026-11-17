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
    IReadOnlyList<string> ConnectedRegions,
    long ReserveEnergy,
    bool IsLoadShed,
    long IncomingPower,
    long OutgoingPower,
    long GridCapacity,
    long GridFree,
    bool BreakerOpen);

public sealed record PowerGridDiagnostics(
    string CivilizationId,
    long TotalGridEnergy,
    long TotalLocalEnergy,
    long TotalCapacity,
    long TotalGenerationPerTick,
    IReadOnlyList<RegionPowerSnapshot> Regions,
    long ReserveEnergy,
    long TotalInTransitPower,
    int HighPriorityThreshold,
    long PriorityReserveEnergy,
    bool AutoReconnect);

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

        var hasPolicy = world.PowerPolicies.TryGetValue(civilizationId, out var policy);
        var reserveEnergy = hasPolicy ? policy!.ReserveEnergy : 0;
        var highPriorityThreshold = hasPolicy ? policy!.HighPriorityThreshold : 0;
        var priorityReserveEnergy = hasPolicy ? policy!.PriorityReserveEnergy : 0;
        var autoReconnect = !hasPolicy || policy!.AutoReconnect;

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
            var regionHasPower = region.Entities.Values.Any(entity =>
                string.Equals(
                    entity.CivilizationId,
                    civilizationId,
                    StringComparison.Ordinal) &&
                entity.Power is not null);
            var gridEnergy = region.GetEnergy(civilizationId);
            var gridCapacity = region.GetGridCapacity(civilizationId);
            var gridFree = region.GetGridFree(civilizationId);
            var breakerOpen = world.PowerGridStates.TryGetValue(
                (region.Id, civilizationId),
                out var gridState) &&
                gridState.BreakerOpen;
            var incomingPower = world.InTransitPower
                .Where(shipment =>
                    string.Equals(
                        shipment.CivilizationId,
                        civilizationId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        shipment.ToRegionId,
                        region.Id,
                        StringComparison.Ordinal))
                .Sum(shipment => shipment.Amount);
            var outgoingPower = world.InTransitPower
                .Where(shipment =>
                    string.Equals(
                        shipment.CivilizationId,
                        civilizationId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        shipment.FromRegionId,
                        region.Id,
                        StringComparison.Ordinal))
                .Sum(shipment => shipment.Amount);

            snapshots.Add(new RegionPowerSnapshot(
                region.Id,
                gridEnergy,
                generation,
                localEnergy,
                localCapacity,
                componentEnergy,
                component.Count,
                breakerOpen || (componentEnergy == 0 && componentHasPower),
                connected,
                reserveEnergy,
                regionHasPower && gridEnergy < reserveEnergy,
                incomingPower,
                outgoingPower,
                gridCapacity,
                gridFree,
                breakerOpen));

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
            snapshots,
            reserveEnergy,
            world.InTransitPower
                .Where(shipment => string.Equals(
                    shipment.CivilizationId,
                    civilizationId,
                    StringComparison.Ordinal))
                .Sum(shipment => shipment.Amount),
            highPriorityThreshold,
            priorityReserveEnergy,
            autoReconnect);
    }
}
