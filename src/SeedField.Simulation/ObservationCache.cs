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

    private ObservationCache(WorldState world)
    {
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

}
