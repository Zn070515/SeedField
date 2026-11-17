namespace SeedField.Simulation;

public static partial class WorldSaveSerializer
{
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
                    blueprint.Power.StorageCapacity,
                    blueprint.Power.Priority),
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
                    blueprint.Power.StorageCapacity,
                    blueprint.Power.Priority),
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
                    entity.Power.StorageCapacity,
                    entity.Power.Priority),
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
                .ToArray(),
            core.Config.RecoveryWindowTicks,
            core.Config.RecoveryQuotaPerWindow,
            core.Config.RecoveryCostGrowthPercent,
            core.Config.RecoveryStreakDecayTicks,
            core.RecoveryWindowStartTick,
            core.RecoveriesInWindow,
            core.RecoveryStreak,
            core.LastRecoveryTick,
            core.Config.RecoveryOverdraftCostPercent);

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
