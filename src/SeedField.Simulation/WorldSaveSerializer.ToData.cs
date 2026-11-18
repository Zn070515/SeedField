namespace SeedField.Simulation;

public static partial class WorldSaveSerializer
{
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
                world.Statistics.MaterialRefundedTotal,
                world.Statistics.TradeMaterialSettledTotal,
                world.Statistics.TradeEnergySettledTotal,
                world.Statistics.EnergyFlowedTotal,
                world.Statistics.EnergyOverflowedTotal,
                world.Statistics.EnergyFlowLossTotal,
                world.Statistics.EnergyAutoDrawnTotal,
                world.Statistics.EnergyWastedTotal,
                world.Statistics.MaterialSalvagedTotal,
                world.Statistics.ShotsMissed,
                world.Statistics.MaterialLootedTotal,
                world.Statistics.MaterialMinedTotal),
            world.Workshop.GetAllVersions().Select(ToBlueprintSave).ToArray(),
            world.Links.Values
                .Select(link => new LinkSaveData(
                    link.Id,
                    link.RegionA,
                    link.RegionB,
                    link.LatencyTicks,
                    link.MessagesPerTick,
                    link.MaterialPerTick,
                    link.EnergyPerTick,
                    link.PowerPerTick,
                    link.PowerLatencyTicks,
                    link.PowerLossPercent))
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
            world.NextConstructionOrderId,
            world.MessagePolicies.Values
                .Select(policy => new MessagePolicySaveData(
                    policy.CivilizationId,
                    policy.AllowAllInbound,
                    policy.BlockedCivilizations.ToArray(),
                    policy.BlockedTopics.ToArray(),
                    policy.AllowAllTopics,
                    policy.AllowedCivilizations.ToArray(),
                    policy.AllowedTopics.ToArray()))
                .ToArray(),
            world.TreatyProposals.Values
                .OrderBy(proposal => proposal.Id)
                .Select(proposal => new TreatyProposalSaveData(
                    proposal.Id,
                    proposal.Kind,
                    proposal.ProposerCivilizationId,
                    proposal.TargetCivilizationId,
                    proposal.ProposedTick,
                    proposal.ExpiresTick))
                .ToArray(),
            world.Treaties.Values
                .OrderBy(treaty => treaty.Id)
                .Select(treaty => new TreatySaveData(
                    treaty.Id,
                    treaty.Kind,
                    treaty.CivilizationA,
                    treaty.CivilizationB,
                    treaty.ProposerCivilizationId,
                    treaty.SignedTick))
                .ToArray(),
            world.TradeOffers.Values
                .OrderBy(offer => offer.Id)
                .Select(offer => new TradeOfferSaveData(
                    offer.Id,
                    offer.ProposerCivilizationId,
                    offer.TargetCivilizationId,
                    offer.RegionId,
                    offer.GiveMaterial,
                    offer.GiveEnergy,
                    offer.WantMaterial,
                    offer.WantEnergy,
                    offer.ProposedTick,
                    offer.ExpiresTick,
                    offer.NegotiationId,
                    offer.Round))
                .ToArray(),
            world.NextTreatyProposalId,
            world.NextTreatyId,
            world.NextTradeOfferId,
            world.PowerPolicies.Values
                .OrderBy(policy => policy.CivilizationId, StringComparer.Ordinal)
                .Select(policy => new PowerPolicySaveData(
                    policy.CivilizationId,
                    policy.ReserveEnergy,
                    policy.HighPriorityThreshold,
                    policy.PriorityReserveEnergy,
                    policy.AutoReconnect,
                    policy.BreakerTripLimit,
                    policy.BreakerMaintenanceMaterialCost,
                    policy.BreakerFaultChancePercent))
                .ToArray(),
            world.InTransitPower
                .OrderBy(shipment => shipment.Id)
                .Select(shipment => new PowerShipmentSaveData(
                    shipment.Id,
                    shipment.CivilizationId,
                    shipment.FromRegionId,
                    shipment.ToRegionId,
                    shipment.Route,
                    shipment.Amount,
                    shipment.DeliveredAmount,
                    shipment.SentTick,
                    shipment.ArriveTick))
                .ToArray(),
            world.NextPowerShipmentId,
            world.Regions.Values
                .SelectMany(region => region.GridCapacities.Select(capacity =>
                    new GridCapacitySaveData(
                        region.Id,
                        capacity.Key,
                        capacity.Value)))
                .OrderBy(capacity => capacity.RegionId, StringComparer.Ordinal)
                .ThenBy(capacity => capacity.CivilizationId, StringComparer.Ordinal)
                .ToArray(),
            world.PowerGridStates.Values
                .OrderBy(state => state.RegionId, StringComparer.Ordinal)
                .ThenBy(state => state.CivilizationId, StringComparer.Ordinal)
                .Select(state => new PowerGridStateSaveData(
                    state.RegionId,
                    state.CivilizationId,
                    state.BreakerOpen,
                    state.ManualHold,
                    state.TripCount,
                    state.MaintenanceRequired))
                .ToArray(),
            world.Random.Seed,
            world.Random.State,
            world.Wrecks.Values
                .OrderBy(wreck => wreck.Id)
                .Select(wreck => new WreckSaveData(
                    wreck.Id,
                    wreck.RegionId,
                    wreck.Position.X,
                    wreck.Position.Y,
                    wreck.OwnerCivilizationId,
                    wreck.BlueprintId,
                    wreck.BlueprintVersion,
                    wreck.MaterialRecoverable,
                    wreck.CreatedTick,
                    wreck.OccupiedByCivilizationId,
                    wreck.OccupiedTick))
                .ToArray(),
            world.NextWreckId,
            world.WreckRecoveryPercent,
            world.CombatHitChancePercent,
            world.CombatDamageVariancePercent,
            world.CombatLootChancePercent,
            world.CombatLootMaterial,
            world.Deposits.Values
                .OrderBy(deposit => deposit.Id)
                .Select(deposit => new DepositSaveData(
                    deposit.Id,
                    deposit.RegionId,
                    deposit.Kind,
                    deposit.Position.X,
                    deposit.Position.Y,
                    deposit.RadiusMm,
                    deposit.Remaining,
                    deposit.YieldPerTick))
                .ToArray(),
            world.NextDepositId);
    }
}
