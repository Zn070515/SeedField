namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    private DeploymentCommandResult ExecuteCancelConstruction(
        CancelConstructionCommand cancel,
        long tick)
    {
        var order = World.ConstructionOrders.FirstOrDefault(candidate =>
            candidate.Id == cancel.OrderId);
        if (order is null ||
            !string.Equals(
                order.CivilizationId,
                cancel.CivilizationId,
                StringComparison.Ordinal))
        {
            return new DeploymentCommandResult(
                false,
                null,
                SimulationErrorCode.DeploymentRejected,
                $"Construction order '{cancel.OrderId}' is not available to this civilization.",
                Array.Empty<SimulationEvent>());
        }

        World.ConstructionOrders.Remove(order);

        if (World.Regions.TryGetValue(order.RegionId, out var region))
        {
            region.AddMaterial(order.CivilizationId, order.MaterialPaid);
            region.AddComponents(order.CivilizationId, order.ComponentsPaid);
        }

        World.Statistics.MaterialRefundedTotal += order.MaterialPaid;
        World.Statistics.ComponentsRefundedTotal += order.ComponentsPaid;

        var cancelled = new ConstructionCancelled(
            tick,
            order.Id,
            order.CivilizationId,
            order.BlueprintId,
            order.MaterialPaid,
            order.ComponentsPaid);

        return new DeploymentCommandResult(
            true,
            null,
            SimulationErrorCode.None,
            $"Cancelled order {order.Id}; refunded {order.MaterialPaid} M.",
            new SimulationEvent[] { cancelled });
    }

    private DeploymentCommandResult ExecuteUpgrade(UpgradeCommand upgrade, long tick)
    {
        var entity = World.FindEntity(upgrade.EntityId);
        if (entity is null)
        {
            return new DeploymentCommandResult(
                false,
                null,
                SimulationErrorCode.DeploymentRejected,
                $"Entity '{upgrade.EntityId}' does not exist.",
                Array.Empty<SimulationEvent>());
        }

        if (!string.Equals(entity.CivilizationId, upgrade.CivilizationId, StringComparison.Ordinal))
        {
            return new DeploymentCommandResult(
                false,
                null,
                SimulationErrorCode.DeploymentRejected,
                "Entity does not belong to this civilization.",
                Array.Empty<SimulationEvent>());
        }

        var oldVersion = entity.BlueprintVersion;
        DeploymentService.Upgrade(World, entity, upgrade.Blueprint);
        var upgraded = new EntityUpgraded(
            tick,
            entity.Id,
            entity.CivilizationId,
            upgrade.Blueprint.BlueprintId,
            oldVersion,
            upgrade.Blueprint.Version);

        return new DeploymentCommandResult(
            true,
            entity.Id,
            SimulationErrorCode.None,
            $"Upgraded {upgrade.Blueprint.BlueprintId} v{oldVersion} -> v{upgrade.Blueprint.Version}.",
            new SimulationEvent[] { upgraded });
    }

    private static SimulationErrorCode MapDeploymentError(string code) =>
        code switch
        {
            "CORE_PROTECTED" => SimulationErrorCode.CoreProtected,
            "INSUFFICIENT_MATERIAL" => SimulationErrorCode.InsufficientMaterial,
            "INSUFFICIENT_COMPONENTS" => SimulationErrorCode.InsufficientComponents,
            "OUT_OF_BOUNDS" => SimulationErrorCode.OutOfBounds,
            _ => SimulationErrorCode.DeploymentRejected,
        };

    private void DeliverDueConstructions(long tick, List<SimulationEvent> events)
    {
        if (World.ConstructionOrders.Count == 0)
        {
            return;
        }

        var due = World.ConstructionOrders
            .Where(order => order.CompleteTick <= tick)
            .OrderBy(order => order.CompleteTick)
            .ThenBy(order => order.Id)
            .ToList();

        foreach (var order in due)
        {
            World.ConstructionOrders.Remove(order);
            var blueprint = World.Workshop.GetVersion(
                order.BlueprintId,
                order.BlueprintVersion);
            if (blueprint is null)
            {
                var refunded = RefundConstruction(order);
                events.Add(new ConstructionFailed(
                    tick,
                    order.Id,
                    order.CivilizationId,
                    order.BlueprintId,
                    "Blueprint version is missing.",
                    refunded.Material,
                    refunded.Components));
                continue;
            }

            try
            {
                DeploymentService.Validate(
                    World,
                    blueprint,
                    order.CivilizationId,
                    order.RegionId,
                    order.Position);
                var entity = DeploymentService.CreateInstance(
                    World,
                    blueprint,
                    order.CivilizationId,
                    order.RegionId,
                    order.Position);
                World.Statistics.ConstructionsCompletedTotal += 1;
                events.Add(new EntityDeployed(
                    tick,
                    entity.Id,
                    order.CivilizationId,
                    blueprint.BlueprintId,
                    blueprint.Version,
                    entity.RegionId,
                    entity.Position));
            }
            catch (BlueprintValidationException exception)
            {
                var refunded = RefundConstruction(order);
                events.Add(new ConstructionFailed(
                    tick,
                    order.Id,
                    order.CivilizationId,
                    order.BlueprintId,
                    exception.Message,
                    refunded.Material,
                    refunded.Components));
            }
        }
    }

    private (long Material, long Components) RefundConstruction(ConstructionOrder order)
    {
        if (!World.Regions.TryGetValue(order.RegionId, out var region))
        {
            return (0, 0);
        }

        region.AddMaterial(order.CivilizationId, order.MaterialPaid);
        region.AddComponents(order.CivilizationId, order.ComponentsPaid);
        World.Statistics.MaterialRefundedTotal += order.MaterialPaid;
        World.Statistics.ComponentsRefundedTotal += order.ComponentsPaid;
        return (order.MaterialPaid, order.ComponentsPaid);
    }

}
