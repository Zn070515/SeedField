namespace SeedField.Simulation;

public static partial class WorldSaveSerializer
{
    private static void LoadRegions(WorldState world, WorldSaveData data)
    {
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
                LoadEntity(world, region, entityData);
            }

            foreach (var stockData in regionData.Stocks ?? Array.Empty<StockSaveData>())
            {
                LoadStock(region, stockData);
            }
        }

        if (world.Regions.Count == 0)
        {
            throw Invalid("NO_REGIONS", "A save must contain at least one region.");
        }
    }

    private static void LoadCombatRules(WorldState world, WorldSaveData data)
    {
        if (data.CombatHitChancePercent is < 0 or > 100 ||
            data.CombatDamageVariancePercent is < 0 or > 100 ||
            data.CombatLootChancePercent is < 0 or > 100)
        {
            throw Invalid(
                "INVALID_COMBAT_RULES",
                "Combat percentages must be within 0..100.");
        }

        if (data.CombatLootMaterial < 0)
        {
            throw Invalid(
                "INVALID_COMBAT_LOOT",
                "Combat loot material must be non-negative.");
        }

        world.CombatHitChancePercent = data.CombatHitChancePercent;
        world.CombatDamageVariancePercent = data.CombatDamageVariancePercent;
        world.CombatLootChancePercent = data.CombatLootChancePercent;
        world.CombatLootMaterial = data.CombatLootMaterial;
    }

    private static void LoadLinks(WorldState world, WorldSaveData data)
    {
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

            if (linkData.LatencyTicks < 0 ||
                linkData.MessagesPerTick < 0 ||
                linkData.MaterialPerTick < 0 ||
                linkData.EnergyPerTick < 0 ||
                linkData.PowerPerTick < 0 ||
                linkData.PowerLatencyTicks < 0 ||
                linkData.PowerLossPercent is < 0 or > 100)
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
                    linkData.EnergyPerTick,
                    linkData.PowerPerTick,
                    linkData.PowerLatencyTicks,
                    linkData.PowerLossPercent));
        }
    }

    private static void LoadWrecks(WorldState world, WorldSaveData data)
    {
        if (data.WreckRecoveryPercent is < 0 or > 100)
        {
            throw Invalid(
                "INVALID_WRECK_RECOVERY_PERCENT",
                "Wreck recovery percent must be within 0..100.");
        }

        world.WreckRecoveryPercent = data.WreckRecoveryPercent;
        foreach (var wreckData in data.Wrecks ?? Array.Empty<WreckSaveData>())
        {
            if (wreckData.Id <= 0 ||
                world.Wrecks.ContainsKey(wreckData.Id) ||
                string.IsNullOrWhiteSpace(wreckData.RegionId) ||
                string.IsNullOrWhiteSpace(wreckData.OwnerCivilizationId) ||
                string.IsNullOrWhiteSpace(wreckData.BlueprintId) ||
                wreckData.BlueprintVersion <= 0 ||
                wreckData.MaterialRecoverable < 0 ||
                wreckData.CreatedTick < 0 ||
                wreckData.CreatedTick > data.Tick ||
                !world.Regions.TryGetValue(wreckData.RegionId, out var region) ||
                !region.Contains(new Vec2(wreckData.X, wreckData.Y)) ||
                (wreckData.OccupiedByCivilizationId is null
                    ? wreckData.OccupiedTick != -1
                    : string.IsNullOrWhiteSpace(wreckData.OccupiedByCivilizationId) ||
                      wreckData.OccupiedTick < 0 ||
                      wreckData.OccupiedTick > data.Tick))
            {
                throw Invalid(
                    "INVALID_WRECK",
                    $"Wreck '{wreckData.Id}' is invalid.");
            }

            var wreck = new WreckState(
                wreckData.Id,
                wreckData.RegionId,
                new Vec2(wreckData.X, wreckData.Y),
                wreckData.OwnerCivilizationId,
                wreckData.BlueprintId,
                wreckData.BlueprintVersion,
                wreckData.MaterialRecoverable,
                wreckData.CreatedTick);
            if (wreckData.OccupiedByCivilizationId is { } occupier)
            {
                wreck.Occupy(occupier, wreckData.OccupiedTick);
            }

            world.Wrecks.Add(wreckData.Id, wreck);
        }

        var maxWreckId = world.Wrecks.Keys.DefaultIfEmpty(0).Max();
        world.NextWreckId = ResolveNextId(
            data.NextWreckId,
            maxWreckId,
            "INVALID_NEXT_WRECK_ID");
    }

    private static void LoadBlueprints(WorldState world, WorldSaveData data)
    {
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
    }

    private static void LoadCores(WorldState world, WorldSaveData data)
    {
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

            if (coreData.RecoveryWindowTicks < 0 ||
                coreData.RecoveryQuotaPerWindow < 0 ||
                coreData.RecoveryCostGrowthPercent < 0 ||
                coreData.RecoveryStreakDecayTicks < 0 ||
                coreData.RecoveryWindowStartTick < 0 ||
                coreData.RecoveriesInWindow < 0 ||
                coreData.RecoveryStreak < 0 ||
                coreData.LastRecoveryTick < -1 ||
                coreData.RecoveryOverdraftCostPercent < 0 ||
                coreData.AnchorTarget < 0 ||
                coreData.AnchorRecoveryCostPercent < 0 ||
                coreData.SiegeRecoveryCostPercent < 0 ||
                coreData.SiegeQuotaPenalty < 0 ||
                coreData.RecoveryMaintenanceMaterialCost < 0 ||
                coreData.LastRecoveryTick > data.Tick ||
                coreData.RecoveryWindowStartTick > data.Tick ||
                (coreData.RecoveryWindowTicks > 0 &&
                 coreData.RecoveryQuotaPerWindow > 0 &&
                 coreData.RecoveryOverdraftCostPercent == 0 &&
                 coreData.RecoveriesInWindow > coreData.RecoveryQuotaPerWindow))
            {
                throw Invalid(
                    "INVALID_CORE_RECOVERY",
                    $"Core '{coreData.CivilizationId}' has invalid recovery quota state.");
            }

            var config = new SeedCoreConfig(
                coreData.EnergyPerTick,
                coreData.MaterialPerTick,
                coreData.MaxEnergyReserve,
                coreData.MaxMaterialReserve,
                coreData.RecoveryWindowTicks,
                coreData.RecoveryQuotaPerWindow,
                coreData.RecoveryCostGrowthPercent,
                coreData.RecoveryStreakDecayTicks,
                coreData.RecoveryOverdraftCostPercent,
                coreData.AnchorRecoveryCostPercent,
                coreData.SiegeRecoveryCostPercent,
                coreData.SiegeQuotaPenalty,
                coreData.RecoveryMaintenanceMaterialCost,
                coreData.AnchorEnergyPerTick,
                coreData.AnchorMaterialPerTick,
                coreData.AnchorStorageBonus);
            config.Validate();

            var core = new SeedCoreState(
                coreData.CivilizationId,
                coreData.RegionId,
                config)
            {
                EnergyReserve = coreData.EnergyReserve,
                MaterialReserve = coreData.MaterialReserve,
                RecoveryWindowStartTick = coreData.RecoveryWindowStartTick,
                RecoveriesInWindow = coreData.RecoveriesInWindow,
                RecoveryStreak = coreData.RecoveryStreak,
                LastRecoveryTick = coreData.LastRecoveryTick,
                AnchorTarget = coreData.AnchorTarget,
            };

            if (core.EnergyReserve < 0 ||
                core.EnergyReserve > core.Config.MaxEnergyReserve ||
                core.MaterialReserve < 0 ||
                core.MaterialReserve > core.Config.MaxMaterialReserve)
            {
                throw Invalid("INVALID_CORE_RESERVE", "Core reserve is outside its cap.");
            }

            core.AnchorTarget = Math.Max(
                core.AnchorTarget,
                CoreShellRules.CountAliveAnchors(world, core.CivilizationId));

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
    }

    private static void ValidateCoreExclusion(WorldState world)
    {
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
    }

    private static void ValidateNextEntityId(WorldState world)
    {
        var maxEntityId = 0L;
        foreach (var entity in world.EnumerateEntities())
        {
            maxEntityId = Math.Max(maxEntityId, entity.Id.Value);
        }

        if (world.NextEntityId <= maxEntityId)
        {
            throw Invalid("INVALID_NEXT_ENTITY_ID", "NextEntityId must exceed every stored entity id.");
        }
    }

    private static void LoadControlPoints(WorldState world, WorldSaveData data)
    {
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
    }

    private static void LoadConstructionOrders(WorldState world, WorldSaveData data)
    {
        var maxConstructionOrderId = 0L;
        foreach (var orderData in data.ConstructionOrders ?? Array.Empty<ConstructionOrderSaveData>())
        {
            if (orderData.Id <= 0 ||
                orderData.MaterialPaid < 0 ||
                orderData.ComponentsPaid < 0 ||
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
                orderData.MaterialPaid,
                orderData.ComponentsPaid));
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
    }
}
