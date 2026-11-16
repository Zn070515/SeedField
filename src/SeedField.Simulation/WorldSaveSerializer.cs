using System.Text.Json;

namespace SeedField.Simulation;

/// <summary>
/// JSON persistence for the authoritative world. Loading rebuilds a brand-new
/// WorldState and validates every structural invariant before returning it.
/// </summary>
public static class WorldSaveSerializer
{
    public const int CurrentSchemaVersion = 1;
    public const string GameVersion = "0.1.0-m0";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string Save(WorldState world) =>
        JsonSerializer.Serialize(ToData(world), Options);

    public static WorldState Load(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new SaveFormatException("EMPTY_SAVE", "Save text is empty.");
        }

        WorldSaveData? data;
        try
        {
            data = JsonSerializer.Deserialize<WorldSaveData>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new SaveFormatException("INVALID_SAVE", exception.Message, exception);
        }

        if (data is null)
        {
            throw new SaveFormatException("INVALID_SAVE", "Save payload is null.");
        }

        return FromData(data);
    }

    public static WorldSaveData ToData(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);

        return new WorldSaveData(
            CurrentSchemaVersion,
            GameVersion,
            world.Tick,
            world.NextEntityId,
            world.NextMessageId,
            world.Regions.Values.Select(ToRegion).ToArray(),
            world.Cores.Values.Select(ToCore).ToArray(),
            world.InFlightMessages
                .OrderBy(message => message.Id)
                .Select(ToMessage)
                .ToArray(),
            world.Inboxes
                .Select(inbox => new InboxSaveData(
                    inbox.Key,
                    inbox.Value
                        .OrderBy(message => message.Id)
                        .Select(ToDeliveredMessage)
                        .ToArray()))
                .ToArray(),
            new StatisticsSaveData(
                world.Statistics.EnergySpentTotal,
                world.Statistics.EnergyRegeneratedTotal,
                world.Statistics.EnergyGeneratedTotal,
                world.Statistics.MaterialSpentTotal,
                world.Statistics.MaterialRegeneratedTotal,
                world.Statistics.ShotsFired,
                world.Statistics.DamageDealt,
                world.Statistics.EntitiesDestroyed,
                world.Statistics.MessagesDelivered,
                world.Statistics.MessagesExpired),
            world.Workshop.GetAllVersions().Select(ToBlueprintSave).ToArray(),
            world.Links.Values
                .Select(link => new LinkSaveData(
                    link.Id,
                    link.RegionA,
                    link.RegionB,
                    link.LatencyTicks,
                    link.MessagesPerTick))
                .ToArray());
    }

    public static WorldState FromData(WorldSaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.SchemaVersion != CurrentSchemaVersion)
        {
            throw new SaveFormatException(
                "UNSUPPORTED_SAVE_VERSION",
                $"Save schema {data.SchemaVersion} is not supported; expected {CurrentSchemaVersion}.");
        }

        var world = new WorldState
        {
            Tick = data.Tick,
            NextEntityId = data.NextEntityId,
            NextMessageId = data.NextMessageId,
        };

        foreach (var regionData in data.Regions)
        {
            if (world.Regions.ContainsKey(regionData.Id))
            {
                throw Invalid("DUPLICATE_REGION", $"Region '{regionData.Id}' appears twice.");
            }

            var region = new RegionState(
                regionData.Id,
                regionData.WidthMm,
                regionData.HeightMm,
                regionData.IsCoreRegion);
            world.Regions.Add(region.Id, region);

            foreach (var entityData in regionData.Entities)
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
                            entityData.Weapon.CooldownTicks),
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
                            entityData.Power.StorageCapacity),
                    entityData.State is null
                        ? null
                        : new StateModule(
                            entityData.State.ComputePerTick,
                            entityData.State.StorageCapacity));

                var entity = new EntityState(definition)
                {
                    Health = entityData.Health,
                    NextWeaponReadyTick = entityData.NextWeaponReadyTick,
                };
                region.Entities.Add(entity.Id, entity);
            }

            foreach (var stockData in regionData.Stocks ?? Array.Empty<StockSaveData>())
            {
                if (stockData.Material < 0)
                {
                    throw Invalid(
                        "INVALID_STOCK",
                        $"Region '{region.Id}' has a negative material stock.");
                }

                if (region.Stocks.ContainsKey(stockData.CivilizationId))
                {
                    throw Invalid(
                        "DUPLICATE_STOCK",
                        $"Region '{region.Id}' has two stocks for '{stockData.CivilizationId}'.");
                }

                region.AddMaterial(stockData.CivilizationId, stockData.Material);
            }
        }

        if (world.Regions.Count == 0)
        {
            throw Invalid("NO_REGIONS", "A save must contain at least one region.");
        }

        foreach (var linkData in data.Links ?? Array.Empty<LinkSaveData>())
        {
            if (string.IsNullOrWhiteSpace(linkData.Id) ||
                world.Links.ContainsKey(linkData.Id))
            {
                throw Invalid("DUPLICATE_LINK", "Region link ids must be unique and non-empty.");
            }

            if (!world.Regions.ContainsKey(linkData.RegionA) ||
                !world.Regions.ContainsKey(linkData.RegionB) ||
                string.Equals(linkData.RegionA, linkData.RegionB, StringComparison.Ordinal))
            {
                throw Invalid(
                    "INVALID_LINK",
                    $"Link '{linkData.Id}' must connect two distinct existing regions.");
            }

            if (linkData.LatencyTicks < 0 || linkData.MessagesPerTick < 0)
            {
                throw Invalid("INVALID_LINK", $"Link '{linkData.Id}' has negative values.");
            }

            world.Links.Add(
                linkData.Id,
                new RegionLink(
                    linkData.Id,
                    linkData.RegionA,
                    linkData.RegionB,
                    linkData.LatencyTicks,
                    linkData.MessagesPerTick));
        }

        foreach (var blueprintData in data.Blueprints ?? Array.Empty<BlueprintSaveData>())
        {
            try
            {
                world.Workshop.Restore(ToBlueprint(blueprintData));
            }
            catch (BlueprintValidationException exception)
            {
                throw Invalid("INVALID_BLUEPRINT", exception.Message);
            }
        }

        foreach (var coreData in data.Cores)
        {
            if (world.Cores.ContainsKey(coreData.CivilizationId))
            {
                throw Invalid(
                    "DUPLICATE_CORE",
                    $"Core '{coreData.CivilizationId}' appears twice.");
            }

            if (!world.Regions.TryGetValue(coreData.RegionId, out var region) ||
                !region.IsCoreRegion)
            {
                throw Invalid(
                    "CORE_REGION_MISSING",
                    $"Core '{coreData.CivilizationId}' references a missing or non-core region.");
            }

            var config = new SeedCoreConfig(
                coreData.EnergyPerTick,
                coreData.MaterialPerTick,
                coreData.MaxEnergyReserve,
                coreData.MaxMaterialReserve);
            config.Validate();

            var core = new SeedCoreState(
                coreData.CivilizationId,
                coreData.RegionId,
                config)
            {
                EnergyReserve = coreData.EnergyReserve,
                MaterialReserve = coreData.MaterialReserve,
            };

            if (core.EnergyReserve < 0 ||
                core.EnergyReserve > core.Config.MaxEnergyReserve ||
                core.MaterialReserve < 0 ||
                core.MaterialReserve > core.Config.MaxMaterialReserve)
            {
                throw Invalid("INVALID_CORE_RESERVE", "Core reserve is outside its cap.");
            }

            foreach (var productData in coreData.LatestProducts ?? Array.Empty<SeedCoreProductSaveData>())
            {
                var blueprint = world.Workshop.GetVersion(productData.BlueprintId, productData.Version);
                if (blueprint is null)
                {
                    throw Invalid(
                        "CORE_PRODUCT_MISSING",
                        $"Core '{core.CivilizationId}' references missing blueprint '{productData.BlueprintId}' v{productData.Version}.");
                }

                try
                {
                    core.RecordLatestProduct(productData.Branch, blueprint);
                }
                catch (Exception exception) when (
                    exception is BlueprintValidationException or ArgumentOutOfRangeException)
                {
                    throw Invalid("INVALID_CORE_PRODUCT", exception.Message);
                }
            }

            world.Cores.Add(core.CivilizationId, core);
        }

        var maxEntityId = 0L;
        foreach (var entity in world.EnumerateEntities())
        {
            maxEntityId = Math.Max(maxEntityId, entity.Id.Value);
        }

        if (world.NextEntityId <= maxEntityId)
        {
            throw Invalid("INVALID_NEXT_ENTITY_ID", "NextEntityId must exceed every stored entity id.");
        }

        var maxMessageId = 0L;
        foreach (var message in data.InFlightMessages)
        {
            if (message.Id <= 0 || message.DeliverTick <= message.SentTick)
            {
                throw Invalid("INVALID_MESSAGE", $"Message '{message.Id}' has invalid timing.");
            }

            var targetRegionId = ResolveTargetRegionId(
                world,
                message.TargetRegionId,
                message.FromEntityId);
            var envelope = new MessageEnvelope(
                message.Id,
                new EntityId(message.FromEntityId),
                message.FromCivilizationId,
                message.ToCivilizationId,
                targetRegionId,
                message.Topic,
                message.Payload,
                message.SentTick,
                message.DeliverTick);
            world.InFlightMessages.Add(envelope);
            maxMessageId = Math.Max(maxMessageId, message.Id);
        }

        foreach (var inboxData in data.Inboxes)
        {
            if (world.Inboxes.ContainsKey(inboxData.CivilizationId))
            {
                throw Invalid(
                    "DUPLICATE_INBOX",
                    $"Inbox '{inboxData.CivilizationId}' appears twice.");
            }

            var inbox = new List<DeliveredMessage>();
            foreach (var message in inboxData.Messages)
            {
                if (message.Id <= 0 || message.DeliveredTick < message.SentTick)
                {
                    throw Invalid(
                        "INVALID_DELIVERED_MESSAGE",
                        $"Delivered message '{message.Id}' has invalid timing.");
                }

                var targetRegionId = ResolveTargetRegionId(
                    world,
                    message.TargetRegionId,
                    message.FromEntityId);
                inbox.Add(new DeliveredMessage(
                    message.Id,
                    new EntityId(message.FromEntityId),
                    message.FromCivilizationId,
                    targetRegionId,
                    message.Topic,
                    message.Payload,
                    message.SentTick,
                    message.DeliveredTick));
                maxMessageId = Math.Max(maxMessageId, message.Id);
            }

            world.Inboxes.Add(inboxData.CivilizationId, inbox);
        }

        if (world.NextMessageId <= maxMessageId)
        {
            throw Invalid("INVALID_NEXT_MESSAGE_ID", "NextMessageId must exceed every stored message id.");
        }

        ApplyStatistics(world, data.Statistics);
        return world;
    }

    private static BlueprintSaveData ToBlueprintSave(DeviceBlueprint blueprint) =>
        new(
            blueprint.BlueprintId,
            blueprint.Version,
            blueprint.Name,
            blueprint.MaxHealth,
            blueprint.EnergyCapacity,
            blueprint.Sensor is null ? null : new SensorSaveData(blueprint.Sensor.RangeMm),
            blueprint.Weapon is null
                ? null
                : new WeaponSaveData(
                    blueprint.Weapon.RangeMm,
                    blueprint.Weapon.Damage,
                    blueprint.Weapon.EnergyCost,
                    blueprint.Weapon.CooldownTicks),
            blueprint.Drive is null
                ? null
                : new DriveSaveData(
                    blueprint.Drive.SpeedMmPerTick,
                    blueprint.Drive.EnergyCostPerMove),
            blueprint.Comm is null
                ? null
                : new CommSaveData(
                    blueprint.Comm.LatencyTicks,
                    blueprint.Comm.EnergyCostPerMessage,
                    blueprint.Comm.BandwidthPerTick),
            blueprint.Power is null
                ? null
                : new PowerSaveData(
                    blueprint.Power.EnergyPerTick,
                    blueprint.Power.StorageCapacity),
            blueprint.State is null
                ? null
                : new StateSaveData(
                    blueprint.State.ComputePerTick,
                    blueprint.State.StorageCapacity),
            blueprint.ControllerProgramId,
            blueprint.MaterialCost);

    private static DeviceBlueprint ToBlueprint(BlueprintSaveData blueprint) =>
        new(
            blueprint.BlueprintId,
            blueprint.Version,
            blueprint.Name,
            blueprint.MaxHealth,
            blueprint.EnergyCapacity,
            blueprint.Sensor is null ? null : new SensorModule(blueprint.Sensor.RangeMm),
            blueprint.Weapon is null
                ? null
                : new WeaponModule(
                    blueprint.Weapon.RangeMm,
                    blueprint.Weapon.Damage,
                    blueprint.Weapon.EnergyCost,
                    blueprint.Weapon.CooldownTicks),
            blueprint.Drive is null
                ? null
                : new DriveModule(
                    blueprint.Drive.SpeedMmPerTick,
                    blueprint.Drive.EnergyCostPerMove),
            blueprint.Comm is null
                ? null
                : new CommModule(
                    blueprint.Comm.LatencyTicks,
                    blueprint.Comm.EnergyCostPerMessage,
                    blueprint.Comm.BandwidthPerTick),
            blueprint.Power is null
                ? null
                : new PowerModule(
                    blueprint.Power.EnergyPerTick,
                    blueprint.Power.StorageCapacity),
            blueprint.State is null
                ? null
                : new StateModule(
                    blueprint.State.ComputePerTick,
                    blueprint.State.StorageCapacity),
            blueprint.ControllerProgramId,
            blueprint.MaterialCost);

    private static RegionSaveData ToRegion(RegionState region) =>
        new(
            region.Id,
            region.WidthMm,
            region.HeightMm,
            region.IsCoreRegion,
            region.Entities.Values.Select(ToEntity).ToArray(),
            region.Stocks
                .Select(stock => new StockSaveData(stock.Key, stock.Value.Material))
                .ToArray());

    private static EntitySaveData ToEntity(EntityState entity) =>
        new(
            entity.Id.Value,
            entity.CivilizationId,
            entity.RegionId,
            entity.Position.X,
            entity.Position.Y,
            entity.MaxHealth,
            entity.Health,
            entity.Energy,
            entity.EnergyCapacity,
            entity.Material,
            entity.NextWeaponReadyTick,
            entity.Sensor is null ? null : new SensorSaveData(entity.Sensor.RangeMm),
            entity.Weapon is null
                ? null
                : new WeaponSaveData(
                    entity.Weapon.RangeMm,
                    entity.Weapon.Damage,
                    entity.Weapon.EnergyCost,
                    entity.Weapon.CooldownTicks),
            entity.Drive is null
                ? null
                : new DriveSaveData(
                    entity.Drive.SpeedMmPerTick,
                    entity.Drive.EnergyCostPerMove),
            entity.Comm is null
                ? null
                : new CommSaveData(
                    entity.Comm.LatencyTicks,
                    entity.Comm.EnergyCostPerMessage,
                    entity.Comm.BandwidthPerTick),
            entity.BlueprintId,
            entity.BlueprintVersion,
            entity.ControllerProgramId,
            entity.Power is null
                ? null
                : new PowerSaveData(
                    entity.Power.EnergyPerTick,
                    entity.Power.StorageCapacity),
            entity.State is null
                ? null
                : new StateSaveData(
                    entity.State.ComputePerTick,
                    entity.State.StorageCapacity));

    private static CoreSaveData ToCore(SeedCoreState core) =>
        new(
            core.CivilizationId,
            core.RegionId,
            core.Config.EnergyPerTick,
            core.Config.MaterialPerTick,
            core.Config.MaxEnergyReserve,
            core.Config.MaxMaterialReserve,
            core.EnergyReserve,
            core.MaterialReserve,
            core.LatestProducts
                .Select(product => new SeedCoreProductSaveData(
                    product.Key,
                    product.Value.BlueprintId,
                    product.Value.Version))
                .ToArray());

    private static MessageSaveData ToMessage(MessageEnvelope message) =>
        new(
            message.Id,
            message.FromEntityId.Value,
            message.FromCivilizationId,
            message.ToCivilizationId,
            message.Topic,
            message.Payload,
            message.SentTick,
            message.DeliverTick,
            message.TargetRegionId);

    private static DeliveredMessageSaveData ToDeliveredMessage(DeliveredMessage message) =>
        new(
            message.Id,
            message.FromEntityId.Value,
            message.FromCivilizationId,
            message.Topic,
            message.Payload,
            message.SentTick,
            message.DeliveredTick,
            message.TargetRegionId);

    private static void ApplyStatistics(WorldState world, StatisticsSaveData statistics)
    {
        world.Statistics.EnergySpentTotal = statistics.EnergySpentTotal;
        world.Statistics.EnergyRegeneratedTotal = statistics.EnergyRegeneratedTotal;
        world.Statistics.EnergyGeneratedTotal = statistics.EnergyGeneratedTotal;
        world.Statistics.MaterialSpentTotal = statistics.MaterialSpentTotal;
        world.Statistics.MaterialRegeneratedTotal = statistics.MaterialRegeneratedTotal;
        world.Statistics.ShotsFired = statistics.ShotsFired;
        world.Statistics.DamageDealt = statistics.DamageDealt;
        world.Statistics.EntitiesDestroyed = statistics.EntitiesDestroyed;
        world.Statistics.MessagesDelivered = statistics.MessagesDelivered;
        world.Statistics.MessagesExpired = statistics.MessagesExpired;
    }

    private static string ResolveTargetRegionId(
        WorldState world,
        string savedTargetRegionId,
        long fromEntityId)
    {
        if (!string.IsNullOrWhiteSpace(savedTargetRegionId))
        {
            return savedTargetRegionId;
        }

        var sender = world.FindEntity(new EntityId(fromEntityId));
        return sender?.RegionId ?? world.Regions.Keys.First();
    }

    private static SaveFormatException Invalid(string code, string message) => new(code, message);
}
