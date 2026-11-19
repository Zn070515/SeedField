namespace SeedField.Simulation;

public static partial class WorldSaveSerializer
{
    private static void LoadEntity(
        WorldState world,
        RegionState region,
        EntitySaveData entityData)
    {
        if (entityData.Id <= 0)
        {
            throw Invalid("INVALID_ENTITY_ID", "Entity ids must be positive.");
        }

        var entityId = new EntityId(entityData.Id);
        if (world.FindEntity(entityId) is not null)
        {
            throw Invalid("DUPLICATE_ENTITY", $"Entity '{entityId}' appears twice.");
        }

        if (!region.Contains(new Vec2(entityData.X, entityData.Y)))
        {
            throw Invalid(
                "ENTITY_OUT_OF_BOUNDS",
                $"Entity '{entityId}' is outside region '{region.Id}'.");
        }

        if (entityData.MaxHealth <= 0 ||
            entityData.Health < 0 ||
            entityData.Health > entityData.MaxHealth)
        {
            throw Invalid("INVALID_HEALTH", $"Entity '{entityId}' has invalid health.");
        }

        if (entityData.Energy < 0 ||
            entityData.Energy > entityData.EnergyCapacity)
        {
            throw Invalid("INVALID_ENERGY", $"Entity '{entityId}' has energy outside capacity.");
        }

        ValidateEntityModules(entityId, entityData);

        var definition = new EntityDefinition(
            entityId,
            entityData.CivilizationId,
            entityData.RegionId,
            new Vec2(entityData.X, entityData.Y),
            entityData.MaxHealth,
            entityData.Energy,
            entityData.EnergyCapacity,
            entityData.Material,
            entityData.Sensor is null ? null : new SensorModule(entityData.Sensor.RangeMm),
            entityData.Weapon is null
                ? null
                : new WeaponModule(
                    entityData.Weapon.RangeMm,
                    entityData.Weapon.Damage,
                    entityData.Weapon.EnergyCost,
                    entityData.Weapon.CooldownTicks,
                    entityData.Weapon.Kind),
            entityData.Drive is null
                ? null
                : new DriveModule(
                    entityData.Drive.SpeedMmPerTick,
                    entityData.Drive.EnergyCostPerMove),
            entityData.Comm is null
                ? null
                : new CommModule(
                    entityData.Comm.LatencyTicks,
                    entityData.Comm.EnergyCostPerMessage,
                    entityData.Comm.BandwidthPerTick),
            entityData.BlueprintId,
            entityData.BlueprintVersion,
            entityData.ControllerProgramId,
            entityData.Power is null
                ? null
                : new PowerModule(
                    entityData.Power.EnergyPerTick,
                    entityData.Power.StorageCapacity,
                    entityData.Power.Priority),
            entityData.State is null
                ? null
                : new StateModule(
                    entityData.State.ComputePerTick,
                    entityData.State.StorageCapacity),
            entityData.IsCoreAnchor,
            entityData.Drill is null
                ? null
                : new DrillModule(
                    entityData.Drill.RatePerTick,
                    entityData.Drill.RangeMm,
                    entityData.Drill.EnergyCostPerTick),
            entityData.Armor is null
                ? null
                : new ArmorModule(entityData.Armor.FlatReduction),
            entityData.Shield is null
                ? null
                : new ShieldModule(
                    entityData.Shield.Capacity,
                    entityData.Shield.RegenPerTick,
                    entityData.Shield.RegenDelayTicks,
                    entityData.Shield.EnergyCostPerRegen),
            entityData.Smelter is null
                ? null
                : new SmelterModule(
                    entityData.Smelter.OrePerTick,
                    entityData.Smelter.EnergyCostPerTick,
                    entityData.Smelter.PreferredOre),
            entityData.Fabricator is null
                ? null
                : new FabricatorModule(
                    entityData.Fabricator.ComponentsPerTick,
                    entityData.Fabricator.MaterialPerComponent,
                    entityData.Fabricator.EnergyCostPerTick,
                    entityData.Fabricator.MaxComponents),
            entityData.Repair is null
                ? null
                : new RepairModule(
                    entityData.Repair.RangeMm,
                    entityData.Repair.HealthPerTick,
                    entityData.Repair.EnergyCostPerTick,
                    entityData.Repair.MaterialPerTick));

        var entity = new EntityState(definition)
        {
            Health = entityData.Health,
            NextWeaponReadyTick = entityData.NextWeaponReadyTick,
            ShieldEnergy = entityData.ShieldEnergy,
            ShieldRegenReadyTick = entityData.ShieldRegenReadyTick,
            ProductionEnabled = entityData.ProductionEnabled,
        };
        region.Entities.Add(entity.Id, entity);
    }

