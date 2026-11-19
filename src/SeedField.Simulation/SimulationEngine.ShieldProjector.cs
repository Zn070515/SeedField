namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    /// <summary>
    /// Passive shield projection: friendly shielded entities in range are recharged
    /// in stable EntityId order. Costs local energy per target; stops when the
    /// projector cannot pay. It intentionally bypasses the target's own regen delay.
    /// </summary>
    private void ProjectShields(long tick, List<SimulationEvent> events)
    {
        foreach (var projector in World.EnumerateEntities().OrderBy(entity => entity.Id.Value))
        {
            if (projector.ShieldProjector is not
                {
                    RangeMm: > 0,
                    RechargePerTick: > 0,
                } module ||
                projector.IsCritical)
            {
                continue;
            }

            var rangeSquared = (long)module.RangeMm * module.RangeMm;
            var targets = World.EnumerateEntities()
                .Where(candidate =>
                    candidate.Id != projector.Id &&
                    candidate.CivilizationId == projector.CivilizationId &&
                    candidate.RegionId == projector.RegionId &&
                    candidate.Shield is not null &&
                    candidate.ShieldEnergy < candidate.Shield.Capacity &&
                    DistanceSquared(projector.Position, candidate.Position) <= rangeSquared)
                .OrderBy(candidate => candidate.Id.Value);

            foreach (var target in targets)
            {
                if (module.EnergyCostPerTarget > 0 &&
                    projector.Energy < module.EnergyCostPerTarget)
                {
                    break;
                }

                var shield = target.Shield!;
                var amount = Math.Min(
                    module.RechargePerTick,
                    shield.Capacity - target.ShieldEnergy);
                if (amount <= 0)
                {
                    continue;
                }

                if (module.EnergyCostPerTarget > 0)
                {
                    projector.Energy -= module.EnergyCostPerTarget;
                    World.Statistics.EnergySpentTotal += module.EnergyCostPerTarget;
                }

                target.ShieldEnergy += amount;
                World.Statistics.ShieldProjectedTotal += amount;
                events.Add(new ShieldProjected(
                    tick,
                    projector.Id,
                    target.Id,
                    amount,
                    target.ShieldEnergy,
                    module.EnergyCostPerTarget));
            }
        }
    }

    private static long DistanceSquared(Vec2 first, Vec2 second)
    {
        var dx = (long)second.X - first.X;
        var dy = (long)second.Y - first.Y;
        return (dx * dx) + (dy * dy);
    }
}
