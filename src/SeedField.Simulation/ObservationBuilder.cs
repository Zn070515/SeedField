namespace SeedField.Simulation;

/// <summary>
/// Builds the filtered view a civilization may legally act on:
/// own entities are always known; foreign entities only appear when a living own
/// entity in the same region can sense them; messages appear only after delivery.
/// </summary>
public static class ObservationBuilder
{
    public static Observation Build(WorldState world, string civilizationId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrEmpty(civilizationId);

        var visible = new SortedDictionary<long, ObservedEntity>();

        foreach (var region in world.Regions.Values)
        {
            foreach (var own in region.Entities.Values)
            {
                if (!string.Equals(own.CivilizationId, civilizationId, StringComparison.Ordinal))
                {
                    continue;
                }

                visible[own.Id.Value] = ToObserved(own, isSelf: true);

                var sensorRange = own.Sensor?.RangeMm ?? 0;
                if (sensorRange <= 0)
                {
                    continue;
                }

                foreach (var candidate in region.Entities.Values)
                {
                    if (string.Equals(candidate.CivilizationId, civilizationId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!own.Position.IsWithinRange(candidate.Position, sensorRange))
                    {
                        continue;
                    }

                    visible.TryAdd(candidate.Id.Value, ToObserved(candidate, isSelf: false));
                }
            }
        }

        var messages = world.GetInbox(civilizationId)
            .OrderBy(message => message.Id)
            .Select(message => new ObservedMessage(
                message.Id,
                message.FromEntityId.Value,
                message.FromCivilizationId,
                message.TargetRegionId,
                message.Topic,
                message.Payload,
                message.SentTick,
                message.DeliveredTick))
            .ToList();

        var shipments = world.InTransitMaterial
            .Where(shipment => string.Equals(
                shipment.CivilizationId,
                civilizationId,
                StringComparison.Ordinal))
            .OrderBy(shipment => shipment.Id)
            .Select(shipment => new ObservedShipment(
                shipment.Id,
                shipment.FromRegionId,
                shipment.ToRegionId,
                shipment.Amount,
                shipment.SentTick,
                shipment.ArriveTick,
                shipment.Route))
            .ToList();

        var energyShipments = world.InTransitEnergy
            .Where(shipment => string.Equals(
                shipment.CivilizationId,
                civilizationId,
                StringComparison.Ordinal))
            .OrderBy(shipment => shipment.Id)
            .Select(shipment => new ObservedEnergyShipment(
                shipment.Id,
                shipment.FromRegionId,
                shipment.ToRegionId,
                shipment.Amount,
                shipment.SentTick,
                shipment.ArriveTick,
                shipment.Route))
            .ToList();

        return new Observation(
            world.Tick,
            civilizationId,
            visible.Values.ToList(),
            messages,
            shipments,
            energyShipments);
    }

    private static ObservedEntity ToObserved(EntityState entity, bool isSelf) =>
        new(
            entity.Id.Value,
            entity.CivilizationId,
            entity.RegionId,
            entity.Position,
            entity.Health,
            entity.MaxHealth,
            isSelf);
}
