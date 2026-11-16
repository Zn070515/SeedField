using System.Text.Json;

namespace SeedField.Simulation;

/// <summary>
/// JSON persistence for the authoritative world. Loading rebuilds a brand-new
/// WorldState and validates every structural invariant before returning it.
/// </summary>
public static class WorldSaveSerializer
{
    public const int CurrentSchemaVersion = 4;
    public const int OldestSupportedSchemaVersion = 1;
    public const string GameVersion = "0.1.0-m0";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string Save(WorldState world) =>
        SaveData(ToData(world));

    public static string SaveData(WorldSaveData data) =>
        JsonSerializer.Serialize(data, Options);

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
                world.Statistics.MessagesExpired,
                world.Statistics.MaterialShippedTotal,
                world.Statistics.MaterialDeliveredTotal,
                world.Statistics.EnergyShippedTotal,
                world.Statistics.EnergyDeliveredTotal,
                world.Statistics.EnergyStoredTotal,
                world.Statistics.EnergyDrawnTotal,
                world.Statistics.EnergyLostTotal,
                world.Statistics.MaterialLostTotal,
                world.Statistics.ConstructionsStartedTotal,
                world.Statistics.ConstructionsCompletedTotal,
                world.Statistics.MaterialRefundedTotal),
            world.Workshop.GetAllVersions().Select(ToBlueprintSave).ToArray(),
            world.Links.Values
                .Select(link => new LinkSaveData(
                    link.Id,
                    link.RegionA,
                    link.RegionB,
                    link.LatencyTicks,
                    link.MessagesPerTick,
                    link.MaterialPerTick,
                    link.EnergyPerTick))
                .ToArray(),
            world.InTransitMaterial
                .OrderBy(shipment => shipment.Id)
                .Select(shipment => new MaterialShipmentSaveData(
                    shipment.Id,
                    shipment.FromEntityId.Value,
                    shipment.CivilizationId,
                    shipment.FromRegionId,
                    shipment.ToRegionId,
                    shipment.Route,
                    shipment.Amount,
                    shipment.SentTick,
                    shipment.ArriveTick))
                .ToArray(),
            world.NextShipmentId,
            world.InTransitEnergy
                .OrderBy(shipment => shipment.Id)
                .Select(shipment => new EnergyShipmentSaveData(
                    shipment.Id,
                    shipment.FromEntityId.Value,
                    shipment.CivilizationId,
                    shipment.FromRegionId,
                    shipment.ToRegionId,
                    shipment.Route,
                    shipment.Amount,
                    shipment.SentTick,
                    shipment.ArriveTick))
                .ToArray(),
            world.NextEnergyShipmentId,
            null,
            world.ControlPoints.Values
                .Select(ToControlPointSave)
                .ToArray(),
            world.Match is null
                ? null
                : new MatchSaveData(
                    world.Match.Rules.CapturedPointsToWin,
                    world.Match.WinnerCivilizationId),
            world.ConstructionOrders
                .OrderBy(order => order.Id)
                .Select(order => new ConstructionOrderSaveData(
                    order.Id,
                    order.CivilizationId,
                    order.BlueprintId,
                    order.BlueprintVersion,
                    order.RegionId,
                    order.Position.X,
                    order.Position.Y,
                    order.StartTick,
                    order.CompleteTick,
                    order.MaterialPaid))
                .ToArray(),
            world.NextConstructionOrderId);
    }

    public static WorldState FromData(WorldSaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.SchemaVersion < OldestSupportedSchemaVersion ||
            data.SchemaVersion > CurrentSchemaVersion)
        {
            throw new SaveFormatException(
                "UNSUPPORTED_SAVE_VERSION",
                $"Save schema {data.SchemaVersion} is not supported; " +
                $"expected {OldestSupportedSchemaVersion}..{CurrentSchemaVersion}.");
        }

        if (data.SchemaVersion < CurrentSchemaVersion)
        {
            data = Migrate(data);
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
                if (stockData.Material < 0 || stockData.Energy < 0)
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
                    linkData.MessagesPerTick,
                    linkData.MaterialPerTick,
                    linkData.EnergyPerTick));
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

        foreach (var region in world.Regions.Values)
        {
            if (!region.IsCoreRegion)
            {
                continue;
            }

            foreach (var entity in region.Entities.Values)
            {
                if (CoreProtection.IsProtectedFrom(world, region.Id, entity.CivilizationId))
                {
                    throw Invalid(
                        "FOREIGN_ENTITY_IN_CORE",
                        $"Entity '{entity.Id}' of '{entity.CivilizationId}' is inside a protected core region.");
                }
            }
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

        var maxShipmentId = 0L;
        foreach (var shipmentData in data.MaterialShipments ?? Array.Empty<MaterialShipmentSaveData>())
        {
            if (shipmentData.Id <= 0 ||
                shipmentData.Amount <= 0 ||
                shipmentData.ArriveTick <= shipmentData.SentTick)
            {
                throw Invalid(
                    "INVALID_SHIPMENT",
                    $"Shipment '{shipmentData.Id}' has invalid id, amount or timing.");
            }

            if (!world.Regions.ContainsKey(shipmentData.FromRegionId) ||
                !world.Regions.ContainsKey(shipmentData.ToRegionId) ||
                !IsValidShipmentRoute(world, shipmentData))
            {
                throw Invalid(
                    "INVALID_SHIPMENT_ROUTE",
                    $"Shipment '{shipmentData.Id}' references a missing region or invalid route.");
            }

            world.InTransitMaterial.Add(new MaterialShipment(
                shipmentData.Id,
                new EntityId(shipmentData.FromEntityId),
                shipmentData.CivilizationId,
                shipmentData.FromRegionId,
                shipmentData.ToRegionId,
                shipmentData.Route,
                shipmentData.Amount,
                shipmentData.SentTick,
                shipmentData.ArriveTick));
            maxShipmentId = Math.Max(maxShipmentId, shipmentData.Id);
        }

        var nextShipmentId = data.NextShipmentId > 0
            ? data.NextShipmentId
            : maxShipmentId + 1;
        if (nextShipmentId <= maxShipmentId)
        {
            throw Invalid(
                "INVALID_NEXT_SHIPMENT_ID",
                "NextShipmentId must exceed every stored shipment id.");
        }

        world.NextShipmentId = nextShipmentId;

        var maxEnergyShipmentId = 0L;
        foreach (var shipmentData in data.EnergyShipments ?? Array.Empty<EnergyShipmentSaveData>())
        {
            if (shipmentData.Id <= 0 ||
                shipmentData.Amount <= 0 ||
                shipmentData.ArriveTick <= shipmentData.SentTick ||
                !world.Regions.ContainsKey(shipmentData.FromRegionId) ||
                !world.Regions.ContainsKey(shipmentData.ToRegionId) ||
                !IsValidRoute(
                    world,
                    shipmentData.FromRegionId,
                    shipmentData.ToRegionId,
                    shipmentData.Route))
            {
                throw Invalid(
                    "INVALID_ENERGY_SHIPMENT",
                    $"Energy shipment '{shipmentData.Id}' is invalid.");
            }

            world.InTransitEnergy.Add(new EnergyShipment(
                shipmentData.Id,
                new EntityId(shipmentData.FromEntityId),
                shipmentData.CivilizationId,
                shipmentData.FromRegionId,
                shipmentData.ToRegionId,
                shipmentData.Route,
                shipmentData.Amount,
                shipmentData.SentTick,
                shipmentData.ArriveTick));
            maxEnergyShipmentId = Math.Max(maxEnergyShipmentId, shipmentData.Id);
        }

        var nextEnergyShipmentId = data.NextEnergyShipmentId > 0
            ? data.NextEnergyShipmentId
            : maxEnergyShipmentId + 1;
        if (nextEnergyShipmentId <= maxEnergyShipmentId)
        {
            throw Invalid(
                "INVALID_NEXT_ENERGY_SHIPMENT_ID",
                "NextEnergyShipmentId must exceed every stored energy shipment id.");
        }

        world.NextEnergyShipmentId = nextEnergyShipmentId;

        foreach (var pointData in data.ControlPoints ?? Array.Empty<ControlPointSaveData>())
        {
            if (!world.Regions.TryGetValue(pointData.RegionId, out var pointRegion) ||
                !pointRegion.Contains(new Vec2(pointData.X, pointData.Y)) ||
                pointData.RadiusMm <= 0 ||
                pointData.HoldTicksRequired <= 0 ||
                pointData.HoldTicks < 0 ||
                (pointData.HoldingCivilizationId is not null && pointData.HoldTicks < 1) ||
                (pointData.OwnerCivilizationId is not null &&
                 pointData.HoldTicks < pointData.HoldTicksRequired))
            {
                throw Invalid("INVALID_CONTROL_POINT", "Control point state is invalid.");
            }

            if (world.ControlPoints.ContainsKey(pointData.Id))
            {
                throw Invalid("DUPLICATE_CONTROL_POINT", "Control point ids must be unique.");
            }

            world.ControlPoints.Add(
                pointData.Id,
                new ControlPointState(
                    pointData.Id,
                    pointData.RegionId,
                    new Vec2(pointData.X, pointData.Y),
                    pointData.RadiusMm,
                    pointData.HoldTicksRequired)
                {
                    HoldingCivilizationId = pointData.HoldingCivilizationId,
                    HoldTicks = pointData.HoldTicks,
                    OwnerCivilizationId = pointData.OwnerCivilizationId,
                });
        }

        if (data.Match is { } matchData)
        {
            if (matchData.CapturedPointsToWin <= 0)
            {
                throw Invalid("INVALID_MATCH", "Match victory threshold must be positive.");
            }

            world.Match = new MatchState(new MatchRules(matchData.CapturedPointsToWin))
            {
                WinnerCivilizationId = matchData.WinnerCivilizationId,
            };
        }

        var maxConstructionOrderId = 0L;
        foreach (var orderData in data.ConstructionOrders ?? Array.Empty<ConstructionOrderSaveData>())
        {
            if (orderData.Id <= 0 ||
                orderData.MaterialPaid < 0 ||
                orderData.CompleteTick <= orderData.StartTick ||
                !world.Regions.TryGetValue(orderData.RegionId, out var orderRegion) ||
                !orderRegion.Contains(new Vec2(orderData.X, orderData.Y)) ||
                world.Workshop.GetVersion(orderData.BlueprintId, orderData.BlueprintVersion) is null)
            {
                throw Invalid(
                    "INVALID_CONSTRUCTION_ORDER",
                    $"Construction order '{orderData.Id}' is invalid.");
            }

            world.ConstructionOrders.Add(new ConstructionOrder(
                orderData.Id,
                orderData.CivilizationId,
                orderData.BlueprintId,
                orderData.BlueprintVersion,
                orderData.RegionId,
                new Vec2(orderData.X, orderData.Y),
                orderData.StartTick,
                orderData.CompleteTick,
                orderData.MaterialPaid));
            maxConstructionOrderId = Math.Max(maxConstructionOrderId, orderData.Id);
        }

        var nextConstructionOrderId = data.NextConstructionOrderId > 0
            ? data.NextConstructionOrderId
            : maxConstructionOrderId + 1;
        if (nextConstructionOrderId <= maxConstructionOrderId)
        {
            throw Invalid(
                "INVALID_NEXT_CONSTRUCTION_ORDER_ID",
                "NextConstructionOrderId must exceed every stored order id.");
        }

        world.NextConstructionOrderId = nextConstructionOrderId;

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
            blueprint.MaterialCost,
            blueprint.BuildTicks);

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
            blueprint.MaterialCost,
            blueprint.BuildTicks);

    private static RegionSaveData ToRegion(RegionState region) =>
        new(
            region.Id,
            region.WidthMm,
            region.HeightMm,
            region.IsCoreRegion,
            region.Entities.Values.Select(ToEntity).ToArray(),
            region.Stocks
                .Select(stock => new StockSaveData(
                    stock.Key,
                    stock.Value.Material,
                    stock.Value.Energy))
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
        world.Statistics.MaterialShippedTotal = statistics.MaterialShippedTotal;
        world.Statistics.MaterialDeliveredTotal = statistics.MaterialDeliveredTotal;
        world.Statistics.EnergyShippedTotal = statistics.EnergyShippedTotal;
        world.Statistics.EnergyDeliveredTotal = statistics.EnergyDeliveredTotal;
        world.Statistics.EnergyStoredTotal = statistics.EnergyStoredTotal;
        world.Statistics.EnergyDrawnTotal = statistics.EnergyDrawnTotal;
        world.Statistics.EnergyLostTotal = statistics.EnergyLostTotal;
        world.Statistics.MaterialLostTotal = statistics.MaterialLostTotal;
        world.Statistics.ConstructionsStartedTotal = statistics.ConstructionsStartedTotal;
        world.Statistics.ConstructionsCompletedTotal = statistics.ConstructionsCompletedTotal;
        world.Statistics.MaterialRefundedTotal = statistics.MaterialRefundedTotal;
    }

    /// <summary>
    /// v1 predates links, blueprint registry, material/energy shipments and the
    /// associated ids/stats. Missing collections are treated as empty and the
    /// loaders compute next ids from stored data when zero.
    /// </summary>
    private static WorldSaveData Migrate(WorldSaveData data) =>
        data with
        {
            SchemaVersion = CurrentSchemaVersion,
            Blueprints = data.Blueprints ?? Array.Empty<BlueprintSaveData>(),
            Links = data.Links ?? Array.Empty<LinkSaveData>(),
            MaterialShipments = data.MaterialShipments ?? Array.Empty<MaterialShipmentSaveData>(),
            EnergyShipments = data.EnergyShipments ?? Array.Empty<EnergyShipmentSaveData>(),
            InFlightMessages = data.InFlightMessages ?? Array.Empty<MessageSaveData>(),
            Inboxes = data.Inboxes ?? Array.Empty<InboxSaveData>(),
            ControlPoints = data.ControlPoints ??
                            (data.ControlPoint is null
                                ? Array.Empty<ControlPointSaveData>()
                                : new[] { data.ControlPoint }),
            ConstructionOrders = data.ConstructionOrders ??
                                 Array.Empty<ConstructionOrderSaveData>(),
        };

    private static ControlPointSaveData ToControlPointSave(ControlPointState point) =>
        new(
            point.Id,
            point.RegionId,
            point.Position.X,
            point.Position.Y,
            point.RadiusMm,
            point.HoldTicksRequired,
            point.HoldingCivilizationId,
            point.HoldTicks,
            point.OwnerCivilizationId);

    private static bool IsValidShipmentRoute(WorldState world, MaterialShipmentSaveData shipment) =>
        IsValidRoute(world, shipment.FromRegionId, shipment.ToRegionId, shipment.Route);

    private static bool IsValidRoute(
        WorldState world,
        string fromRegionId,
        string toRegionId,
        string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return false;
        }

        var currentRegion = fromRegionId;
        foreach (var linkId in route.Split('>', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!world.Links.TryGetValue(linkId, out var link) || !link.Connects(currentRegion))
            {
                return false;
            }

            currentRegion = link.Other(currentRegion);
        }

        return string.Equals(currentRegion, toRegionId, StringComparison.Ordinal);
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
