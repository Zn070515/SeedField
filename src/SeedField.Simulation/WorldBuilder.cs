namespace SeedField.Simulation;

/// <summary>
/// Explicit construction helper for deterministic scenarios. It performs no balancing
/// decisions: every number comes from the caller.
/// </summary>
public sealed class WorldBuilder
{
    private readonly WorldState _world = new();

    public WorldBuilder SetRandomSeed(ulong seed)
    {
        _world.Random.Reset(seed);
        return this;
    }

    public WorldBuilder SetWreckRecoveryPercent(int percent)
    {
        if (percent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percent),
                "Wreck recovery percent must be within 0..100.");
        }

        _world.WreckRecoveryPercent = percent;
        return this;
    }

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

    public WorldBuilder AddLink(
        string id,
        string regionA,
        string regionB,
        int latencyTicks,
        int messagesPerTick = 0,
        int materialPerTick = 0,
        int energyPerTick = 0,
        int powerPerTick = 0,
        int powerLatencyTicks = 0,
        int powerLossPercent = 0)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Link id is required.", nameof(id));
        }

        if (_world.Links.ContainsKey(id))
        {
            throw new InvalidOperationException($"Link '{id}' already exists.");
        }

        if (!_world.Regions.ContainsKey(regionA) || !_world.Regions.ContainsKey(regionB))
        {
            throw new InvalidOperationException("Both link regions must exist.");
        }

        if (string.Equals(regionA, regionB, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A region cannot link to itself.");
        }

        if (latencyTicks < 0 ||
            messagesPerTick < 0 ||
            materialPerTick < 0 ||
            energyPerTick < 0 ||
            powerPerTick < 0 ||
            powerLatencyTicks < 0 ||
            powerLossPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(latencyTicks), "Link values must be non-negative.");
        }

        _world.Links.Add(
            id,
            new RegionLink(
                id,
                regionA,
                regionB,
                latencyTicks,
                messagesPerTick,
                materialPerTick,
                energyPerTick,
                powerPerTick,
                powerLatencyTicks,
                powerLossPercent));
        return this;
    }

    public WorldBuilder AddMessagePolicy(
        string civilizationId,
        bool allowAllInbound,
        IEnumerable<string>? blockedCivilizations = null,
        IEnumerable<string>? blockedTopics = null,
        IEnumerable<string>? allowedCivilizations = null,
        bool allowAllTopics = true,
        IEnumerable<string>? allowedTopics = null)
    {
        if (_world.MessagePolicies.ContainsKey(civilizationId))
        {
            throw new InvalidOperationException($"Message policy for '{civilizationId}' already exists.");
        }

        var policy = new MessagePolicyState(civilizationId, allowAllInbound)
        {
            AllowAllTopics = allowAllTopics,
        };
        foreach (var blocked in blockedCivilizations ?? Array.Empty<string>())
        {
            policy.BlockedCivilizations.Add(blocked);
        }

        foreach (var blocked in blockedTopics ?? Array.Empty<string>())
        {
            policy.BlockedTopics.Add(blocked);
        }

        foreach (var allowed in allowedCivilizations ?? Array.Empty<string>())
        {
            policy.AllowedCivilizations.Add(allowed);
        }

        foreach (var allowed in allowedTopics ?? Array.Empty<string>())
        {
            policy.AllowedTopics.Add(allowed);
        }

        _world.MessagePolicies.Add(civilizationId, policy);
        return this;
    }

    public WorldBuilder SetPowerPolicy(
        string civilizationId,
        long reserveEnergy,
        int highPriorityThreshold = 0,
        long priorityReserveEnergy = 0,
        bool autoReconnect = true,
        int breakerTripLimit = 0,
        long breakerMaintenanceMaterialCost = 0,
        int breakerFaultChancePercent = 0)
    {
        if (string.IsNullOrWhiteSpace(civilizationId))
        {
            throw new ArgumentException("Civilization id is required.", nameof(civilizationId));
        }

        if (reserveEnergy < 0 ||
            highPriorityThreshold < 0 ||
            priorityReserveEnergy < 0 ||
            breakerTripLimit < 0 ||
            breakerMaintenanceMaterialCost < 0 ||
            breakerFaultChancePercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reserveEnergy),
                "Power policy values must be non-negative; fault chance must be 0..100.");
        }

        if (_world.PowerPolicies.ContainsKey(civilizationId))
        {
            throw new InvalidOperationException(
                $"Power policy for '{civilizationId}' already exists.");
        }

        _world.PowerPolicies.Add(
            civilizationId,
            new PowerPolicyState(
                civilizationId,
                reserveEnergy,
                highPriorityThreshold,
                priorityReserveEnergy,
                autoReconnect,
                breakerTripLimit,
                breakerMaintenanceMaterialCost,
                breakerFaultChancePercent));
        return this;
    }

    public WorldBuilder SetGridCapacity(
        string regionId,
        string civilizationId,
        long capacity)
    {
        if (!_world.Regions.TryGetValue(regionId, out var region))
        {
            throw new InvalidOperationException($"Unknown region '{regionId}'.");
        }

        if (string.IsNullOrWhiteSpace(civilizationId))
        {
            throw new ArgumentException(
                "Civilization id is required.",
                nameof(civilizationId));
        }

        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "Grid capacity must be non-negative.");
        }

        if (region.GridCapacities.ContainsKey(civilizationId))
        {
            throw new InvalidOperationException(
                $"Grid capacity for '{civilizationId}' already exists in '{regionId}'.");
        }

        region.GridCapacities.Add(civilizationId, capacity);
        return this;
    }

    public WorldBuilder AddControlPoint(
        string id,
        string regionId,
        Vec2 position,
        int radiusMm,
        int holdTicksRequired)
    {
        if (!_world.Regions.TryGetValue(regionId, out var region))
        {
            throw new InvalidOperationException($"Unknown region '{regionId}'.");
        }

        if (!region.Contains(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Control point is outside the region.");
        }

        if (_world.ControlPoints.ContainsKey(id))
        {
            throw new InvalidOperationException($"Control point '{id}' already exists.");
        }

        _world.ControlPoints.Add(
            id,
            new ControlPointState(
                id,
                regionId,
                position,
                radiusMm,
                holdTicksRequired));
        return this;
    }

    public WorldBuilder SetVictoryCondition(int capturedPointsToWin)
    {
        _world.Match = new MatchState(new MatchRules(capturedPointsToWin));
        return this;
    }

    public WorldBuilder AddStock(string regionId, string civilizationId, long material)
    {
        if (!_world.Regions.TryGetValue(regionId, out var region))
        {
            throw new InvalidOperationException($"Unknown region '{regionId}'.");
        }

        region.AddMaterial(civilizationId, material);
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

        foreach (var core in _world.Cores.Values)
        {
            core.AnchorTarget = Math.Max(
                core.AnchorTarget,
                CoreShellRules.CountAliveAnchors(_world, core.CivilizationId));
        }

        return _world;
    }
}
