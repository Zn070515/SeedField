using System.Collections.Generic;

namespace SeedField.Simulation;

/// <summary>Derived counters used for diagnostics and conservation tests.</summary>
public sealed class SimulationStatistics
{
    public long EnergySpentTotal { get; internal set; }

    public long EnergyRegeneratedTotal { get; internal set; }

    public long EnergyGeneratedTotal { get; internal set; }

    public long MaterialSpentTotal { get; internal set; }

    public long MaterialRegeneratedTotal { get; internal set; }

    public long ShotsFired { get; internal set; }

    public long DamageDealt { get; internal set; }

    public long EntitiesDestroyed { get; internal set; }

    public long MessagesDelivered { get; internal set; }

    public long MessagesExpired { get; internal set; }

    public long MaterialShippedTotal { get; internal set; }

    public long MaterialDeliveredTotal { get; internal set; }

    public long EnergyShippedTotal { get; internal set; }

    public long EnergyDeliveredTotal { get; internal set; }

    public long EnergyStoredTotal { get; internal set; }

    public long EnergyDrawnTotal { get; internal set; }

    public long EnergyLostTotal { get; internal set; }

    public long MaterialLostTotal { get; internal set; }

    public long MaterialRefundedTotal { get; internal set; }

    public long ConstructionsStartedTotal { get; internal set; }

    public long ConstructionsCompletedTotal { get; internal set; }

    public long TradeMaterialSettledTotal { get; internal set; }

    public long TradeEnergySettledTotal { get; internal set; }

    public long EnergyFlowedTotal { get; internal set; }

    public long EnergyOverflowedTotal { get; internal set; }

    public long EnergyFlowLossTotal { get; internal set; }

    public long EnergyAutoDrawnTotal { get; internal set; }

    public long EnergyWastedTotal { get; internal set; }
}

/// <summary>
/// Single authoritative world state. UI and controllers never receive mutable
/// references to this object.
/// </summary>
public sealed class WorldState
{
    public long Tick { get; internal set; }

    public long NextEntityId { get; internal set; } = 1;

    public long NextMessageId { get; internal set; } = 1;

    public long NextShipmentId { get; internal set; } = 1;

    public long NextEnergyShipmentId { get; internal set; } = 1;

    public long NextConstructionOrderId { get; internal set; } = 1;

    public long NextTreatyProposalId { get; internal set; } = 1;

    public long NextTreatyId { get; internal set; } = 1;

    public long NextTradeOfferId { get; internal set; } = 1;

    public long NextPowerShipmentId { get; internal set; } = 1;

    public SortedDictionary<string, RegionState> Regions { get; } = new(StringComparer.Ordinal);

    /// <summary>Explicit bidirectional region links.</summary>
    public SortedDictionary<string, RegionLink> Links { get; } = new(StringComparer.Ordinal);

    public SortedDictionary<string, SeedCoreState> Cores { get; } = new(StringComparer.Ordinal);

    public SortedDictionary<string, MessagePolicyState> MessagePolicies { get; } =
        new(StringComparer.Ordinal);

    /// <summary>Per-civilization regional grid policies (reserve floor).</summary>
    public SortedDictionary<string, PowerPolicyState> PowerPolicies { get; } =
        new(StringComparer.Ordinal);

    /// <summary>Persistent breaker state keyed by region and civilization.</summary>
    public SortedDictionary<(string RegionId, string CivilizationId), PowerGridState>
        PowerGridStates { get; } = new();

    /// <summary>Immutable workshop versions available to deployments in this world.</summary>
    public WorkshopService Workshop { get; } = new();

    /// <summary>Optional capture objectives for a scenario, sorted by id.</summary>
    public SortedDictionary<string, ControlPointState> ControlPoints { get; } =
        new(StringComparer.Ordinal);

    /// <summary>Optional match/victory rules for a scenario.</summary>
    public MatchState? Match { get; internal set; }

    public List<MessageEnvelope> InFlightMessages { get; } = new();

    public List<MaterialShipment> InTransitMaterial { get; } = new();

    public List<EnergyShipment> InTransitEnergy { get; } = new();

    public List<PowerShipment> InTransitPower { get; } = new();

    public List<ConstructionOrder> ConstructionOrders { get; } = new();

    /// <summary>Pending treaty proposals by stable id.</summary>
    public SortedDictionary<long, TreatyProposalState> TreatyProposals { get; } = new();

    /// <summary>Active bilateral treaties by stable id.</summary>
    public SortedDictionary<long, TreatyState> Treaties { get; } = new();

    /// <summary>Active trade offers by stable id. Escrow is already deducted.</summary>
    public SortedDictionary<long, TradeOfferState> TradeOffers { get; } = new();

    /// <summary>Delivered messages per civilization, in delivery order.</summary>
    public SortedDictionary<string, List<DeliveredMessage>> Inboxes { get; } = new(StringComparer.Ordinal);

    public SimulationStatistics Statistics { get; } = new();

    public EntityState? FindEntity(EntityId id)
    {
        foreach (var region in Regions.Values)
        {
            if (region.Entities.TryGetValue(id, out var entity))
            {
                return entity;
            }
        }

        return null;
    }

    public EntityState? FindEntity(EntityId id, out RegionState? region)
    {
        foreach (var candidate in Regions.Values)
        {
            if (candidate.Entities.TryGetValue(id, out var entity))
            {
                region = candidate;
                return entity;
            }
        }

        region = null;
        return null;
    }

    public IEnumerable<EntityState> EnumerateEntities()
    {
        foreach (var region in Regions.Values)
        {
            foreach (var entity in region.Entities.Values)
            {
                yield return entity;
            }
        }
    }

    public IReadOnlyList<DeliveredMessage> GetInbox(string civilizationId) =>
        Inboxes.TryGetValue(civilizationId, out var inbox)
            ? inbox
            : Array.Empty<DeliveredMessage>();
}