    private static void ValidateEntityModules(EntityId entityId, EntitySaveData entityData)
    {
        if (entityData.Drill is { RatePerTick: < 0 } or
            { RangeMm: < 1 } or
            { EnergyCostPerTick: < 0 })
        {
            throw Invalid(
                "INVALID_DRILL",
                $"Entity '{entityId}' has an invalid drill module.");
        }

        if (entityData.Smelter is { OrePerTick: < 0 } or
            { EnergyCostPerTick: < 0 } ||
            (entityData.Smelter?.PreferredOre is { } preferred &&
             !Enum.IsDefined(preferred)))
        {
            throw Invalid(
                "INVALID_SMELTER",
                $"Entity '{entityId}' has an invalid smelter module.");
        }

        if (entityData.Fabricator is { ComponentsPerTick: < 0 } or
            { MaterialPerComponent: < 1 } or
            { EnergyCostPerTick: < 0 } or
            { MaxComponents: < 0 })
        {
            throw Invalid(
                "INVALID_FABRICATOR",
                $"Entity '{entityId}' has an invalid fabricator module.");
        }

        if (entityData.Repair is { RangeMm: < 1 } or
            { HealthPerTick: < 1 } or
            { EnergyCostPerTick: < 0 } or
            { MaterialPerTick: < 0 })
        {
            throw Invalid(
                "INVALID_REPAIR",
                $"Entity '{entityId}' has an invalid repair module.");
        }

        if (entityData.Armor is { FlatReduction: < 0 })
        {
            throw Invalid(
                "INVALID_ARMOR",
                $"Entity '{entityId}' has negative armor.");
        }

        if (entityData.Shield is { Capacity: <= 0 } or
            { RegenPerTick: < 0 } or
            { RegenDelayTicks: < 0 } or
            { EnergyCostPerRegen: < 0 })
        {
            throw Invalid(
                "INVALID_SHIELD",
                $"Entity '{entityId}' has an invalid shield module.");
        }

        if (entityData.ShieldEnergy < 0 ||
            (entityData.Shield is null && entityData.ShieldEnergy != 0) ||
            (entityData.Shield is not null &&
             entityData.ShieldEnergy > entityData.Shield.Capacity))
        {
            throw Invalid(
                "INVALID_SHIELD",
                $"Entity '{entityId}' has invalid shield energy.");
        }
    }

    private static void LoadStock(RegionState region, StockSaveData stockData)
    {
        if (stockData.Material < 0 || stockData.Energy < 0 ||
            stockData.Ore < 0 || stockData.Components < 0 ||
            stockData.OreCopper < 0 || stockData.OreLead < 0 ||
            stockData.OreTitanium < 0 || stockData.OreCoal < 0)
        {
            throw Invalid(
                "INVALID_STOCK",
                $"Region '{region.Id}' has a negative stock.");
        }

        if (region.Stocks.ContainsKey(stockData.CivilizationId))
        {
            throw Invalid(
                "DUPLICATE_STOCK",
                $"Region '{region.Id}' has two stocks for '{stockData.CivilizationId}'.");
        }

        region.AddMaterial(stockData.CivilizationId, stockData.Material);
        region.AddEnergy(stockData.CivilizationId, stockData.Energy);
        if (stockData.OreCopper == 0 && stockData.OreLead == 0 &&
            stockData.OreTitanium == 0 && stockData.OreCoal == 0 &&
            stockData.Ore > 0)
        {
            // v24 and earlier had a single Ore pool; migrate it to copper.
            region.AddOre(stockData.CivilizationId, OreKind.Copper, stockData.Ore);
        }
        else
        {
            region.AddOre(stockData.CivilizationId, OreKind.Copper, stockData.OreCopper);
            region.AddOre(stockData.CivilizationId, OreKind.Lead, stockData.OreLead);
            region.AddOre(stockData.CivilizationId, OreKind.Titanium, stockData.OreTitanium);
            region.AddOre(stockData.CivilizationId, OreKind.Coal, stockData.OreCoal);
        }

        region.AddComponents(stockData.CivilizationId, stockData.Components);
    }
}
