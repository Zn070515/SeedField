using SeedField.Simulation;

namespace SeedField.Game;

/// <summary>
/// Decides whether a simulation event may be shown to civilization A. This is a
/// read-only presentation filter; the authoritative event stream is unchanged.
/// </summary>
public static class PlayerEventFilter
{
    public static bool IsVisible(
        SimulationEvent simulationEvent,
        IReadOnlySet<long> visibleEntityIds,
        bool omniscient)
    {
        if (omniscient)
        {
            return true;
        }

        return simulationEvent switch
        {
            ShotFired shot =>
                visibleEntityIds.Contains(shot.AttackerId.Value) &&
                visibleEntityIds.Contains(shot.TargetId.Value),
            ShotMissed missed =>
                visibleEntityIds.Contains(missed.AttackerId.Value) &&
                visibleEntityIds.Contains(missed.TargetId.Value),
            CombatLootGranted loot =>
                loot.CivilizationId == "A" ||
                visibleEntityIds.Contains(loot.SourceEntityId.Value),
            EntityDamaged damaged => visibleEntityIds.Contains(damaged.EntityId.Value),
            EntityDestroyed destroyed => visibleEntityIds.Contains(destroyed.EntityId.Value),
            EntityMoved moved => visibleEntityIds.Contains(moved.EntityId.Value),
            EntityInventoryLost lost => visibleEntityIds.Contains(lost.EntityId.Value),
            EntityDeployed deployed => deployed.CivilizationId == "A",
            EntityUpgraded upgraded => upgraded.CivilizationId == "A",
            EntityBootstrapped bootstrapped => bootstrapped.CivilizationId == "A",
            ConstructionQueued queued => queued.CivilizationId == "A",
            ConstructionCancelled cancelled => cancelled.CivilizationId == "A",
            ConstructionFailed failed => failed.CivilizationId == "A",
            // Incoming traffic plus any transmission from an entity the player can see.
            MessageQueued queued =>
                queued.ToCivilizationId == "A" ||
                visibleEntityIds.Contains(queued.FromEntityId.Value),
            MessageDelivered delivered =>
                delivered.ToCivilizationId == "A" ||
                visibleEntityIds.Contains(delivered.FromEntityId.Value),
            MessageExpired expired => expired.CivilizationId == "A",
            ControlPointCaptured => true,
            MatchEnded => true,
            CoreRegenerated regenerated => regenerated.CivilizationId == "A",
            CoreMaintained maintained => maintained.CivilizationId == "A",
            RecoveryQuotaDenied denied => denied.CivilizationId == "A",
            PowerFlowed flowed => flowed.CivilizationId == "A",
            PowerBlackout blackout => blackout.CivilizationId == "A",
            PowerRestored restored => restored.CivilizationId == "A",
            BreakerLockedOut lockedOut => lockedOut.CivilizationId == "A",
            BreakerMaintained maintained => maintained.CivilizationId == "A",
            PowerShipped shipped => shipped.CivilizationId == "A",
            PowerArrived arrived => arrived.CivilizationId == "A",
            PowerShortfall shortfall =>
                shortfall.CivilizationId == "A" &&
                visibleEntityIds.Contains(shortfall.EntityId.Value),
            EnergyAutoDrawn drawn => visibleEntityIds.Contains(drawn.EntityId.Value),
            EnergyDrawn drawn => visibleEntityIds.Contains(drawn.EntityId.Value),
            EnergyStored stored => visibleEntityIds.Contains(stored.EntityId.Value),
            EnergyWasted wasted => wasted.CivilizationId == "A",
            MaterialShipped shipped => visibleEntityIds.Contains(shipped.FromEntityId.Value),
            EnergyShipped shipped => visibleEntityIds.Contains(shipped.FromEntityId.Value),
            WreckCreated wreck => wreck.OwnerCivilizationId == "A",
            WreckOccupied occupied => occupied.CivilizationId == "A",
            WreckSalvaged salvaged => salvaged.CivilizationId == "A",
            RuinImported imported => imported.CivilizationId == "A",
            TreatyProposed proposed => InvolvesA(
                proposed.ProposerCivilizationId,
                proposed.TargetCivilizationId),
            TreatyAccepted accepted => InvolvesA(
                accepted.CivilizationA,
                accepted.CivilizationB),
            TreatyRejected rejected => InvolvesA(
                rejected.ProposerCivilizationId,
                rejected.TargetCivilizationId),
            TreatyBroken broken => InvolvesA(
                broken.CivilizationA,
                broken.CivilizationB),
            TreatyProposalExpired expired => InvolvesA(
                expired.ProposerCivilizationId,
                expired.TargetCivilizationId),
            TradeProposed proposed => InvolvesA(
                proposed.ProposerCivilizationId,
                proposed.TargetCivilizationId),
            TradeAccepted accepted => InvolvesA(
                accepted.ProposerCivilizationId,
                accepted.TargetCivilizationId),
            TradeRejected rejected => InvolvesA(
                rejected.ProposerCivilizationId,
                rejected.TargetCivilizationId),
            TradeCancelled cancelled => InvolvesA(
                cancelled.ProposerCivilizationId,
                cancelled.TargetCivilizationId),
            TradeExpired expired => InvolvesA(
                expired.ProposerCivilizationId,
                expired.TargetCivilizationId),
            TradeCountered countered => countered.CivilizationId == "A",
            IntentRejected rejected => visibleEntityIds.Contains(rejected.EntityId.Value),
            _ => false,
        };
    }

    private static bool InvolvesA(string first, string second) =>
        first == "A" || second == "A";
}
