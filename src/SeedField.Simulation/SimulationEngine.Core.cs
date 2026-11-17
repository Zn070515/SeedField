namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    private void RegenerateCores(long tick, List<SimulationEvent> events)
    {
        foreach (var core in World.Cores.Values)
        {
            core.RefreshRecoveryWindow(tick);
            core.RefreshRecoveryDebt(tick);

            var anchors = CoreShellRules.CountAliveAnchors(World, core.CivilizationId);
            var storageBonus = SaturatingMultiply(anchors, core.Config.AnchorStorageBonus);
            var maxEnergy = SaturatingAdd(core.Config.MaxEnergyReserve, storageBonus);
            var maxMaterial = SaturatingAdd(core.Config.MaxMaterialReserve, storageBonus);
            var energyRate = SaturatingAdd(
                core.Config.EnergyPerTick,
                SaturatingMultiply(anchors, core.Config.AnchorEnergyPerTick));
            var materialRate = SaturatingAdd(
                core.Config.MaterialPerTick,
                SaturatingMultiply(anchors, core.Config.AnchorMaterialPerTick));

            var energyRoom = Math.Max(0, maxEnergy - core.EnergyReserve);
            var materialRoom = Math.Max(0, maxMaterial - core.MaterialReserve);

            var energyAdded = Math.Min(energyRate, energyRoom);
            var materialAdded = Math.Min(materialRate, materialRoom);

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

    private static long SaturatingMultiply(int count, long value)
    {
        if (count <= 0 || value <= 0)
        {
            return 0;
        }

        var product = (Int128)count * value;
        return product > long.MaxValue ? long.MaxValue : (long)product;
    }

    private static long SaturatingAdd(long left, long right)
    {
        var sum = (Int128)left + right;
        return sum > long.MaxValue ? long.MaxValue : (long)sum;
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
