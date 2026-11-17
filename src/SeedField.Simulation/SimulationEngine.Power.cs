namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    private void GeneratePower()
    {
        foreach (var region in World.Regions.Values)
        {
            if (!IsRegionActive(region.Id))
            {
                continue;
            }

            foreach (var entity in region.Entities.Values)
            {
                if (entity.Power is null)
                {
                    continue;
                }

                var generated = Math.Max(0, entity.Power.EnergyPerTick);
                if (generated <= 0)
                {
                    continue;
                }

                var room = Math.Max(0, entity.EnergyCapacity - entity.Energy);
                var toLocal = Math.Min(generated, room);
                var overflow = generated - toLocal;
                if (toLocal > 0)
                {
                    entity.Energy += toLocal;
                }

                if (overflow > 0)
                {
                    var free = region.GetGridFree(entity.CivilizationId);
                    var toGrid = free == long.MaxValue
                        ? overflow
                        : Math.Min(overflow, free);
                    if (toGrid > 0)
                    {
                        region.AddEnergy(entity.CivilizationId, toGrid);
                        World.Statistics.EnergyOverflowedTotal += toGrid;
                    }

                    var wasted = overflow - toGrid;
                    if (wasted > 0)
                    {
                        World.Statistics.EnergyWastedTotal += wasted;
                    }
                }

                World.Statistics.EnergyGeneratedTotal += generated;
            }
        }
    }

    /// <summary>Unified host entry for breaker open/close/maintenance.</summary>
    public PowerCommandResult ExecutePowerCommand(PowerCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var events = new List<SimulationEvent>();
        return command switch
        {
            OpenBreakerCommand open => OpenBreaker(open, events),
            CloseBreakerCommand close => CloseBreaker(close, events),
            MaintainBreakerCommand maintain => MaintainBreaker(maintain, events),
            _ => new PowerCommandResult(
                false,
                SimulationErrorCode.InvalidAction,
                "Unknown power command.",
                events),
        };
    }

    private PowerCommandResult OpenBreaker(
        OpenBreakerCommand command,
        List<SimulationEvent> events)
    {
        if (!World.Regions.ContainsKey(command.RegionId))
        {
            return new PowerCommandResult(
                false,
                SimulationErrorCode.InvalidAction,
                $"Region '{command.RegionId}' does not exist.",
                events);
        }

        var state = GetOrCreatePowerGridState(command.RegionId, command.CivilizationId);
        var changed = !state.BreakerOpen;
        state.BreakerOpen = true;
        state.ManualHold = true;
        if (changed)
        {
            events.Add(new PowerBlackout(
                World.Tick,
                command.CivilizationId,
                command.RegionId));
        }

        return new PowerCommandResult(
            true,
            SimulationErrorCode.None,
            $"Breaker for {command.CivilizationId} in '{command.RegionId}' is open.",
            events);
    }

    private PowerCommandResult CloseBreaker(
        CloseBreakerCommand command,
        List<SimulationEvent> events)
    {
        if (!World.Regions.TryGetValue(command.RegionId, out var region))
        {
            return new PowerCommandResult(
                false,
                SimulationErrorCode.InvalidAction,
                $"Region '{command.RegionId}' does not exist.",
                events);
        }

        var state = GetOrCreatePowerGridState(command.RegionId, command.CivilizationId);
        if (!state.BreakerOpen)
        {
            state.ManualHold = false;
            return new PowerCommandResult(
                true,
                SimulationErrorCode.None,
                "Breaker is already closed.",
                events);
        }

        if (state.MaintenanceRequired)
        {
            return new PowerCommandResult(
                false,
                SimulationErrorCode.BreakerMaintenanceRequired,
                "Breaker is locked out; perform maintenance before closing.",
                events);
        }

        if (region.GetEnergy(command.CivilizationId) <= 0)
        {
            return new PowerCommandResult(
                false,
                SimulationErrorCode.NoEnergy,
                "Cannot close a breaker on an empty grid.",
                events);
        }

        state.BreakerOpen = false;
        state.ManualHold = false;
        events.Add(new PowerRestored(
            World.Tick,
            command.CivilizationId,
            command.RegionId,
            region.GetEnergy(command.CivilizationId)));
        return new PowerCommandResult(
            true,
            SimulationErrorCode.None,
            $"Breaker for {command.CivilizationId} in '{command.RegionId}' is closed.",
            events);
    }

    private PowerCommandResult MaintainBreaker(
        MaintainBreakerCommand command,
        List<SimulationEvent> events)
    {
        if (!World.Regions.TryGetValue(command.RegionId, out var region))
        {
            return new PowerCommandResult(
                false,
                SimulationErrorCode.InvalidAction,
                $"Region '{command.RegionId}' does not exist.",
                events);
        }

        if (!World.PowerGridStates.TryGetValue(
                (command.RegionId, command.CivilizationId),
                out var state))
        {
            return new PowerCommandResult(
                false,
                SimulationErrorCode.InvalidAction,
                $"No breaker exists for {command.CivilizationId} in '{command.RegionId}'.",
                events);
        }

        var cost = BreakerMaintenanceCost(command.CivilizationId);
        if (cost > 0 &&
            !region.TryConsumeMaterial(command.CivilizationId, cost))
        {
            return new PowerCommandResult(
                false,
                SimulationErrorCode.InsufficientMaterial,
                $"Breaker maintenance costs {cost} material.",
                events);
        }

        state.TripCount = 0;
        state.MaintenanceRequired = false;
        if (cost > 0)
        {
            World.Statistics.MaterialSpentTotal += cost;
        }

        events.Add(new BreakerMaintained(
            World.Tick,
            command.CivilizationId,
            command.RegionId,
            cost));
        return new PowerCommandResult(
            true,
            SimulationErrorCode.None,
            $"Breaker for {command.CivilizationId} in '{command.RegionId}' maintained.",
            events);
    }

    private void UpdatePowerBreakers(long tick, List<SimulationEvent> events)
    {
        var loads = new SortedSet<(string RegionId, string CivilizationId)>();
        foreach (var region in World.Regions.Values)
        {
            foreach (var entity in region.Entities.Values)
            {
                if (entity.Power is not null)
                {
                    loads.Add((region.Id, entity.CivilizationId));
                }
            }
        }

        foreach (var load in loads)
        {
            var state = GetOrCreatePowerGridState(load.RegionId, load.CivilizationId);
            var grid = World.Regions[load.RegionId].GetEnergy(load.CivilizationId);

            if (state.BreakerOpen)
            {
                if (!state.ManualHold &&
                    !state.MaintenanceRequired &&
                    AutoReconnect(load.CivilizationId) &&
                    grid > 0 &&
                    grid >= PowerReserve(load.CivilizationId))
                {
                    state.BreakerOpen = false;
                    events.Add(new PowerRestored(
                        tick,
                        load.CivilizationId,
                        load.RegionId,
                        grid));
                }

                continue;
            }

            if (grid <= 0)
            {
                state.BreakerOpen = true;
                state.ManualHold = false;
                state.TripCount++;
                events.Add(new PowerBlackout(
                    tick,
                    load.CivilizationId,
                    load.RegionId));

                if (!state.MaintenanceRequired)
                {
                    var faultChance = BreakerFaultChance(load.CivilizationId);
                    var limit = BreakerTripLimit(load.CivilizationId);
                    if (faultChance > 0 && World.Random.Percent(faultChance))
                    {
                        state.MaintenanceRequired = true;
                        events.Add(new BreakerLockedOut(
                            tick,
                            load.CivilizationId,
                            load.RegionId,
                            state.TripCount,
                            "fault"));
                    }
                    else if (limit > 0 && state.TripCount >= limit)
                    {
                        state.MaintenanceRequired = true;
                        events.Add(new BreakerLockedOut(
                            tick,
                            load.CivilizationId,
                            load.RegionId,
                            state.TripCount,
                            "trips"));
                    }
                }
            }
        }
    }

    private long PowerReserve(string civilizationId) =>
        World.PowerPolicies.TryGetValue(civilizationId, out var policy)
            ? policy.ReserveEnergy
            : 0;

    private int BreakerTripLimit(string civilizationId) =>
        World.PowerPolicies.TryGetValue(civilizationId, out var policy)
            ? policy.BreakerTripLimit
            : 0;

    private long BreakerMaintenanceCost(string civilizationId) =>
        World.PowerPolicies.TryGetValue(civilizationId, out var policy)
            ? policy.BreakerMaintenanceMaterialCost
            : 0;

    private int BreakerFaultChance(string civilizationId) =>
        World.PowerPolicies.TryGetValue(civilizationId, out var policy)
            ? policy.BreakerFaultChancePercent
            : 0;

    private long DrawFloor(EntityState entity)
    {
        if (!World.PowerPolicies.TryGetValue(
                entity.CivilizationId,
                out var policy))
        {
            return 0;
        }

        var floor = policy.ReserveEnergy;
        if (entity.Power is not null &&
            entity.Power.Priority < policy.HighPriorityThreshold)
        {
            floor += policy.PriorityReserveEnergy;
        }

        return floor;
    }

    private bool TryAutoDrawForAction(
        EntityState entity,
        RegionState region,
        long cost,
        long tick,
        List<SimulationEvent> events)
    {
        if (entity.Energy >= cost)
        {
            return true;
        }

        if (entity.Power is null ||
            IsBreakerOpen(region.Id, entity.CivilizationId))
        {
            return false;
        }

        var floor = DrawFloor(entity);
        var available = Math.Max(
            0,
            region.GetEnergy(entity.CivilizationId) - floor);
        var needed = cost - entity.Energy;
        var headroom = entity.EnergyCapacity - entity.Energy;
        if (needed > headroom ||
            entity.Energy + available < cost)
        {
            return false;
        }

        var amount = Math.Min(needed, available);
        if (amount <= 0 ||
            !region.TryConsumeEnergy(entity.CivilizationId, amount))
        {
            return false;
        }

        entity.Energy += amount;
        World.Statistics.EnergyDrawnTotal += amount;
        World.Statistics.EnergyAutoDrawnTotal += amount;
        events.Add(new EnergyAutoDrawn(
            tick,
            entity.Id,
            region.Id,
            amount));
        return entity.Energy >= cost;
    }

    private long ActionAvailableEnergy(EntityState entity, RegionState region)
    {
        if (entity.Power is null ||
            IsBreakerOpen(region.Id, entity.CivilizationId))
        {
            return entity.Energy;
        }

        var floor = DrawFloor(entity);
        return entity.Energy + Math.Max(
            0,
            region.GetEnergy(entity.CivilizationId) - floor);
    }

    private string ActionShortfallSource(
        EntityState entity,
        RegionState region,
        long requested)
    {
        if (entity.Power is null)
        {
            return "local";
        }

        if (IsBreakerOpen(region.Id, entity.CivilizationId))
        {
            return "blackout";
        }

        var grid = region.GetEnergy(entity.CivilizationId);
        if (grid <= 0)
        {
            return "local";
        }

        var floor = DrawFloor(entity);
        var baseReserve = PowerReserve(entity.CivilizationId);
        if (floor > baseReserve && grid < requested + floor)
        {
            return "priority";
        }

        return baseReserve > 0 && grid < requested + baseReserve
            ? "reserve"
            : "grid";
    }

    private static void AddPowerShortfall(
        EntityState entity,
        string regionId,
        long requested,
        long available,
        string source,
        long tick,
        List<SimulationEvent> events) =>
        events.Add(new PowerShortfall(
            tick,
            entity.CivilizationId,
            regionId,
            entity.Id,
            requested,
            available,
            source));

}
