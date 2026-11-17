namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    private void RegenerateCores(long tick, List<SimulationEvent> events)
    {
        foreach (var core in World.Cores.Values)
        {
            core.RefreshRecoveryWindow(tick);
            core.RefreshRecoveryDebt(tick);

            var energyRoom = Math.Max(0, core.Config.MaxEnergyReserve - core.EnergyReserve);
            var materialRoom = Math.Max(0, core.Config.MaxMaterialReserve - core.MaterialReserve);

            var energyAdded = Math.Min(core.Config.EnergyPerTick, energyRoom);
            var materialAdded = Math.Min(core.Config.MaterialPerTick, materialRoom);

            if (energyAdded <= 0 && materialAdded <= 0)
            {
                continue;
            }

            core.EnergyReserve += energyAdded;
            core.MaterialReserve += materialAdded;
            World.Statistics.EnergyRegeneratedTotal += energyAdded;
            World.Statistics.MaterialRegeneratedTotal += materialAdded;

            events.Add(new CoreRegenerated(tick, core.CivilizationId, energyAdded, materialAdded));
        }
    }

    private static void ValidateBlueprint(RecoveryBlueprint blueprint)
    {
        if (blueprint.MaxHealth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blueprint), "Blueprint MaxHealth must be positive.");
        }

        if (blueprint.RequiredEnergy < 0 || blueprint.RequiredMaterial < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blueprint), "Recovery costs must be non-negative.");
        }

        if (blueprint.Energy < 0 || blueprint.Energy > blueprint.EnergyCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(blueprint), "Blueprint energy must be within capacity.");
        }
    }

    /// <summary>
    /// Shell pressure is derived, never stored: uncovered anchors add a per-anchor cost,
    /// and a hostile (non-NAP) civilization in a directly linked shell region adds siege
    /// cost and reduces the effective quota. Core regeneration itself is never reduced.
    /// </summary>
    private (int CostPercent, int QuotaPenalty) CorePressure(SeedCoreState core)
    {
        var costPercent = 0;
        if (core.Config.AnchorRecoveryCostPercent > 0)
        {
            costPercent +=
                CoreShellRules.CountUncoveredAnchors(World, core) *
                core.Config.AnchorRecoveryCostPercent;
        }

        var quotaPenalty = 0;
        if (IsCoreBesieged(core))
        {
            costPercent += core.Config.SiegeRecoveryCostPercent;
            quotaPenalty = core.Config.SiegeQuotaPenalty;
        }

        return (costPercent, quotaPenalty);
    }

    private bool IsCoreBesieged(SeedCoreState core)
    {
        foreach (var link in World.Links.Values)
        {
            if (!link.Connects(core.RegionId))
            {
                continue;
            }

            var otherRegionId = link.Other(core.RegionId);
            if (!World.Regions.TryGetValue(otherRegionId, out var region))
            {
                continue;
            }

            foreach (var entity in region.Entities.Values)
            {
                if (string.Equals(
                        entity.CivilizationId,
                        core.CivilizationId,
                        StringComparison.Ordinal) ||
                    HasActiveTreaty(
                        TreatyKind.NonAggression,
                        entity.CivilizationId,
                        core.CivilizationId))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private static long ScaleRecoveryCost(long cost, long percent)
    {
        if (cost <= 0 || percent <= 100)
        {
            return cost;
        }

        var scaled = ((Int128)cost * percent + 99) / 100;
        return scaled > long.MaxValue ? long.MaxValue : (long)scaled;
    }

    private static BootstrapResult BootstrapFailure(
        SimulationErrorCode code,
        string message,
        SimulationEvent? recoveryEvent = null) =>
        new(false, null, code, message, recoveryEvent);

}
