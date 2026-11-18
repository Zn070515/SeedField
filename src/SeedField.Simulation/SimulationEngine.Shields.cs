namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    /// <summary>
    /// Passive shield regeneration. It spends local energy only, never auto-draws
    /// from the grid, stops while the device is damaged, and is skipped when the
    /// regen delay has not elapsed or the shield is full.
    /// </summary>
    private void RegenerateShields(long tick)
    {
        foreach (var entity in World.EnumerateEntities())
        {
            if (entity.Shield is not { } shield ||
                entity.IsDamaged ||
                entity.ShieldEnergy >= shield.Capacity ||
                tick < entity.ShieldRegenReadyTick)
            {
                continue;
            }

            var amount = Math.Min(
                shield.RegenPerTick,
                shield.Capacity - entity.ShieldEnergy);
            if (amount <= 0)
            {
                continue;
            }

            var cost = shield.EnergyCostPerRegen;
            if (cost > 0 && entity.Energy < cost)
            {
                continue;
            }

            entity.Energy -= cost;
            World.Statistics.EnergySpentTotal += cost;
            entity.ShieldEnergy += amount;
            World.Statistics.ShieldRegeneratedTotal += amount;
        }
    }
}
