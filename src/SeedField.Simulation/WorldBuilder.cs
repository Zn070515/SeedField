namespace SeedField.Simulation;

/// <summary>
/// Explicit construction helper for deterministic scenarios. It performs no balancing
/// decisions: every number comes from the caller.
/// </summary>
public sealed class WorldBuilder
{
    private readonly WorldState _world = new();

    public WorldBuilder AddRegion(string id, int widthMm, int heightMm, bool isCoreRegion = false)
    {
        if (_world.Regions.ContainsKey(id))
        {
            throw new InvalidOperationException($"Region '{id}' already exists.");
        }

        _world.Regions.Add(id, new RegionState(id, widthMm, heightMm, isCoreRegion));
        return this;
    }

    public WorldBuilder AddCore(string civilizationId, string regionId, SeedCoreConfig config)
    {
        if (_world.Cores.ContainsKey(civilizationId))
        {
            throw new InvalidOperationException($"Core for '{civilizationId}' already exists.");
        }

        if (!_world.Regions.TryGetValue(regionId, out var region))
        {
            throw new InvalidOperationException($"Unknown region '{regionId}'.");
        }

        if (!region.IsCoreRegion)
        {
            throw new InvalidOperationException($"Region '{regionId}' is not marked as a core region.");
        }

        _world.Cores.Add(civilizationId, new SeedCoreState(civilizationId, regionId, config));
        return this;
    }

    public WorldBuilder AddEntity(EntityDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!_world.Regions.TryGetValue(definition.RegionId, out var region))
        {
            throw new InvalidOperationException($"Unknown region '{definition.RegionId}'.");
        }

        if (region.Entities.ContainsKey(definition.Id))
        {
            throw new InvalidOperationException($"Entity '{definition.Id}' already exists.");
        }

        if (!region.Contains(definition.Position))
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                $"Entity '{definition.Id}' starts outside region '{region.Id}'.");
        }

        if (definition.MaxHealth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition), "MaxHealth must be positive.");
        }

        if (definition.Energy < 0 || definition.Energy > definition.EnergyCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(definition), "Energy must be within capacity.");
        }

        if (definition.Id.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition), "Entity ids start at 1.");
        }

        region.Entities.Add(definition.Id, new EntityState(definition));
        _world.NextEntityId = Math.Max(_world.NextEntityId, definition.Id.Value + 1);
        return this;
    }

    public WorldState Build()
    {
        if (_world.Regions.Count == 0)
        {
            throw new InvalidOperationException("A world needs at least one region.");
        }

        return _world;
    }
}
