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
                var kind = ChooseOreKind(smelter, region, entity.CivilizationId);
                if (kind is { } chosen && entity.Energy >= smelter.EnergyCostPerTick)
                {
                    var recipe = SmeltingRecipes.For(chosen);
                    var available = region.GetOre(entity.CivilizationId, chosen);
                    var batchesByOre = available / recipe.OrePerBatch;
                    var batchesByRate = Math.Max(1, smelter.OrePerTick / recipe.OrePerBatch);
                    var batches = Math.Min(batchesByOre, batchesByRate);
                    var oreUsed = batches * recipe.OrePerBatch;
                    if (batches > 0 &&
                        region.TryConsumeOre(entity.CivilizationId, chosen, oreUsed))
                    {
                        entity.Energy -= smelter.EnergyCostPerTick;
                        World.Statistics.EnergySpentTotal += smelter.EnergyCostPerTick;
                        region.AddMaterial(
                            entity.CivilizationId,
                            batches * recipe.MaterialPerBatch);
                        World.Statistics.OreSmeltedTotal += oreUsed;
                        SmeltByproduct byproduct = SmeltByproduct.None;
                        var byproductAmount = 0L;
                        if (recipe.Byproduct != SmeltByproduct.None &&
                            recipe.ByproductPerBatch > 0)
                        {
                            byproduct = recipe.Byproduct;
                            byproductAmount = batches * recipe.ByproductPerBatch;
                            switch (byproduct)
                            {
                                case SmeltByproduct.Components:
                                    region.AddComponents(
                                        entity.CivilizationId,
                                        byproductAmount);
                                    break;
                                case SmeltByproduct.Energy:
                                    region.AddEnergy(entity.CivilizationId, byproductAmount);
                                    break;
                            }

                            World.Statistics.ByproductProducedTotal += byproductAmount;
                        }

                        events.Add(new OreSmelted(
                            tick,
                            entity.Id,
                            region.Id,
                            oreUsed,
                            region.GetOre(entity.CivilizationId, chosen),
                            chosen,
                            byproduct,
                            byproductAmount));
                    }
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

    private static OreKind? ChooseOreKind(
        SmelterModule smelter,
        RegionState region,
        string civilizationId)
    {
        if (smelter.PreferredOre is { } preferred &&
            region.GetOre(civilizationId, preferred) >=
            SmeltingRecipes.For(preferred).OrePerBatch)
        {
            return preferred;
        }

        OreKind? best = null;
        var bestStock = -1L;
        foreach (var recipe in SmeltingRecipes.All)
        {
            var stock = region.GetOre(civilizationId, recipe.Kind);
            if (stock >= recipe.OrePerBatch && stock > bestStock)
            {
                best = recipe.Kind;
                bestStock = stock;
            }
        }

        return best;
    }
}
