namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    /// <summary>
    /// Passive production chain: smelters convert regional ore to material, then
    /// fabricators convert material to components up to their regional cap. Both
    /// spend local energy only and stop while the device is critically damaged.
    /// </summary>
    private void ProduceMaterials(long tick, List<SimulationEvent> events)
    {
        foreach (var entity in World.EnumerateEntities())
        {
            if (!entity.ProductionEnabled ||
                entity.IsCritical ||
                !World.Regions.TryGetValue(entity.RegionId, out var region))
            {
                continue;
            }

            if (entity.Smelter is { OrePerTick: > 0 } smelter)
            {
                var ore = region.GetOre(entity.CivilizationId);
                var amount = Math.Min(smelter.OrePerTick, ore);
                if (amount > 0 &&
                    entity.Energy >= smelter.EnergyCostPerTick &&
                    region.TryConsumeOre(entity.CivilizationId, amount))
                {
                    entity.Energy -= smelter.EnergyCostPerTick;
                    World.Statistics.EnergySpentTotal += smelter.EnergyCostPerTick;
                    region.AddMaterial(entity.CivilizationId, amount);
                    World.Statistics.OreSmeltedTotal += amount;
                    events.Add(new OreSmelted(
                        tick,
                        entity.Id,
                        region.Id,
                        amount,
                        region.GetOre(entity.CivilizationId)));
                }
            }

            if (entity.Fabricator is { ComponentsPerTick: > 0 } fabricator)
            {
                var components = region.GetComponents(entity.CivilizationId);
                var room = fabricator.MaxComponents - components;
                var amount = Math.Min(fabricator.ComponentsPerTick, room);
                var materialNeeded = amount * fabricator.MaterialPerComponent;
                if (amount > 0 &&
                    entity.Energy >= fabricator.EnergyCostPerTick &&
                    region.GetMaterial(entity.CivilizationId) >= materialNeeded &&
                    region.TryConsumeMaterial(entity.CivilizationId, materialNeeded))
                {
                    entity.Energy -= fabricator.EnergyCostPerTick;
                    World.Statistics.EnergySpentTotal += fabricator.EnergyCostPerTick;
                    region.AddComponents(entity.CivilizationId, amount);
                    World.Statistics.ComponentsFabricatedTotal += amount;
                    events.Add(new ComponentsFabricated(
                        tick,
                        entity.Id,
                        region.Id,
                        amount,
                        materialNeeded,
                        region.GetComponents(entity.CivilizationId)));
                }
            }
        }
    }
}
