using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main
{
    private static string Describe(SimulationEvent simulationEvent) =>
        simulationEvent switch
        {
            ShotFired shot =>
                $"t{shot.Tick,4}  {shot.AttackerId} → {shot.TargetId}   hit {shot.Damage}   −{shot.EnergySpent}E",
            ShotMissed missed =>
                $"t{missed.Tick,4}  {missed.AttackerId} → {missed.TargetId}   miss   −{missed.EnergySpent}E",
            CombatLootGranted loot =>
                $"t{loot.Tick,4}  {loot.CivilizationId} looted {loot.Material}M " +
                $"from {loot.SourceEntityId} in {loot.RegionId}",
            EntityDestroyed destroyed => $"t{destroyed.Tick,4}  {destroyed.EntityId} destroyed",
            MessageDelivered delivered =>
                $"t{delivered.Tick,4}  message {delivered.MessageId} delivered to {delivered.ToCivilizationId}",
            MessageQueued queued =>
                $"t{queued.Tick,4}  message {queued.MessageId} queued → t{queued.DeliverTick}",
            CoreRegenerated core =>
                $"t{core.Tick,4}  core {core.CivilizationId} +{core.EnergyAdded}E +{core.MaterialAdded}M",
            EntityBootstrapped bootstrapped =>
                $"t{bootstrapped.Tick,4}  {bootstrapped.BlueprintName} bootstrapped from core",
            ControlPointCaptured captured =>
                $"t{captured.Tick,4}  {captured.ControlPointId} captured by {captured.CivilizationId}",
            MatchEnded ended =>
                $"t{ended.Tick,4}  match won by {ended.CivilizationId} ({ended.CapturedPoints} points)",
            TreatyProposed proposed =>
                $"t{proposed.Tick,4}  {proposed.ProposerCivilizationId} → " +
                $"{proposed.TargetCivilizationId} propose {KindLabel(proposed.Kind)}",
            TreatyAccepted accepted =>
                $"t{accepted.Tick,4}  {accepted.CivilizationA} ↔ " +
                $"{accepted.CivilizationB} {KindLabel(accepted.Kind)} accepted",
            TreatyRejected rejected =>
                $"t{rejected.Tick,4}  {rejected.ProposerCivilizationId} → " +
                $"{rejected.TargetCivilizationId} {KindLabel(rejected.Kind)} rejected",
            TreatyBroken broken =>
                $"t{broken.Tick,4}  {KindLabel(broken.Kind)} broken by " +
                $"{broken.BrokenByCivilizationId}",
            TreatyProposalExpired expired =>
                $"t{expired.Tick,4}  {KindLabel(expired.Kind)} proposal " +
                $"{expired.ProposalId} expired",
            TradeProposed proposed =>
                $"t{proposed.Tick,4}  trade {proposed.TradeOfferId} proposed: " +
                $"give M{proposed.GiveMaterial}/E{proposed.GiveEnergy} → " +
                $"want M{proposed.WantMaterial}/E{proposed.WantEnergy}",
            TradeAccepted accepted =>
                $"t{accepted.Tick,4}  trade {accepted.TradeOfferId} settled " +
                $"M{accepted.GiveMaterial + accepted.WantMaterial}/" +
                $"E{accepted.GiveEnergy + accepted.WantEnergy}",
            TradeRejected rejected =>
                $"t{rejected.Tick,4}  trade {rejected.TradeOfferId} rejected; " +
                $"refund M{rejected.RefundedMaterial}/E{rejected.RefundedEnergy}",
            TradeCancelled cancelled =>
                $"t{cancelled.Tick,4}  trade {cancelled.TradeOfferId} cancelled; " +
                $"refund M{cancelled.RefundedMaterial}/E{cancelled.RefundedEnergy}",
            TradeExpired expired =>
                $"t{expired.Tick,4}  trade {expired.TradeOfferId} expired; " +
                $"refund M{expired.RefundedMaterial}/E{expired.RefundedEnergy}",
            TradeCountered countered =>
                $"t{countered.Tick,4}  trade {countered.OriginalTradeOfferId} → " +
                $"{countered.NewTradeOfferId} countered by {countered.CivilizationId} " +
                $"R{countered.Round} give M{countered.GiveMaterial}/E{countered.GiveEnergy} → " +
                $"want M{countered.WantMaterial}/E{countered.WantEnergy}",
            PowerFlowed flowed =>
                $"t{flowed.Tick,4}  power {flowed.CivilizationId} " +
                $"{flowed.FromRegionId} → {flowed.ToRegionId} +{flowed.Amount}E",
            PowerShipped shipped =>
                $"t{shipped.Tick,4}  power {shipped.CivilizationId} " +
                $"{shipped.FromRegionId} → {shipped.ToRegionId} " +
                $"{shipped.Amount}E shipped · arrives t{shipped.ArriveTick}",
            PowerArrived arrived =>
                $"t{arrived.Tick,4}  power {arrived.CivilizationId} arrived " +
                $"{arrived.ToRegionId} +{arrived.Amount}E" +
                (arrived.LostAmount > 0 ? $" (−{arrived.LostAmount}E loss)" : string.Empty),
            PowerShortfall shortfall =>
                $"t{shortfall.Tick,4}  {shortfall.CivilizationId} power shortfall " +
                $"{shortfall.EntityId} in {shortfall.RegionId}: need {shortfall.Requested}E, " +
                $"have {shortfall.Available}E ({shortfall.Source})",
            EnergyAutoDrawn autoDrawn =>
                $"t{autoDrawn.Tick,4}  {autoDrawn.EntityId} auto-drew {autoDrawn.Amount}E " +
                $"from {autoDrawn.RegionId}",
            EnergyWasted wasted =>
                $"t{wasted.Tick,4}  {wasted.CivilizationId} wasted {wasted.Amount}E " +
                $"in {wasted.RegionId} ({wasted.Source})",
            PowerBlackout blackout =>
                $"t{blackout.Tick,4}  {blackout.CivilizationId} breaker opened " +
                $"in {blackout.RegionId}",
            PowerRestored restored =>
                $"t{restored.Tick,4}  {restored.CivilizationId} breaker closed " +
                $"in {restored.RegionId} (grid {restored.GridEnergy})",
            BreakerLockedOut lockedOut =>
                $"t{lockedOut.Tick,4}  {lockedOut.CivilizationId} breaker locked out " +
                $"in {lockedOut.RegionId} after {lockedOut.TripCount} trips " +
                $"({lockedOut.Reason})",
            BreakerMaintained maintained =>
                $"t{maintained.Tick,4}  {maintained.CivilizationId} breaker maintained " +
                $"in {maintained.RegionId} (−{maintained.MaterialSpent}M)",
            RecoveryQuotaDenied quota =>
                $"t{quota.Tick,4}  {quota.CivilizationId} recovery quota exhausted " +
                $"in {quota.RegionId} ({quota.Used}/{quota.Limit}, reset in {quota.ResetsInTicks}t)",
            WreckCreated wreck =>
                $"t{wreck.Tick,4}  wreck {wreck.WreckId} in {wreck.RegionId}: " +
                $"{wreck.BlueprintId} v{wreck.BlueprintVersion} · {wreck.MaterialRecoverable}M",
            WreckSalvaged salvaged =>
                $"t{salvaged.Tick,4}  {salvaged.CivilizationId} salvaged wreck " +
                $"{salvaged.WreckId} in {salvaged.RegionId} (+{salvaged.MaterialRecovered}M)",
            _ => $"t{simulationEvent.Tick,4}  {simulationEvent.GetType().Name}",
        };

    internal static string KindLabel(TreatyKind kind) =>
        kind == TreatyKind.NonAggression ? "NAP" : "TRADE";
}
