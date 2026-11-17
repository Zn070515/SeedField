namespace SeedField.Simulation;

/// <summary>
/// Per-tick read-only observation cache. Rebuilt whenever the world tick changes;
/// device observations reuse the same own-entity DTOs and spatial index.
/// </summary>
internal sealed class ObservationCache
{
    private readonly Dictionary<string, RegionState> _regions =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, RegionSpatialIndex> _indices =
        new(StringComparer.Ordinal);
    private readonly Dictionary<(string Region, string Civ), List<ObservedEntity>> _own =
        new();
    private readonly Dictionary<(long EntityId, string Civ), ObservedEntity> _observed =
        new();
    private readonly Dictionary<string, List<ObservedMessage>> _messages = new();
    private readonly Dictionary<string, List<ObservedShipment>> _shipments = new();
    private readonly Dictionary<string, List<ObservedEnergyShipment>> _energyShipments = new();
    private readonly Dictionary<string, List<ObservedControlPoint>> _controlPoints = new();
    private readonly Dictionary<string, List<ObservedTreaty>> _treaties = new();
    private readonly Dictionary<string, List<ObservedTreatyProposal>> _proposals = new();
    private readonly Dictionary<string, List<ObservedTradeOffer>> _tradeOffers = new();
    private readonly WorldState _world;

    private ObservationCache(WorldState world)
    {
        _world = world;
        Tick = world.Tick;
        foreach (var region in world.Regions.Values)
        {
            _regions[region.Id] = region;
        }
    }

    public long Tick { get; }

    public static ObservationCache Build(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return new ObservationCache(world);
    }

    /// <summary>
    /// Lazily builds the index for one region. Observations only query the viewer's
    /// region, so indexing every region up front would charge the world size to a
    /// single-region read.
    /// </summary>
    public RegionSpatialIndex Index(string regionId)
    {
        if (_indices.TryGetValue(regionId, out var index))
        {
            return index;
        }

        if (!_regions.TryGetValue(regionId, out var region))
        {
            throw new KeyNotFoundException($"Region '{regionId}' is not in this observation cache.");
        }

        index = RegionSpatialIndex.Build(region);
        _indices[regionId] = index;
        return index;
    }

    public IReadOnlyList<ObservedEntity> Own(string regionId, string civilizationId)
    {
        if (_own.TryGetValue((regionId, civilizationId), out var cached))
        {
            return cached;
        }

        if (!_regions.TryGetValue(regionId, out var worldRegion))
        {
            throw new KeyNotFoundException($"Region '{regionId}' is not in this observation cache.");
        }

        var list = new List<ObservedEntity>();
        foreach (var entity in worldRegion.Entities.Values)
        {
            if (string.Equals(
                    entity.CivilizationId,
                    civilizationId,
                    StringComparison.Ordinal))
            {
                list.Add(Observe(entity, civilizationId, isSelf: true));
            }
        }

        _own[(regionId, civilizationId)] = list;
        return list;
    }

    public ObservedEntity Observe(
        EntityState entity,
        string civilizationId,
        bool isSelf)
    {
        var key = (entity.Id.Value, civilizationId);
        if (_observed.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var observed = ObservationBuilder.ToObserved(entity, isSelf);
        _observed[key] = observed;
        return observed;
    }

    public IReadOnlyList<ObservedMessage> Messages(string regionId, string civilizationId) =>
        Cached(_messages, Key(regionId, civilizationId), () => _world
            .GetInbox(civilizationId)
            .Where(message => string.Equals(
                message.TargetRegionId,
                regionId,
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
            .ToList());

    public IReadOnlyList<ObservedShipment> Shipments(
        string regionId,
        string civilizationId) =>
        Cached(_shipments, Key(regionId, civilizationId), () => _world
            .InTransitMaterial
            .Where(shipment =>
                string.Equals(
                    shipment.CivilizationId,
                    civilizationId,
                    StringComparison.Ordinal) &&
                (string.Equals(shipment.FromRegionId, regionId, StringComparison.Ordinal) ||
                 string.Equals(shipment.ToRegionId, regionId, StringComparison.Ordinal)))
            .OrderBy(shipment => shipment.Id)
            .Select(shipment => new ObservedShipment(
                shipment.Id,
                shipment.FromRegionId,
                shipment.ToRegionId,
                shipment.Amount,
                shipment.SentTick,
                shipment.ArriveTick,
                shipment.Route))
            .ToList());

    public IReadOnlyList<ObservedEnergyShipment> EnergyShipments(
        string regionId,
        string civilizationId) =>
        Cached(_energyShipments, Key(regionId, civilizationId), () => _world
            .InTransitEnergy
            .Where(shipment =>
                string.Equals(
                    shipment.CivilizationId,
                    civilizationId,
                    StringComparison.Ordinal) &&
                (string.Equals(shipment.FromRegionId, regionId, StringComparison.Ordinal) ||
                 string.Equals(shipment.ToRegionId, regionId, StringComparison.Ordinal)))
            .OrderBy(shipment => shipment.Id)
            .Select(shipment => new ObservedEnergyShipment(
                shipment.Id,
                shipment.FromRegionId,
                shipment.ToRegionId,
                shipment.Amount,
                shipment.SentTick,
                shipment.ArriveTick,
                shipment.Route))
            .ToList());

    public IReadOnlyList<ObservedControlPoint> ControlPoints(string regionId) =>
        Cached(_controlPoints, Key(regionId, string.Empty), () => _world.ControlPoints.Values
            .Where(point => string.Equals(
                point.RegionId,
                regionId,
                StringComparison.Ordinal))
            .Select(ObservationBuilder.ToObservedControlPoint)
            .ToList());

    public IReadOnlyList<ObservedTreaty> Treaties(string civilizationId) =>
        Cached(_treaties, Key(string.Empty, civilizationId), () => _world.Treaties.Values
            .Where(treaty => treaty.Involves(civilizationId))
            .OrderBy(treaty => treaty.Id)
            .Select(treaty => ObservationBuilder.ToObservedTreaty(treaty, civilizationId))
            .ToList());

    public IReadOnlyList<ObservedTreatyProposal> Proposals(string civilizationId) =>
        Cached(_proposals, Key(string.Empty, civilizationId), () =>
            _world.TreatyProposals.Values
                .Where(proposal => proposal.Involves(civilizationId))
                .OrderBy(proposal => proposal.Id)
                .Select(ObservationBuilder.ToObservedTreatyProposal)
                .ToList());

    public IReadOnlyList<ObservedTradeOffer> TradeOffers(
        string regionId,
        string civilizationId) =>
        Cached(_tradeOffers, Key(regionId, civilizationId), () =>
            _world.TradeOffers.Values
                .Where(offer =>
                    offer.Involves(civilizationId) &&
                    string.Equals(offer.RegionId, regionId, StringComparison.Ordinal))
                .OrderBy(offer => offer.Id)
                .Select(ObservationBuilder.ToObservedTradeOffer)
                .ToList());

    private static List<T> Cached<T>(
        Dictionary<string, List<T>> cache,
        string key,
        Func<List<T>> build)
    {
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var value = build();
        cache[key] = value;
        return value;
    }

    private static string Key(string regionId, string civilizationId) =>
        regionId + "\0" + civilizationId;
}
