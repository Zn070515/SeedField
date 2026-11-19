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

        var messages = MessageWindow(world.GetInbox(civilizationId), regionId: null);

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
            Match: ToObservedMatch(world.Match),
            Treaties: world.Treaties.Values
                .Where(treaty => treaty.Involves(civilizationId))
                .OrderBy(treaty => treaty.Id)
                .Select(treaty => ToObservedTreaty(treaty, civilizationId))
                .ToList(),
            TreatyProposals: world.TreatyProposals.Values
                .Where(proposal => proposal.Involves(civilizationId))
                .OrderBy(proposal => proposal.Id)
                .Select(ToObservedTreatyProposal)
                .ToList(),
            TradeOffers: world.TradeOffers.Values
                .Where(offer => offer.Involves(civilizationId))
                .OrderBy(offer => offer.Id)
                .Select(ToObservedTradeOffer)
                .ToList(),
            Deposits: world.Deposits.Values
                .Where(deposit => world.IsDepositDiscovered(civilizationId, deposit.Id))
                .Select(ToObserved)
                .ToList(),
            Material: world.Regions.Values.Sum(region => region.GetMaterial(civilizationId)),
            Ore: world.Regions.Values.Sum(region => region.GetOre(civilizationId)),
            Components: world.Regions.Values.Sum(region => region.GetComponents(civilizationId)),
            OreCopper: world.Regions.Values.Sum(region => region.GetOre(civilizationId, OreKind.Copper)),
            OreLead: world.Regions.Values.Sum(region => region.GetOre(civilizationId, OreKind.Lead)),
            OreTitanium: world.Regions.Values.Sum(region => region.GetOre(civilizationId, OreKind.Titanium)),
            OreCoal: world.Regions.Values.Sum(region => region.GetOre(civilizationId, OreKind.Coal)));
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
        return BuildForEntity(world, entityId, ObservationCache.Build(world));
    }

    internal static Observation BuildForEntity(
        WorldState world,
        EntityId entityId,
        ObservationCache cache,
        bool includeAllies = true)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cache);

        var viewer = world.FindEntity(entityId)
            ?? throw new ArgumentException($"Entity '{entityId}' does not exist.", nameof(entityId));
        var region = world.Regions[viewer.RegionId];

        // One visibility path: own entities come from the shared per-tick cache, and
        // foreign entities are resolved through the region index. Keeping a second
        // full-scan implementation here invited the two rules to drift apart.
        var visible = includeAllies
            ? new List<ObservedEntity>(cache.Own(region.Id, viewer.CivilizationId))
            : new List<ObservedEntity>
            {
                cache.Observe(viewer, viewer.CivilizationId, isSelf: true),
            };
        if (viewer.Sensor is { RangeMm: > 0 })
        {
            var candidates = new List<EntityState>();
            cache.Index(region.Id).Query(
                viewer.Position,
                viewer.Sensor.RangeMm,
                candidates,
                viewer.CivilizationId);
            foreach (var candidate in candidates)
            {
                if (candidate.Id == viewer.Id ||
                    string.Equals(
                        candidate.CivilizationId,
                        viewer.CivilizationId,
                        StringComparison.Ordinal) ||
                    !VisibilityRules.IsSensedBy(viewer, candidate))
                {
                    continue;
                }

                visible.Add(cache.Observe(
                    candidate,
                    viewer.CivilizationId,
                    isSelf: false));
            }

            visible.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        var messages = cache.Messages(viewer.RegionId, viewer.CivilizationId);
        var shipments = cache.Shipments(viewer.RegionId, viewer.CivilizationId);
        var energyShipments = cache.EnergyShipments(
            viewer.RegionId,
            viewer.CivilizationId);
        var controlPoints = cache.ControlPoints(viewer.RegionId);
        var treaties = cache.Treaties(viewer.CivilizationId);
        var proposals = cache.Proposals(viewer.CivilizationId);
        var tradeOffers = cache.TradeOffers(viewer.RegionId, viewer.CivilizationId);
        var sightRange = Math.Max(
            viewer.Sensor?.RangeMm ?? 0,
            viewer.Drill?.RangeMm ?? 0);
        var deposits = sightRange <= 0
            ? new List<ObservedDeposit>()
            : world.Deposits.Values
                .Where(deposit =>
                    string.Equals(deposit.RegionId, viewer.RegionId, StringComparison.Ordinal) &&
                    viewer.Position.IsWithinRange(deposit.Position, sightRange) &&
                    world.IsDepositDiscovered(viewer.CivilizationId, deposit.Id))
                .Select(ToObserved)
                .ToList();

        return new Observation(
            world.Tick,
            viewer.CivilizationId,
            visible,
            messages,
            shipments,
            energyShipments,
            viewer.Id.Value,
            controlPoints,
            ToObservedMatch(world.Match),
            Treaties: treaties,
            TreatyProposals: proposals,
            TradeOffers: tradeOffers,
            Deposits: deposits,
            Material: region.GetMaterial(viewer.CivilizationId),
            Ore: region.GetOre(viewer.CivilizationId),
            Components: region.GetComponents(viewer.CivilizationId),
            OreCopper: region.GetOre(viewer.CivilizationId, OreKind.Copper),
            OreLead: region.GetOre(viewer.CivilizationId, OreKind.Lead),
            OreTitanium: region.GetOre(viewer.CivilizationId, OreKind.Titanium),
            OreCoal: region.GetOre(viewer.CivilizationId, OreKind.Coal));
    }

    /// <summary>
    /// Takes the last N messages in delivery order without materializing/sorting the
    /// whole inbox. A fleet-sized inbox can hold tens of thousands of messages; the
    /// previous OrderBy+ToList allocated that list for every observation tick.
    /// </summary>
    internal static List<ObservedMessage> MessageWindow(
        IReadOnlyList<DeliveredMessage> inbox,
        string? regionId)
    {
        var matching = 0;
        foreach (var message in inbox)
        {
            if (regionId is null ||
                string.Equals(
                    message.TargetRegionId,
                    regionId,
                    StringComparison.Ordinal))
            {
                matching++;
            }
        }

        var skip = Math.Max(0, matching - ObservationLimits.MaxMessages);
        var result = new List<ObservedMessage>(Math.Min(matching, ObservationLimits.MaxMessages));
        var seen = 0;
        foreach (var message in inbox)
        {
            if (regionId is not null &&
                !string.Equals(
                    message.TargetRegionId,
                    regionId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (seen++ < skip)
            {
                continue;
            }

            result.Add(new ObservedMessage(
                message.Id,
                message.FromEntityId.Value,
                message.FromCivilizationId,
                message.TargetRegionId,
                message.Topic,
                message.Payload,
                message.SentTick,
                message.DeliveredTick));
        }

        return result;
    }

    internal static ObservedControlPoint ToObservedControlPoint(ControlPointState point) =>
        new(
            point.Id,
            point.RegionId,
            point.Position,
            point.RadiusMm,
            point.HoldTicksRequired,
            point.HoldingCivilizationId,
            point.HoldTicks,
            point.OwnerCivilizationId);

    internal static ObservedTreaty ToObservedTreaty(
        TreatyState treaty,
        string civilizationId) =>
        new(
            treaty.Id,
            treaty.Kind,
            treaty.Other(civilizationId),
            treaty.SignedTick);

    internal static ObservedTreatyProposal ToObservedTreatyProposal(
        TreatyProposalState proposal) =>
        new(
            proposal.Id,
            proposal.Kind,
            proposal.ProposerCivilizationId,
            proposal.TargetCivilizationId,
            proposal.ProposedTick,
            proposal.ExpiresTick);

    internal static ObservedTradeOffer ToObservedTradeOffer(TradeOfferState offer) =>
        new(
            offer.Id,
            offer.ProposerCivilizationId,
            offer.TargetCivilizationId,
            offer.RegionId,
            offer.GiveMaterial,
            offer.GiveEnergy,
            offer.WantMaterial,
            offer.WantEnergy,
            offer.ProposedTick,
            offer.ExpiresTick,
            offer.NegotiationId,
            offer.Round);

    private static ObservedMatch? ToObservedMatch(MatchState? match) =>
        match is null
            ? null
            : new ObservedMatch(
                match.Rules.CapturedPointsToWin,
                match.WinnerCivilizationId);

    internal static ObservedEntity ToObserved(EntityState entity, bool isSelf) =>
        new(
            entity.Id.Value,
            entity.CivilizationId,
            entity.RegionId,
            entity.Position,
            entity.Health,
            entity.MaxHealth,
            isSelf,
            entity.Sensor is not null && !entity.IsCritical,
            entity.Sensor?.RangeMm ?? 0,
            entity.Weapon is not null && !entity.IsCritical,
            entity.Weapon?.RangeMm ?? 0,
            entity.Drive is not null && !entity.IsCritical,
            entity.Comm is not null,
            entity.Drill is not null,
            entity.Drill?.RangeMm ?? 0,
            entity.Armor?.FlatReduction ?? 0,
            entity.ShieldEnergy,
            entity.Shield?.Capacity ?? 0,
            (entity.Weapon?.Kind ?? WeaponKind.Direct).ToString(),
            entity.Smelter is not null,
            entity.Fabricator is not null,
            entity.ProductionEnabled,
            entity.Repair is not null,
            entity.Repair?.RangeMm ?? 0,
            entity.Repair?.HealthPerTick ?? 0);

    internal static ObservedDeposit ToObserved(ResourceDepositState deposit) =>
        new(
            deposit.Id,
            deposit.RegionId,
            deposit.Kind,
            deposit.Position,
            deposit.RadiusMm,
            deposit.Remaining,
            deposit.YieldPerTick);
}
