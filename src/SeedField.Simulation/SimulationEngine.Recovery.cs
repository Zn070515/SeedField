namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    public BootstrapResult TryBootstrapFromCore(
        string civilizationId,
        RecoveryBlueprint blueprint,
        Vec2 position)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        ValidateBlueprint(blueprint);

        if (!World.Cores.TryGetValue(civilizationId, out var core))
        {
            return BootstrapFailure(SimulationErrorCode.CoreNotAvailable, "No seed core for civilization.");
        }

        if (!World.Regions.TryGetValue(core.RegionId, out var region) || !region.IsCoreRegion)
        {
            return BootstrapFailure(SimulationErrorCode.CoreNotAvailable, "Core region is unavailable.");
        }

        if (!region.Contains(position))
        {
            return BootstrapFailure(SimulationErrorCode.OutOfBounds, "Bootstrap position is outside the core region.");
        }

        var tick = World.Tick;
        var pressure = CoreShellRules.EvaluatePressure(World, core);
        var quota = core.EvaluateRecoveryQuota(
            tick,
            pressure.QuotaPenalty,
            out var resetsInTicks,
            out var effectiveLimit);
        if (quota == SeedCoreState.RecoveryQuotaDecision.Exhausted)
        {
            return BootstrapFailure(
                SimulationErrorCode.RecoveryQuotaExhausted,
                $"Recovery quota exhausted; the window resets in {resetsInTicks} ticks.",
                new RecoveryQuotaDenied(
                    tick,
                    civilizationId,
                    core.RegionId,
                    core.RecoveriesInWindow,
                    effectiveLimit,
                    resetsInTicks));
        }

        var multiplier = core.RecoveryCostMultiplierPercent(
            tick,
            quota == SeedCoreState.RecoveryQuotaDecision.Overdraft,
            pressure.CostPercent);
        var energyCost = ScaleRecoveryCost(blueprint.RequiredEnergy, multiplier);
        var materialCost = ScaleRecoveryCost(blueprint.RequiredMaterial, multiplier);

        if (core.EnergyReserve < energyCost)
        {
            return BootstrapFailure(SimulationErrorCode.NoEnergy, "Core energy reserve is insufficient.");
        }

        if (core.MaterialReserve < materialCost)
        {
            return BootstrapFailure(
                SimulationErrorCode.InsufficientMaterial,
                "Core material reserve is insufficient.");
        }

        core.EnergyReserve -= energyCost;
        core.MaterialReserve -= materialCost;
        World.Statistics.EnergySpentTotal += energyCost;
        World.Statistics.MaterialSpentTotal += materialCost;
        core.RecordRecovery(tick);

        var entityId = new EntityId(World.NextEntityId++);
        var definition = new EntityDefinition(
            entityId,
            civilizationId,
            core.RegionId,
            position,
            blueprint.MaxHealth,
            blueprint.Energy,
            blueprint.EnergyCapacity,
            blueprint.Material,
            blueprint.Sensor,
            blueprint.Weapon,
            blueprint.Drive,
            blueprint.Comm);

        region.Entities.Add(entityId, new EntityState(definition));

        var bootstrapEvent = new EntityBootstrapped(
            World.Tick,
            entityId,
            civilizationId,
            blueprint.Name,
            energyCost,
            materialCost);

        return new BootstrapResult(
            true,
            entityId,
            SimulationErrorCode.None,
            "Minimum entity bootstrapped from the seed core.",
            bootstrapEvent);
    }

    /// <summary>
    /// The unified host command path for workshop deployment and explicit upgrades.
    /// UI callers must use this instead of mutating DeploymentService directly.
    /// </summary>

    /// <summary>
    /// Unified host entry for core recovery. Dialogue/UI must use this command path
    /// rather than calling the low-level bootstrap methods directly.
    /// </summary>
    public RecoveryCommandResult ExecuteRecoveryCommand(RecoveryCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command switch
        {
            BootstrapCoreCommand bootstrap => FromBootstrap(
                TryBootstrapFromCore(
                    bootstrap.CivilizationId,
                    bootstrap.Blueprint,
                    bootstrap.Position)),
            RestoreCoreProductCommand restore => FromBootstrap(
                TryRestoreLatestProduct(
                    restore.CivilizationId,
                    restore.Branch,
                    restore.Position,
                    restore.EnergyCost,
                    restore.MaterialCost)),
            MaintainCoreCommand maintain => MaintainCore(maintain),
            _ => new RecoveryCommandResult(
                false,
                null,
                SimulationErrorCode.DeploymentRejected,
                "Unknown recovery command.",
                null),
        };
    }

    private static RecoveryCommandResult FromBootstrap(BootstrapResult result) =>
        new(
            result.Success,
            result.EntityId,
            result.ErrorCode,
            result.Message,
            result.Event);

    private RecoveryCommandResult MaintainCore(MaintainCoreCommand command)
    {
        if (!World.Cores.TryGetValue(command.CivilizationId, out var core))
        {
            return new RecoveryCommandResult(
                false,
                null,
                SimulationErrorCode.CoreNotAvailable,
                "No seed core for civilization.",
                null);
        }

        var cost = core.Config.RecoveryMaintenanceMaterialCost;
        if (cost > 0 && core.MaterialReserve < cost)
        {
            return new RecoveryCommandResult(
                false,
                null,
                SimulationErrorCode.InsufficientMaterial,
                $"Core maintenance costs {cost} M; reserve is {core.MaterialReserve}.",
                null);
        }

        if (cost > 0)
        {
            core.MaterialReserve -= cost;
            World.Statistics.MaterialSpentTotal += cost;
        }

        core.RecoveriesInWindow = 0;
        core.RecoveryStreak = 0;
        core.RecoveryWindowStartTick = World.Tick;
        core.LastRecoveryTick = -1;

        var maintained = new CoreMaintained(
            World.Tick,
            core.CivilizationId,
            core.RegionId,
            cost);
        return new RecoveryCommandResult(
            true,
            null,
            SimulationErrorCode.None,
            $"Core maintained; recovery debt cleared (−{cost} M).",
            maintained);
    }

    /// <summary>
    /// Restores the latest confirmed product for one branch directly from the core.
    /// Costs are supplied by the caller because the real recovery quotas are still an
    /// open design value.
    /// </summary>
    public BootstrapResult TryRestoreLatestProduct(
        string civilizationId,
        DeviceBranch branch,
        Vec2 position,
        long energyCost,
        long materialCost)
    {
        if (energyCost < 0 || materialCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(energyCost), "Recovery costs must be non-negative.");
        }

        var tick = World.Tick;

        if (!World.Cores.TryGetValue(civilizationId, out var core))
        {
            return BootstrapFailure(SimulationErrorCode.CoreNotAvailable, "No seed core for civilization.");
        }

        if (!core.LatestProducts.TryGetValue(branch, out var product))
        {
            return BootstrapFailure(
                SimulationErrorCode.DeploymentRejected,
                $"No confirmed product is registered for {branch}.");
        }

        var blueprint = World.Workshop.GetVersion(product.BlueprintId, product.Version);
        if (blueprint is null)
        {
            return BootstrapFailure(
                SimulationErrorCode.DeploymentRejected,
                $"Registered product {product.BlueprintId} v{product.Version} is missing from the workshop.");
        }

        if (!World.Regions.TryGetValue(core.RegionId, out var region) || !region.IsCoreRegion)
        {
            return BootstrapFailure(SimulationErrorCode.CoreNotAvailable, "Core region is unavailable.");
        }

        if (!region.Contains(position))
        {
            return BootstrapFailure(SimulationErrorCode.OutOfBounds, "Restore position is outside the core region.");
        }

        var pressure = CoreShellRules.EvaluatePressure(World, core);
        var quota = core.EvaluateRecoveryQuota(
            tick,
            pressure.QuotaPenalty,
            out var resetsInTicks,
            out var effectiveLimit);
        if (quota == SeedCoreState.RecoveryQuotaDecision.Exhausted)
        {
            return BootstrapFailure(
                SimulationErrorCode.RecoveryQuotaExhausted,
                $"Recovery quota exhausted; the window resets in {resetsInTicks} ticks.",
                new RecoveryQuotaDenied(
                    tick,
                    civilizationId,
                    core.RegionId,
                    core.RecoveriesInWindow,
                    effectiveLimit,
                    resetsInTicks));
        }

        var multiplier = core.RecoveryCostMultiplierPercent(
            tick,
            quota == SeedCoreState.RecoveryQuotaDecision.Overdraft,
            pressure.CostPercent);
        energyCost = ScaleRecoveryCost(energyCost, multiplier);
        materialCost = ScaleRecoveryCost(materialCost, multiplier);

        if (core.EnergyReserve < energyCost)
        {
            return BootstrapFailure(SimulationErrorCode.NoEnergy, "Core energy reserve is insufficient.");
        }

        if (core.MaterialReserve < materialCost)
        {
            return BootstrapFailure(
                SimulationErrorCode.InsufficientMaterial,
                "Core material reserve is insufficient.");
        }

        core.EnergyReserve -= energyCost;
        core.MaterialReserve -= materialCost;
        World.Statistics.EnergySpentTotal += energyCost;
        World.Statistics.MaterialSpentTotal += materialCost;
        core.RecordRecovery(tick);

        var entity = DeploymentService.Deploy(World, blueprint, civilizationId, core.RegionId, position);
        var bootstrapEvent = new EntityBootstrapped(
            tick,
            entity.Id,
            civilizationId,
            blueprint.Name,
            energyCost,
            materialCost);

        return new BootstrapResult(
            true,
            entity.Id,
            SimulationErrorCode.None,
            $"Restored {blueprint.Name} v{blueprint.Version} from the seed core.",
            bootstrapEvent);
    }
}
