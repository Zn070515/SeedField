namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    private void ProcessMine(
        EntityState entity,
        RegionState region,
        MineIntent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results)
    {
        if (entity.Drill is not { } drill)
        {
            Reject(
                intent,
                SimulationErrorCode.NoDrill,
                "Entity has no drill module.",
                tick,
                events,
                results);
            return;
        }

        var deposit = NearestDepositInRange(
            region.Id,
            entity.Position,
            drill.RangeMm,
            skipDepleted: true);
        if (deposit is null)
        {
            var depleted = NearestDepositInRange(
                region.Id,
                entity.Position,
                drill.RangeMm,
                skipDepleted: false);
            Reject(
                intent,
                depleted is null
                    ? SimulationErrorCode.DepositOutOfRange
                    : SimulationErrorCode.DepositDepleted,
                depleted is null
                    ? "No deposit is inside the drill's range."
                    : $"Deposit '{depleted.Id}' is depleted.",
                tick,
                events,
                results);
            return;
        }

        var amount = Math.Min(drill.RatePerTick, deposit.Remaining);
        if (amount <= 0)
        {
            Reject(
                intent,
                SimulationErrorCode.DepositDepleted,
                $"Deposit '{deposit.Id}' cannot yield material.",
                tick,
                events,
                results);
            return;
        }

        var cost = drill.EnergyCostPerTick;
        if (entity.Energy < cost &&
            !TryAutoDrawForAction(entity, region, cost, tick, events))
        {
            AddPowerShortfall(
                entity,
                region.Id,
                cost,
                ActionAvailableEnergy(entity, region),
                ActionShortfallSource(entity, region, cost),
                tick,
                events);
            Reject(
                intent,
                SimulationErrorCode.NoEnergy,
                "Not enough energy to run the drill.",
                tick,
                events,
                results);
            return;
        }

        entity.Energy -= cost;
        World.Statistics.EnergySpentTotal += cost;
        deposit.Remaining -= amount;
        region.AddOre(entity.CivilizationId, deposit.Kind, amount);
        World.Statistics.OreMinedTotal += amount;

        events.Add(new OreMined(
            tick,
            entity.Id,
            deposit.Id,
            region.Id,
            deposit.Kind,
            amount,
            deposit.Remaining));
        Accept(
            intent,
            $"Mined {amount} M from deposit {deposit.Id} ({deposit.Remaining} left).",
            results);
    }

    /// <summary>
    /// Scouting: a living own device with a sensor or drill discovers deposits inside
    /// its effective range. Discovery is permanent per civilization.
    /// </summary>
    private void DiscoverDeposits()
    {
        foreach (var entity in World.EnumerateEntities())
        {
            var range = Math.Max(
                entity.IsCritical ? 0 : entity.Sensor?.RangeMm ?? 0,
                entity.Drill?.RangeMm ?? 0);
            if (range <= 0 || !World.Regions.ContainsKey(entity.RegionId))
            {
                continue;
            }

            foreach (var deposit in World.Deposits.Values)
            {
                if (string.Equals(
                        deposit.RegionId,
                        entity.RegionId,
                        StringComparison.Ordinal) &&
                    entity.Position.IsWithinRange(deposit.Position, range))
                {
                    World.DiscoverDeposit(entity.CivilizationId, deposit.Id);
                }
            }
        }
    }

    /// <summary>
    /// Deterministic nearest selection: squared distance first, then stable deposit id.
    /// Depleted deposits stay in the world (so art/UI can show them) but are skipped.
    /// </summary>
    private ResourceDepositState? NearestDepositInRange(
        string regionId,
        Vec2 position,
        int rangeMm,
        bool skipDepleted)
    {
        ResourceDepositState? best = null;
        var bestDistance = long.MaxValue;
        foreach (var deposit in World.Deposits.Values)
        {
            if ((skipDepleted && deposit.IsDepleted) ||
                !string.Equals(deposit.RegionId, regionId, StringComparison.Ordinal) ||
                !position.IsWithinRange(deposit.Position, rangeMm))
            {
                continue;
            }

            var distance = position.DistanceSquaredTo(deposit.Position);
            if (best is null || distance < bestDistance ||
                (distance == bestDistance && deposit.Id < best.Id))
            {
                best = deposit;
                bestDistance = distance;
            }
        }

        return best;
    }
}
