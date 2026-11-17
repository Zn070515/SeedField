using System.Text;

namespace SeedField.Simulation;

/// <summary>
/// Stable FNV-1a 64-bit digest of every field that can affect future simulation
/// results. Used by tests and headless runs to prove reproducibility across runs.
/// </summary>
public static class WorldStateHasher
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Compute(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var hash = OffsetBasis;

        Append(ref hash, world.Tick);
        Append(ref hash, world.NextEntityId);
        Append(ref hash, world.NextMessageId);
        Append(ref hash, world.NextShipmentId);
        Append(ref hash, world.NextEnergyShipmentId);
        Append(ref hash, world.NextConstructionOrderId);
        Append(ref hash, world.NextTreatyProposalId);
        Append(ref hash, world.NextTreatyId);
        Append(ref hash, world.NextTradeOfferId);
        Append(ref hash, world.NextPowerShipmentId);
        Append(ref hash, world.Random.Seed);
        Append(ref hash, world.Random.State);

        Append(ref hash, world.Statistics.EnergySpentTotal);
        Append(ref hash, world.Statistics.EnergyRegeneratedTotal);
        Append(ref hash, world.Statistics.EnergyGeneratedTotal);
        Append(ref hash, world.Statistics.MaterialSpentTotal);
        Append(ref hash, world.Statistics.MaterialRegeneratedTotal);
        Append(ref hash, world.Statistics.ShotsFired);
        Append(ref hash, world.Statistics.DamageDealt);
        Append(ref hash, world.Statistics.EntitiesDestroyed);
        Append(ref hash, world.Statistics.MessagesDelivered);
        Append(ref hash, world.Statistics.MessagesExpired);
        Append(ref hash, world.Statistics.MaterialShippedTotal);
        Append(ref hash, world.Statistics.MaterialDeliveredTotal);
        Append(ref hash, world.Statistics.EnergyShippedTotal);
        Append(ref hash, world.Statistics.EnergyDeliveredTotal);
        Append(ref hash, world.Statistics.EnergyStoredTotal);
        Append(ref hash, world.Statistics.EnergyDrawnTotal);
        Append(ref hash, world.Statistics.EnergyLostTotal);
        Append(ref hash, world.Statistics.MaterialLostTotal);
        Append(ref hash, world.Statistics.ConstructionsStartedTotal);
        Append(ref hash, world.Statistics.ConstructionsCompletedTotal);
        Append(ref hash, world.Statistics.MaterialRefundedTotal);
        Append(ref hash, world.Statistics.TradeMaterialSettledTotal);
        Append(ref hash, world.Statistics.TradeEnergySettledTotal);
        Append(ref hash, world.Statistics.EnergyFlowedTotal);
        Append(ref hash, world.Statistics.EnergyOverflowedTotal);
        Append(ref hash, world.Statistics.EnergyFlowLossTotal);
        Append(ref hash, world.Statistics.EnergyAutoDrawnTotal);
        Append(ref hash, world.Statistics.EnergyWastedTotal);

        Append(ref hash, world.Regions.Count);
        foreach (var region in world.Regions.Values)
        {
            Append(ref hash, region.Id);
            Append(ref hash, region.WidthMm);
            Append(ref hash, region.HeightMm);
            Append(ref hash, region.IsCoreRegion);
            Append(ref hash, region.Stocks.Count);
            foreach (var stock in region.Stocks)
            {
                Append(ref hash, stock.Key);
                Append(ref hash, stock.Value.Material);
                Append(ref hash, stock.Value.Energy);
            }

            Append(ref hash, region.GridCapacities.Count);
            foreach (var capacity in region.GridCapacities)
            {
                Append(ref hash, capacity.Key);
                Append(ref hash, capacity.Value);
            }

            Append(ref hash, region.Entities.Count);

            foreach (var entity in region.Entities.Values)
            {
                Append(ref hash, entity.Id.Value);
                Append(ref hash, entity.CivilizationId);
                Append(ref hash, entity.RegionId);
                Append(ref hash, entity.Position.X);
                Append(ref hash, entity.Position.Y);
                Append(ref hash, entity.MaxHealth);
                Append(ref hash, entity.Health);
                Append(ref hash, entity.Energy);
                Append(ref hash, entity.EnergyCapacity);
                Append(ref hash, entity.Material);
                Append(ref hash, entity.NextWeaponReadyTick);
                Append(ref hash, entity.BlueprintId ?? string.Empty);
                Append(ref hash, entity.BlueprintVersion);
                Append(ref hash, entity.ControllerProgramId ?? string.Empty);

                AppendModule(ref hash, entity.Sensor);
                AppendModule(ref hash, entity.Weapon);
                AppendModule(ref hash, entity.Drive);
                AppendModule(ref hash, entity.Comm);

                Append(ref hash, entity.Power is not null);
                if (entity.Power is not null)
                {
                    Append(ref hash, entity.Power.EnergyPerTick);
                    Append(ref hash, entity.Power.StorageCapacity);
                    Append(ref hash, entity.Power.Priority);
                }

                Append(ref hash, entity.State is not null);
                if (entity.State is not null)
                {
                    Append(ref hash, entity.State.ComputePerTick);
                    Append(ref hash, entity.State.StorageCapacity);
                }
            }
        }

        Append(ref hash, world.ControlPoints.Count);
        foreach (var point in world.ControlPoints.Values)
        {
            Append(ref hash, point.Id);
            Append(ref hash, point.RegionId);
            Append(ref hash, point.Position.X);
            Append(ref hash, point.Position.Y);
            Append(ref hash, point.RadiusMm);
            Append(ref hash, point.HoldTicksRequired);
            Append(ref hash, point.HoldingCivilizationId ?? string.Empty);
            Append(ref hash, point.HoldTicks);
            Append(ref hash, point.OwnerCivilizationId ?? string.Empty);
        }

        Append(ref hash, world.Match is not null);
        if (world.Match is { } match)
        {
            Append(ref hash, match.Rules.CapturedPointsToWin);
            Append(ref hash, match.WinnerCivilizationId ?? string.Empty);
        }

        var shipments = world.InTransitMaterial.OrderBy(shipment => shipment.Id).ToList();
        Append(ref hash, shipments.Count);
        foreach (var shipment in shipments)
        {
            Append(ref hash, shipment.Id);
            Append(ref hash, shipment.FromEntityId.Value);
            Append(ref hash, shipment.CivilizationId);
            Append(ref hash, shipment.FromRegionId);
            Append(ref hash, shipment.ToRegionId);
            Append(ref hash, shipment.Route);
            Append(ref hash, shipment.Amount);
            Append(ref hash, shipment.SentTick);
            Append(ref hash, shipment.ArriveTick);
        }

        var constructionOrders = world.ConstructionOrders
            .OrderBy(order => order.Id)
            .ToList();
        Append(ref hash, constructionOrders.Count);
        foreach (var order in constructionOrders)
        {
            Append(ref hash, order.Id);
            Append(ref hash, order.CivilizationId);
            Append(ref hash, order.BlueprintId);
            Append(ref hash, order.BlueprintVersion);
            Append(ref hash, order.RegionId);
            Append(ref hash, order.Position.X);
            Append(ref hash, order.Position.Y);
            Append(ref hash, order.StartTick);
            Append(ref hash, order.CompleteTick);
            Append(ref hash, order.MaterialPaid);
        }

        Append(ref hash, world.TreatyProposals.Count);
        foreach (var proposal in world.TreatyProposals.Values.OrderBy(p => p.Id))
        {
            Append(ref hash, proposal.Id);
            Append(ref hash, (int)proposal.Kind);
            Append(ref hash, proposal.ProposerCivilizationId);
            Append(ref hash, proposal.TargetCivilizationId);
            Append(ref hash, proposal.ProposedTick);
            Append(ref hash, proposal.ExpiresTick);
        }

        Append(ref hash, world.Treaties.Count);
        foreach (var treaty in world.Treaties.Values.OrderBy(t => t.Id))
        {
            Append(ref hash, treaty.Id);
            Append(ref hash, (int)treaty.Kind);
            Append(ref hash, treaty.CivilizationA);
            Append(ref hash, treaty.CivilizationB);
            Append(ref hash, treaty.ProposerCivilizationId);
            Append(ref hash, treaty.SignedTick);
        }

        Append(ref hash, world.TradeOffers.Count);
        foreach (var offer in world.TradeOffers.Values.OrderBy(o => o.Id))
        {
            Append(ref hash, offer.Id);
            Append(ref hash, offer.ProposerCivilizationId);
            Append(ref hash, offer.TargetCivilizationId);
            Append(ref hash, offer.RegionId);
            Append(ref hash, offer.GiveMaterial);
            Append(ref hash, offer.GiveEnergy);
            Append(ref hash, offer.WantMaterial);
            Append(ref hash, offer.WantEnergy);
            Append(ref hash, offer.NegotiationId);
            Append(ref hash, offer.Round);
            Append(ref hash, offer.ProposedTick);
            Append(ref hash, offer.ExpiresTick);
        }

        var powerShipments = world.InTransitPower.OrderBy(shipment => shipment.Id).ToList();
        Append(ref hash, powerShipments.Count);
        foreach (var shipment in powerShipments)
        {
            Append(ref hash, shipment.Id);
            Append(ref hash, shipment.CivilizationId);
            Append(ref hash, shipment.FromRegionId);
            Append(ref hash, shipment.ToRegionId);
            Append(ref hash, shipment.Route);
            Append(ref hash, shipment.Amount);
            Append(ref hash, shipment.DeliveredAmount);
            Append(ref hash, shipment.SentTick);
            Append(ref hash, shipment.ArriveTick);
        }

        var energyShipments = world.InTransitEnergy
            .OrderBy(shipment => shipment.Id)
            .ToList();
        Append(ref hash, energyShipments.Count);
        foreach (var shipment in energyShipments)
        {
            Append(ref hash, shipment.Id);
            Append(ref hash, shipment.FromEntityId.Value);
            Append(ref hash, shipment.CivilizationId);
            Append(ref hash, shipment.FromRegionId);
            Append(ref hash, shipment.ToRegionId);
            Append(ref hash, shipment.Route);
            Append(ref hash, shipment.Amount);
            Append(ref hash, shipment.SentTick);
            Append(ref hash, shipment.ArriveTick);
        }

        Append(ref hash, world.Links.Count);
        foreach (var link in world.Links.Values)
        {
            Append(ref hash, link.Id);
            Append(ref hash, link.RegionA);
            Append(ref hash, link.RegionB);
            Append(ref hash, link.LatencyTicks);
            Append(ref hash, link.MessagesPerTick);
            Append(ref hash, link.MaterialPerTick);
            Append(ref hash, link.EnergyPerTick);
            Append(ref hash, link.PowerPerTick);
            Append(ref hash, link.PowerLatencyTicks);
            Append(ref hash, link.PowerLossPercent);
        }

        var blueprints = world.Workshop.GetAllVersions()
            .OrderBy(blueprint => blueprint.BlueprintId, StringComparer.Ordinal)
            .ThenBy(blueprint => blueprint.Version)
            .ToList();
        Append(ref hash, blueprints.Count);
        foreach (var blueprint in blueprints)
        {
            Append(ref hash, blueprint.BlueprintId);
            Append(ref hash, blueprint.Version);
            Append(ref hash, blueprint.Name);
            Append(ref hash, blueprint.MaxHealth);
            Append(ref hash, blueprint.EnergyCapacity);
            Append(ref hash, blueprint.ControllerProgramId ?? string.Empty);
            Append(ref hash, blueprint.MaterialCost);
            Append(ref hash, blueprint.BuildTicks);
            AppendModule(ref hash, blueprint.Sensor);
            AppendModule(ref hash, blueprint.Weapon);
            AppendModule(ref hash, blueprint.Drive);
            AppendModule(ref hash, blueprint.Comm);

            Append(ref hash, blueprint.Power is not null);
            if (blueprint.Power is not null)
            {
                Append(ref hash, blueprint.Power.EnergyPerTick);
                Append(ref hash, blueprint.Power.StorageCapacity);
                Append(ref hash, blueprint.Power.Priority);
            }

            Append(ref hash, blueprint.State is not null);
            if (blueprint.State is not null)
            {
                Append(ref hash, blueprint.State.ComputePerTick);
                Append(ref hash, blueprint.State.StorageCapacity);
            }
        }

        Append(ref hash, world.MessagePolicies.Count);
        foreach (var policy in world.MessagePolicies.Values)
        {
            Append(ref hash, policy.CivilizationId);
            Append(ref hash, policy.AllowAllInbound);
            Append(ref hash, policy.BlockedCivilizations.Count);
            foreach (var blocked in policy.BlockedCivilizations)
            {
                Append(ref hash, blocked);
            }

            Append(ref hash, policy.AllowAllTopics);

            Append(ref hash, policy.BlockedTopics.Count);
            foreach (var blocked in policy.BlockedTopics)
            {
                Append(ref hash, blocked);
            }

            Append(ref hash, policy.AllowedCivilizations.Count);
            foreach (var allowed in policy.AllowedCivilizations)
            {
                Append(ref hash, allowed);
            }

            Append(ref hash, policy.AllowedTopics.Count);
            foreach (var allowed in policy.AllowedTopics)
            {
                Append(ref hash, allowed);
            }
        }

        Append(ref hash, world.PowerPolicies.Count);
        foreach (var policy in world.PowerPolicies.Values
                     .OrderBy(policy => policy.CivilizationId, StringComparer.Ordinal))
        {
            Append(ref hash, policy.CivilizationId);
            Append(ref hash, policy.ReserveEnergy);
            Append(ref hash, policy.HighPriorityThreshold);
            Append(ref hash, policy.PriorityReserveEnergy);
            Append(ref hash, policy.AutoReconnect);
            Append(ref hash, policy.BreakerTripLimit);
            Append(ref hash, policy.BreakerMaintenanceMaterialCost);
            Append(ref hash, policy.BreakerFaultChancePercent);
        }

        Append(ref hash, world.PowerGridStates.Count);
        foreach (var state in world.PowerGridStates.Values
                     .OrderBy(state => state.RegionId, StringComparer.Ordinal)
                     .ThenBy(state => state.CivilizationId, StringComparer.Ordinal))
        {
            Append(ref hash, state.RegionId);
            Append(ref hash, state.CivilizationId);
            Append(ref hash, state.BreakerOpen);
            Append(ref hash, state.ManualHold);
            Append(ref hash, state.TripCount);
            Append(ref hash, state.MaintenanceRequired);
        }

        Append(ref hash, world.Cores.Count);
        foreach (var core in world.Cores.Values)
        {
            Append(ref hash, core.CivilizationId);
            Append(ref hash, core.RegionId);
            Append(ref hash, core.EnergyReserve);
            Append(ref hash, core.MaterialReserve);
            Append(ref hash, core.Config.EnergyPerTick);
            Append(ref hash, core.Config.MaterialPerTick);
            Append(ref hash, core.Config.MaxEnergyReserve);
            Append(ref hash, core.Config.MaxMaterialReserve);

            Append(ref hash, core.LatestProducts.Count);
            foreach (var product in core.LatestProducts)
            {
                Append(ref hash, (int)product.Key);
                Append(ref hash, product.Value.BlueprintId);
                Append(ref hash, product.Value.Version);
            }
        }

        var inFlight = world.InFlightMessages.OrderBy(message => message.Id).ToList();
        Append(ref hash, inFlight.Count);
        foreach (var message in inFlight)
        {
            Append(ref hash, message.Id);
            Append(ref hash, message.FromEntityId.Value);
            Append(ref hash, message.FromCivilizationId);
            Append(ref hash, message.ToCivilizationId);
            Append(ref hash, message.TargetRegionId);
            Append(ref hash, message.Topic);
            Append(ref hash, message.Payload);
            Append(ref hash, message.SentTick);
            Append(ref hash, message.DeliverTick);
        }

        Append(ref hash, world.Inboxes.Count);
        foreach (var inbox in world.Inboxes)
        {
            Append(ref hash, inbox.Key);
            var messages = inbox.Value.OrderBy(message => message.Id).ToList();
            Append(ref hash, messages.Count);
            foreach (var message in messages)
            {
                Append(ref hash, message.Id);
                Append(ref hash, message.FromEntityId.Value);
                Append(ref hash, message.FromCivilizationId);
                Append(ref hash, message.TargetRegionId);
                Append(ref hash, message.Topic);
                Append(ref hash, message.Payload);
                Append(ref hash, message.SentTick);
                Append(ref hash, message.DeliveredTick);
            }
        }

        return hash;
    }

    public static string ComputeHex(WorldState world) => Compute(world).ToString("x16");

    private static void AppendModule(ref ulong hash, SensorModule? module)
    {
        Append(ref hash, module is not null);
        if (module is not null)
        {
            Append(ref hash, module.RangeMm);
        }
    }

    private static void AppendModule(ref ulong hash, WeaponModule? module)
    {
        Append(ref hash, module is not null);
        if (module is not null)
        {
            Append(ref hash, module.RangeMm);
            Append(ref hash, module.Damage);
            Append(ref hash, module.EnergyCost);
            Append(ref hash, module.CooldownTicks);
        }
    }

    private static void AppendModule(ref ulong hash, DriveModule? module)
    {
        Append(ref hash, module is not null);
        if (module is not null)
        {
            Append(ref hash, module.SpeedMmPerTick);
            Append(ref hash, module.EnergyCostPerMove);
        }
    }

    private static void AppendModule(ref ulong hash, CommModule? module)
    {
        Append(ref hash, module is not null);
        if (module is not null)
        {
            Append(ref hash, module.LatencyTicks);
            Append(ref hash, module.EnergyCostPerMessage);
            Append(ref hash, module.BandwidthPerTick);
        }
    }

    private static void Append(ref ulong hash, long value)
    {
        for (var shift = 0; shift < 64; shift += 8)
        {
            hash ^= (byte)(value >> shift);
            hash *= Prime;
        }
    }

    private static void Append(ref ulong hash, ulong value) =>
        Append(ref hash, unchecked((long)value));

    private static void Append(ref ulong hash, int value) => Append(ref hash, (long)value);

    private static void Append(ref ulong hash, bool value) => Append(ref hash, value ? 1L : 0L);

    private static void Append(ref ulong hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Append(ref hash, bytes.Length);
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= Prime;
        }
    }
}
