namespace SeedField.Simulation;

/// <summary>
/// Materializes a blueprint version into world space. Cost enforcement belongs to the
/// economy layer; this service only creates or explicitly upgrades instances.
/// </summary>
public static class DeploymentService
{
    public static EntityState Deploy(
        WorldState world,
        DeviceBlueprint blueprint,
        string civilizationId,
        string regionId,
        Vec2 position)
    {
        Validate(world, blueprint, civilizationId, regionId, position);
        ConsumeMaterial(world, blueprint, civilizationId, regionId);
        return CreateInstance(world, blueprint, civilizationId, regionId, position);
    }

    public static void Validate(
        WorldState world,
        DeviceBlueprint blueprint,
        string civilizationId,
        string regionId,
        Vec2 position)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(blueprint);
        ArgumentException.ThrowIfNullOrEmpty(civilizationId);

        if (blueprint.Version <= 0)
        {
            throw new BlueprintValidationException(
                "UNPUBLISHED_BLUEPRINT",
                "Only a published blueprint version can be deployed.");
        }

        if (!world.Regions.TryGetValue(regionId, out var region))
        {
            throw new BlueprintValidationException("UNKNOWN_REGION", $"Region '{regionId}' does not exist.");
        }

        if (!region.Contains(position))
        {
            throw new BlueprintValidationException(
                "OUT_OF_BOUNDS",
                "Deployment position is outside the region.");
        }

        if (CoreProtection.IsProtectedFrom(world, regionId, civilizationId))
        {
            throw new BlueprintValidationException(
                "CORE_PROTECTED",
                $"Region '{regionId}' is a protected core region for another civilization.");
        }
    }

    public static long ConsumeMaterial(
        WorldState world,
        DeviceBlueprint blueprint,
        string civilizationId,
        string regionId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(blueprint);

        if (!world.Regions.TryGetValue(regionId, out var region))
        {
            throw new BlueprintValidationException("UNKNOWN_REGION", $"Region '{regionId}' does not exist.");
        }

        if (blueprint.MaterialCost > 0 &&
            !region.TryConsumeMaterial(civilizationId, blueprint.MaterialCost))
        {
            throw new BlueprintValidationException(
                "INSUFFICIENT_MATERIAL",
                $"Region '{regionId}' has {region.GetMaterial(civilizationId)} M; " +
                $"{blueprint.BlueprintId} v{blueprint.Version} needs {blueprint.MaterialCost}.");
        }

        world.Statistics.MaterialSpentTotal += blueprint.MaterialCost;
        return blueprint.MaterialCost;
    }

    public static EntityState CreateInstance(
        WorldState world,
        DeviceBlueprint blueprint,
        string civilizationId,
        string regionId,
        Vec2 position)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(blueprint);

        if (!world.Regions.TryGetValue(regionId, out var region))
        {
            throw new BlueprintValidationException("UNKNOWN_REGION", $"Region '{regionId}' does not exist.");
        }

        var definition = new EntityDefinition(
            new EntityId(world.NextEntityId++),
            civilizationId,
            regionId,
            position,
            blueprint.MaxHealth,
            Energy: blueprint.EnergyCapacity,
            EnergyCapacity: blueprint.EnergyCapacity,
            Material: 0,
            blueprint.Sensor,
            blueprint.Weapon,
            blueprint.Drive,
            blueprint.Comm,
            blueprint.BlueprintId,
            blueprint.Version,
            blueprint.ControllerProgramId,
            blueprint.Power,
            blueprint.State,
            blueprint.IsCoreAnchor);

        var entity = new EntityState(definition);
        region.Entities.Add(entity.Id, entity);
        CoreShellRules.RegisterAnchor(world, entity);
        return entity;
    }

    /// <summary>
    /// Explicit upgrade. Existing health/energy are preserved and clamped to the new
    /// version's limits; position, id and civilization never change.
    /// </summary>
    public static void Upgrade(
        WorldState world,
        EntityState entity,
        DeviceBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(blueprint);

        if (blueprint.Version <= 0)
        {
            throw new BlueprintValidationException(
                "UNPUBLISHED_BLUEPRINT",
                "Only a published blueprint version can be upgraded to.");
        }

        if (!string.Equals(entity.BlueprintId, blueprint.BlueprintId, StringComparison.Ordinal))
        {
            throw new BlueprintValidationException(
                "BLUEPRINT_MISMATCH",
                $"Entity '{entity.Id}' is an instance of '{entity.BlueprintId}', not '{blueprint.BlueprintId}'.");
        }

        entity.MaxHealth = blueprint.MaxHealth;
        entity.Health = Math.Min(entity.Health, blueprint.MaxHealth);
        entity.EnergyCapacity = blueprint.EnergyCapacity;
        entity.Energy = Math.Min(entity.Energy, blueprint.EnergyCapacity);
        entity.Sensor = blueprint.Sensor;
        entity.Weapon = blueprint.Weapon;
        entity.Drive = blueprint.Drive;
        entity.Comm = blueprint.Comm;
        entity.Power = blueprint.Power;
        entity.State = blueprint.State;
        entity.BlueprintVersion = blueprint.Version;
        entity.ControllerProgramId = blueprint.ControllerProgramId;
        entity.IsCoreAnchor = blueprint.IsCoreAnchor;
        CoreShellRules.RegisterAnchor(world, entity);
    }
}
