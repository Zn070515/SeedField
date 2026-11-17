namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    private void ProcessTransferMaterial(
        EntityState entity,
        RegionState sourceRegion,
        TransferMaterialIntent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results,
        Dictionary<string, int> materialLinkUsage)
    {
        if (entity.Drive is null)
        {
            Reject(intent, SimulationErrorCode.NoDrive, "Entity has no drive module.", tick, events, results);
            return;
        }

        if (intent.Amount <= 0)
        {
            Reject(intent, SimulationErrorCode.InvalidAction, "Transfer amount must be positive.", tick, events, results);
            return;
        }

        if (string.Equals(intent.TargetRegionId, sourceRegion.Id, StringComparison.Ordinal))
        {
            Reject(
                intent,
                SimulationErrorCode.InvalidAction,
                "Material transfer target must be a different region.",
                tick,
                events,
                results);
            return;
        }

        if (!World.Regions.ContainsKey(intent.TargetRegionId))
        {
            Reject(
                intent,
                SimulationErrorCode.NoRoute,
                $"Target region '{intent.TargetRegionId}' does not exist.",
                tick,
                events,
                results);
            return;
        }

        var path = FindMessagePath(sourceRegion.Id, intent.TargetRegionId);
        if (path is null)
        {
            Reject(
                intent,
                SimulationErrorCode.NoRoute,
                $"No link path from '{sourceRegion.Id}' to '{intent.TargetRegionId}'.",
                tick,
                events,
                results);
            return;
        }

        foreach (var link in path)
        {
            if (link.MaterialPerTick > 0 &&
                materialLinkUsage.GetValueOrDefault(link.Id) >= link.MaterialPerTick)
            {
                Reject(
                    intent,
                    SimulationErrorCode.NoBandwidth,
                    $"Link '{link.Id}' material capacity is exhausted for this tick.",
                    tick,
                    events,
                    results);
                return;
            }
        }

        if (!sourceRegion.TryConsumeMaterial(entity.CivilizationId, intent.Amount))
        {
            Reject(
                intent,
                SimulationErrorCode.InsufficientMaterial,
                $"Region '{sourceRegion.Id}' has {sourceRegion.GetMaterial(entity.CivilizationId)} M; " +
                $"transfer needs {intent.Amount}.",
                tick,
                events,
                results);
            return;
        }

        var route = string.Join(">", path.Select(link => link.Id));
        var shipmentId = World.NextShipmentId++;
        var arriveTick = tick + Math.Max(1, path.Sum(link => link.LatencyTicks));
        var shipment = new MaterialShipment(
            shipmentId,
            entity.Id,
            entity.CivilizationId,
            sourceRegion.Id,
            intent.TargetRegionId,
            route,
            intent.Amount,
            tick,
            arriveTick);

        foreach (var link in path)
        {
            if (link.MaterialPerTick > 0)
            {
                materialLinkUsage[link.Id] = materialLinkUsage.GetValueOrDefault(link.Id) + 1;
            }
        }

        World.InTransitMaterial.Add(shipment);
        World.Statistics.MaterialShippedTotal += intent.Amount;

        events.Add(new MaterialShipped(
            tick,
            shipmentId,
            entity.Id,
            sourceRegion.Id,
            intent.TargetRegionId,
            intent.Amount,
            arriveTick,
            route));

        Accept(
            intent,
            $"Shipment {shipmentId} queued: {intent.Amount} M arrives at tick {arriveTick}.",
            results);
    }

    private void DeliverDueMaterialShipments(long tick, List<SimulationEvent> events)
    {
        if (World.InTransitMaterial.Count == 0)
        {
            return;
        }

        var due = World.InTransitMaterial
            .Where(shipment => shipment.ArriveTick <= tick)
            .OrderBy(shipment => shipment.ArriveTick)
            .ThenBy(shipment => shipment.Id)
            .ToList();

        foreach (var shipment in due)
        {
            World.InTransitMaterial.Remove(shipment);

            if (!World.Regions.TryGetValue(shipment.ToRegionId, out var destination))
            {
                continue;
            }

            destination.AddMaterial(shipment.CivilizationId, shipment.Amount);
            World.Statistics.MaterialDeliveredTotal += shipment.Amount;
            events.Add(new MaterialArrived(
                tick,
                shipment.Id,
                shipment.ToRegionId,
                shipment.Amount));
        }
    }

    private void ProcessSendEnergy(
        EntityState entity,
        RegionState sourceRegion,
        SendEnergyIntent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results,
        Dictionary<string, int> energyLinkUsage)
    {
        if (entity.Power is null)
        {
            Reject(intent, SimulationErrorCode.NoPower, "Entity has no power module.", tick, events, results);
            return;
        }

        if (intent.Amount <= 0)
        {
            Reject(intent, SimulationErrorCode.InvalidAction, "Energy amount must be positive.", tick, events, results);
            return;
        }

        var targetRegionId = string.IsNullOrWhiteSpace(intent.TargetRegionId)
            ? sourceRegion.Id
            : intent.TargetRegionId;

        if (!World.Regions.ContainsKey(targetRegionId))
        {
            Reject(
                intent,
                SimulationErrorCode.NoRoute,
                $"Target region '{targetRegionId}' does not exist.",
                tick,
                events,
                results);
            return;
        }

        if (entity.Energy < intent.Amount)
        {
            AddPowerShortfall(
                entity,
                sourceRegion.Id,
                intent.Amount,
                entity.Energy,
                "local",
                tick,
                events);
            Reject(intent, SimulationErrorCode.NoEnergy, "Not enough local energy.", tick, events, results);
            return;
        }

        if (string.Equals(targetRegionId, sourceRegion.Id, StringComparison.Ordinal))
        {
            if (sourceRegion.GetGridFree(entity.CivilizationId) < intent.Amount)
            {
                Reject(
                    intent,
                    SimulationErrorCode.GridFull,
                    $"Region '{sourceRegion.Id}' grid has insufficient free capacity.",
                    tick,
                    events,
                    results);
                return;
            }

            entity.Energy -= intent.Amount;
            sourceRegion.AddEnergy(entity.CivilizationId, intent.Amount);
            World.Statistics.EnergyStoredTotal += intent.Amount;
            events.Add(new EnergyStored(tick, entity.Id, sourceRegion.Id, intent.Amount));
            Accept(intent, $"Stored {intent.Amount} E in the local grid.", results);
            return;
        }

        var path = FindMessagePath(sourceRegion.Id, targetRegionId);
        if (path is null)
        {
            Reject(
                intent,
                SimulationErrorCode.NoRoute,
                $"No link path from '{sourceRegion.Id}' to '{targetRegionId}'.",
                tick,
                events,
                results);
            return;
        }

        foreach (var link in path)
        {
            if (link.EnergyPerTick > 0 &&
                energyLinkUsage.GetValueOrDefault(link.Id) >= link.EnergyPerTick)
            {
                Reject(
                    intent,
                    SimulationErrorCode.NoBandwidth,
                    $"Link '{link.Id}' energy capacity is exhausted for this tick.",
                    tick,
                    events,
                    results);
                return;
            }
        }

        var route = string.Join(">", path.Select(link => link.Id));
        var shipmentId = World.NextEnergyShipmentId++;
        var arriveTick = tick + Math.Max(1, path.Sum(link => link.LatencyTicks));

        entity.Energy -= intent.Amount;
        foreach (var link in path)
        {
            if (link.EnergyPerTick > 0)
            {
                energyLinkUsage[link.Id] = energyLinkUsage.GetValueOrDefault(link.Id) + 1;
            }
        }

        World.InTransitEnergy.Add(new EnergyShipment(
            shipmentId,
            entity.Id,
            entity.CivilizationId,
            sourceRegion.Id,
            targetRegionId,
            route,
            intent.Amount,
            tick,
            arriveTick));

        World.Statistics.EnergyShippedTotal += intent.Amount;
        events.Add(new EnergyShipped(
            tick,
            shipmentId,
            entity.Id,
            sourceRegion.Id,
            targetRegionId,
            intent.Amount,
            arriveTick,
            route));

        Accept(
            intent,
            $"Energy shipment {shipmentId} queued: {intent.Amount} E arrives at tick {arriveTick}.",
            results);
    }

    private void ProcessDrawEnergy(
        EntityState entity,
        RegionState region,
        DrawEnergyIntent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results)
    {
        if (entity.Power is null)
        {
            Reject(intent, SimulationErrorCode.NoPower, "Entity has no power module.", tick, events, results);
            return;
        }

        if (intent.Amount <= 0)
        {
            Reject(intent, SimulationErrorCode.InvalidAction, "Energy amount must be positive.", tick, events, results);
            return;
        }

        if (entity.EnergyCapacity - entity.Energy < intent.Amount)
        {
            Reject(
                intent,
                SimulationErrorCode.InvalidAction,
                "Local storage does not have enough headroom.",
                tick,
                events,
                results);
            return;
        }

        if (IsBreakerOpen(region.Id, entity.CivilizationId))
        {
            AddPowerShortfall(
                entity,
                region.Id,
                intent.Amount,
                0,
                "blackout",
                tick,
                events);
            Reject(
                intent,
                SimulationErrorCode.NoEnergy,
                $"Region '{region.Id}' breaker is open.",
                tick,
                events,
                results);
            return;
        }

        var floor = DrawFloor(entity);
        var baseReserve = PowerReserve(entity.CivilizationId);
        var gridEnergy = region.GetEnergy(entity.CivilizationId);
        var availableAboveFloor = Math.Max(0, gridEnergy - floor);
        if (availableAboveFloor < intent.Amount ||
            !region.TryConsumeEnergy(entity.CivilizationId, intent.Amount))
        {
            var source = floor > baseReserve &&
                         gridEnergy < intent.Amount + floor
                ? "priority"
                : baseReserve > 0 &&
                  gridEnergy < intent.Amount + baseReserve
                    ? "reserve"
                    : "grid";
            AddPowerShortfall(
                entity,
                region.Id,
                intent.Amount,
                availableAboveFloor,
                source,
                tick,
                events);
            Reject(
                intent,
                SimulationErrorCode.NoEnergy,
                source is "priority" or "reserve"
                    ? $"Region '{region.Id}' grid reserve leaves insufficient energy."
                    : $"Region '{region.Id}' grid has insufficient energy.",
                tick,
                events,
                results);
            return;
        }

        entity.Energy += intent.Amount;
        World.Statistics.EnergyDrawnTotal += intent.Amount;
        events.Add(new EnergyDrawn(tick, entity.Id, region.Id, intent.Amount));
        Accept(intent, $"Drew {intent.Amount} E from the local grid.", results);
    }

    private void DeliverDueEnergyShipments(long tick, List<SimulationEvent> events)
    {
        if (World.InTransitEnergy.Count == 0)
        {
            return;
        }

        var due = World.InTransitEnergy
            .Where(shipment => shipment.ArriveTick <= tick)
            .OrderBy(shipment => shipment.ArriveTick)
            .ThenBy(shipment => shipment.Id)
            .ToList();

        foreach (var shipment in due)
        {
            World.InTransitEnergy.Remove(shipment);

            if (!World.Regions.TryGetValue(shipment.ToRegionId, out var destination))
            {
                continue;
            }

            var free = destination.GetGridFree(shipment.CivilizationId);
            var stored = free == long.MaxValue
                ? shipment.Amount
                : Math.Min(shipment.Amount, free);
            if (stored > 0)
            {
                destination.AddEnergy(shipment.CivilizationId, stored);
            }

            var wasted = shipment.Amount - stored;
            if (wasted > 0)
            {
                World.Statistics.EnergyWastedTotal += wasted;
                events.Add(new EnergyWasted(
                    tick,
                    shipment.CivilizationId,
                    shipment.ToRegionId,
                    wasted,
                    "arrival"));
            }

            World.Statistics.EnergyDeliveredTotal += shipment.Amount;
            events.Add(new EnergyArrived(
                tick,
                shipment.Id,
                shipment.ToRegionId,
                shipment.Amount));
        }
    }

    private void DeliverDuePowerShipments(long tick, List<SimulationEvent> events)
    {
        if (World.InTransitPower.Count == 0)
        {
            return;
        }

        var due = World.InTransitPower
            .Where(shipment => shipment.ArriveTick <= tick)
            .OrderBy(shipment => shipment.ArriveTick)
            .ThenBy(shipment => shipment.Id)
            .ToList();

        foreach (var shipment in due)
        {
            World.InTransitPower.Remove(shipment);
            var lost = shipment.Amount - shipment.DeliveredAmount;

            if (!World.Regions.TryGetValue(shipment.ToRegionId, out var destination))
            {
                World.Statistics.EnergyFlowLossTotal += shipment.Amount;
                events.Add(new PowerArrived(
                    tick,
                    shipment.Id,
                    shipment.CivilizationId,
                    shipment.ToRegionId,
                    0,
                    shipment.Amount));
                continue;
            }

            var free = destination.GetGridFree(shipment.CivilizationId);
            var stored = free == long.MaxValue
                ? shipment.DeliveredAmount
                : Math.Min(shipment.DeliveredAmount, free);
            if (stored > 0)
            {
                destination.AddEnergy(shipment.CivilizationId, stored);
            }

            var wasted = shipment.DeliveredAmount - stored;
            if (wasted > 0)
            {
                World.Statistics.EnergyWastedTotal += wasted;
                events.Add(new EnergyWasted(
                    tick,
                    shipment.CivilizationId,
                    shipment.ToRegionId,
                    wasted,
                    "arrival"));
            }

            World.Statistics.EnergyFlowedTotal += stored;
            World.Statistics.EnergyFlowLossTotal += lost;
            events.Add(new PowerArrived(
                tick,
                shipment.Id,
                shipment.CivilizationId,
                shipment.ToRegionId,
                stored,
                lost));
        }
    }

}
