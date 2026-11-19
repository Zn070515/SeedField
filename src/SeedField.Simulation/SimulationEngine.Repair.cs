namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    /// <summary>
    /// Passive repair: the nearest damaged friendly entity in the same region is
    /// repaired once per tick. The repairer must not be critical. Energy is local;
    /// material is regional. The full cost is paid only when a target is repaired.
    /// </summary>
    private void RepairEntities(long tick, List<SimulationEvent> events)
    {
        foreach (var repairer in World.EnumerateEntities().OrderBy(entity => entity.Id.Value))
        {
            if (repairer.Repair is not { HealthPerTick: > 0, RangeMm: > 0 } repair ||
                repairer.IsCritical ||
                !World.Regions.TryGetValue(repairer.RegionId, out var region))
            {
                continue;
            }

            var rangeSquared = (long)repair.RangeMm * repair.RangeMm;
            EntityState? target = null;
            var bestDistance = long.MaxValue;

            foreach (var candidate in World.EnumerateEntities())
            {
                if (candidate.Id == repairer.Id ||
                    candidate.CivilizationId != repairer.CivilizationId ||
                    candidate.RegionId != repairer.RegionId ||
                    candidate.Health >= candidate.MaxHealth)
                {
                    continue;
                }

                var dx = (long)candidate.Position.X - repairer.Position.X;
                var dy = (long)candidate.Position.Y - repairer.Position.Y;
                var distance = (dx * dx) + (dy * dy);
                if (distance > rangeSquared)
                {
                    continue;
                }

                if (distance < bestDistance ||
                    (distance == bestDistance &&
                     target is not null &&
                     candidate.Id.Value < target.Id.Value))
                {
                    target = candidate;
                    bestDistance = distance;
                }
            }

            if (target is null)
            {
                continue;
            }

            var amount = Math.Min(repair.HealthPerTick, target.MaxHealth - target.Health);
            if (amount <= 0)
            {
                continue;
            }

            if (repair.EnergyCostPerTick > 0 && repairer.Energy < repair.EnergyCostPerTick)
            {
                continue;
            }

            if (repair.MaterialPerTick > 0 &&
                region.GetMaterial(repairer.CivilizationId) < repair.MaterialPerTick)
            {
                continue;
            }

            if (repair.EnergyCostPerTick > 0)
            {
                repairer.Energy -= repair.EnergyCostPerTick;
                World.Statistics.EnergySpentTotal += repair.EnergyCostPerTick;
            }

            if (repair.MaterialPerTick > 0)
            {
                region.TryConsumeMaterial(repairer.CivilizationId, repair.MaterialPerTick);
            }

            target.Health += amount;
            World.Statistics.HealthRepairedTotal += amount;
            events.Add(new EntityRepaired(
                tick,
                repairer.Id,
                target.Id,
                amount,
                target.Health,
                repair.EnergyCostPerTick,
                repair.MaterialPerTick));
        }
    }
}
