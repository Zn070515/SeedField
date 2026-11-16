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

    public SortedDictionary<string, RegionState> Regions { get; } = new(StringComparer.Ordinal);

    /// <summary>Explicit bidirectional region links.</summary>
    public SortedDictionary<string, RegionLink> Links { get; } = new(StringComparer.Ordinal);

    public SortedDictionary<string, SeedCoreState> Cores { get; } = new(StringComparer.Ordinal);

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
