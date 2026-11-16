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

                    if (!VisibilityRules.IsSensedBy(own, candidate))
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
            energyShipments,
            ControlPoints: world.ControlPoints.Values
                .Select(ToObservedControlPoint)
                .ToList(),
            Match: ToObservedMatch(world.Match));
    }

    /// <summary>
    /// Device-scoped observation. Only entities in the viewer's region are known; other
    /// own units in that region are friendly, foreign units require this device's sensor.
    /// Messages and shipments are filtered to the device's region, so knowledge does not
    /// fuse across the civilization for free.
    /// </summary>
    public static Observation BuildForEntity(WorldState world, EntityId entityId)
    {
        ArgumentNullException.ThrowIfNull(world);

        var viewer = world.FindEntity(entityId)
            ?? throw new ArgumentException($"Entity '{entityId}' does not exist.", nameof(entityId));
        var region = world.Regions[viewer.RegionId];

        var visible = new SortedDictionary<long, ObservedEntity>();
        foreach (var candidate in region.Entities.Values)
        {
            if (candidate.Id != viewer.Id &&
                !string.Equals(candidate.CivilizationId, viewer.CivilizationId, StringComparison.Ordinal) &&
                !VisibilityRules.IsSensedBy(viewer, candidate))
            {
                continue;
            }

            visible[candidate.Id.Value] = ToObserved(
                candidate,
                isSelf: string.Equals(
                    candidate.CivilizationId,
                    viewer.CivilizationId,
                    StringComparison.Ordinal));
        }

        var messages = world.GetInbox(viewer.CivilizationId)
            .Where(message => string.Equals(
                message.TargetRegionId,
                viewer.RegionId,
                StringComparison.Ordinal))
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
            .Where(shipment =>
                string.Equals(shipment.CivilizationId, viewer.CivilizationId, StringComparison.Ordinal) &&
                (string.Equals(shipment.FromRegionId, viewer.RegionId, StringComparison.Ordinal) ||
                 string.Equals(shipment.ToRegionId, viewer.RegionId, StringComparison.Ordinal)))
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
            .Where(shipment =>
                string.Equals(shipment.CivilizationId, viewer.CivilizationId, StringComparison.Ordinal) &&
                (string.Equals(shipment.FromRegionId, viewer.RegionId, StringComparison.Ordinal) ||
                 string.Equals(shipment.ToRegionId, viewer.RegionId, StringComparison.Ordinal)))
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

        var controlPoints = world.ControlPoints.Values
            .Where(point => string.Equals(
                point.RegionId,
                viewer.RegionId,
                StringComparison.Ordinal))
            .Select(ToObservedControlPoint)
            .ToList();

        return new Observation(
            world.Tick,
            viewer.CivilizationId,
            visible.Values.ToList(),
            messages,
            shipments,
            energyShipments,
            viewer.Id.Value,
            controlPoints,
            ToObservedMatch(world.Match));
    }

    private static ObservedControlPoint ToObservedControlPoint(ControlPointState point) =>
        new(
            point.Id,
            point.RegionId,
            point.Position,
            point.RadiusMm,
            point.HoldTicksRequired,
            point.HoldingCivilizationId,
            point.HoldTicks,
            point.OwnerCivilizationId);

    private static ObservedMatch? ToObservedMatch(MatchState? match) =>
        match is null
            ? null
            : new ObservedMatch(
                match.Rules.CapturedPointsToWin,
                match.WinnerCivilizationId);

    private static ObservedEntity ToObserved(EntityState entity, bool isSelf) =>
        new(
            entity.Id.Value,
            entity.CivilizationId,
            entity.RegionId,
            entity.Position,
            entity.Health,
            entity.MaxHealth,
            isSelf,
            entity.Sensor is not null,
            entity.Sensor?.RangeMm ?? 0,
            entity.Weapon is not null,
            entity.Weapon?.RangeMm ?? 0,
            entity.Drive is not null,
            entity.Comm is not null);
}
