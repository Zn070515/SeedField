namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    private void ProcessMove(
        EntityState entity,
        RegionState region,
        MoveIntent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results)
    {
        if (entity.Drive is null)
        {
            Reject(intent, SimulationErrorCode.NoDrive, "Entity has no drive module.", tick, events, results);
            return;
        }

        if (!region.Contains(intent.Target))
        {
            Reject(intent, SimulationErrorCode.OutOfBounds, "Move target is outside the region.", tick, events, results);
            return;
        }

        var from = entity.Position;
        var to = from.MoveToward(intent.Target, entity.Drive.SpeedMmPerTick);
        if (to == from)
        {
            Accept(intent, "Entity is already at the target.", results);
            return;
        }

        var cost = entity.Drive.EnergyCostPerMove;
        if (entity.Energy < cost &&
            !TryAutoDrawForAction(entity, region, cost, tick, events))
        {
            AddPowerShortfall(
                entity,
                region.Id,
                cost,
                ActionAvailableEnergy(entity, region),
                ActionShortfallSource(entity, region, cost),
                tick,
                events);
            Reject(intent, SimulationErrorCode.NoEnergy, "Not enough energy to move.", tick, events, results);
            return;
        }

        entity.Energy -= cost;
        World.Statistics.EnergySpentTotal += cost;
        entity.Position = to;

        events.Add(new EntityMoved(tick, entity.Id, from, to, cost));
        Accept(intent, $"Moved to {to}.", results);
    }

    private void ProcessAttack(
        EntityState entity,
        RegionState region,
        AttackIntent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results)
    {
        if (entity.Weapon is null)
        {
            Reject(intent, SimulationErrorCode.NoWeapon, "Entity has no weapon module.", tick, events, results);
            return;
        }

        if (intent.TargetId == entity.Id)
        {
            Reject(intent, SimulationErrorCode.InvalidAction, "An entity cannot target itself.", tick, events, results);
            return;
        }

        var target = World.FindEntity(intent.TargetId, out var targetRegion);
        if (target is null || targetRegion is null || targetRegion.Id != region.Id)
        {
            Reject(intent, SimulationErrorCode.TargetUnknown, "Target is not present in this region.", tick, events, results);
            return;
        }

        if (!VisibilityRules.IsVisibleTo(World, entity.CivilizationId, target))
        {
            // Same code as a missing target: guessing an id must not reveal existence.
            Reject(intent, SimulationErrorCode.TargetUnknown, "Target is not visible.", tick, events, results);
            return;
        }

        if (!string.Equals(
                entity.CivilizationId,
                target.CivilizationId,
                StringComparison.Ordinal) &&
            HasActiveTreaty(
                TreatyKind.NonAggression,
                entity.CivilizationId,
                target.CivilizationId))
        {
            Reject(
                intent,
                SimulationErrorCode.TreatyDenied,
                "A non-aggression treaty forbids this attack.",
                tick,
                events,
                results);
            return;
        }

        if (tick < entity.NextWeaponReadyTick)
        {
            Reject(intent, SimulationErrorCode.WeaponCoolingDown, "Weapon is still cooling down.", tick, events, results);
            return;
        }

        if (!entity.Position.IsWithinRange(target.Position, entity.Weapon.RangeMm))
        {
            Reject(intent, SimulationErrorCode.OutOfRange, "Target is outside weapon range.", tick, events, results);
            return;
        }

        var cost = entity.Weapon.EnergyCost;
        if (entity.Energy < cost &&
            !TryAutoDrawForAction(entity, region, cost, tick, events))
        {
            AddPowerShortfall(
                entity,
                region.Id,
                cost,
                ActionAvailableEnergy(entity, region),
                ActionShortfallSource(entity, region, cost),
                tick,
                events);
            Reject(intent, SimulationErrorCode.NoEnergy, "Not enough energy to fire.", tick, events, results);
            return;
        }

        entity.Energy -= cost;
        World.Statistics.EnergySpentTotal += cost;
        World.Statistics.ShotsFired += 1;

        entity.NextWeaponReadyTick = tick + Math.Max(0, entity.Weapon.CooldownTicks);

        var appliedDamage = Math.Min(entity.Weapon.Damage, target.Health);
        target.Health -= appliedDamage;
        World.Statistics.DamageDealt += appliedDamage;

        events.Add(new ShotFired(
            tick,
            entity.Id,
            target.Id,
            appliedDamage,
            entity.Position,
            target.Position,
            cost));
        events.Add(new EntityDamaged(tick, target.Id, appliedDamage, Math.Max(0, target.Health)));

        if (target.Health <= 0)
        {
            targetRegion.Entities.Remove(target.Id);
            World.Statistics.EntitiesDestroyed += 1;
            World.Statistics.EnergyLostTotal += target.Energy;
            World.Statistics.MaterialLostTotal += target.Material;
            events.Add(new EntityDestroyed(tick, target.Id));
            events.Add(new EntityInventoryLost(
                tick,
                target.Id,
                target.Energy,
                target.Material));
            CreateWreck(target, targetRegion, tick, events);
        }

        Accept(intent, $"Hit {target.Id} for {appliedDamage}.", results);
    }

    private void CreateWreck(
        EntityState target,
        RegionState region,
        long tick,
        List<SimulationEvent> events)
    {
        if (World.WreckRecoveryPercent <= 0 || target.BlueprintId is null)
        {
            return;
        }

        var blueprint = World.Workshop.GetVersion(
            target.BlueprintId,
            target.BlueprintVersion);
        if (blueprint is null || blueprint.MaterialCost <= 0)
        {
            return;
        }

        var material = (long)(
            (Int128)blueprint.MaterialCost * World.WreckRecoveryPercent / 100);
        if (material <= 0)
        {
            return;
        }

        var wreckId = World.NextWreckId++;
        World.Wrecks.Add(
            wreckId,
            new WreckState(
                wreckId,
                region.Id,
                target.Position,
                target.CivilizationId,
                target.BlueprintId,
                target.BlueprintVersion,
                material,
                tick));
        events.Add(new WreckCreated(
            tick,
            wreckId,
            region.Id,
            target.CivilizationId,
            target.BlueprintId,
            target.BlueprintVersion,
            material));
    }
}
