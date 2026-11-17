using SeedField.Simulation;

namespace SeedField.Runtime.Js;

/// <summary>
/// Host-side diplomacy profile. Values are prototype scenario choices, not final balance.
/// </summary>
public sealed record DiplomacyProfile(
    bool AcceptsNonAggression,
    bool AcceptsTradeAgreement,
    bool AcceptsTradeOffers,
    int MinimumTradeValuePercent,
    long MaterialValue = 1,
    long EnergyValue = 1);

public static class OpponentDiplomacyProfiles
{
    /// <summary>Aggressive raider: refuses NAP, trades only at a profit.</summary>
    public static DiplomacyProfile Aggressive { get; } = new(
        AcceptsNonAggression: false,
        AcceptsTradeAgreement: true,
        AcceptsTradeOffers: true,
        MinimumTradeValuePercent: 120);

    /// <summary>Defensive opponent: accepts NAP and fair trades.</summary>
    public static DiplomacyProfile Cautious { get; } = new(
        AcceptsNonAggression: true,
        AcceptsTradeAgreement: true,
        AcceptsTradeOffers: true,
        MinimumTradeValuePercent: 100);

    public static DiplomacyProfile Default => Cautious;

    public static DiplomacyProfile ForPackage(string packageId) =>
        packageId switch
        {
            "builtin.skirmisher" => Aggressive,
            "builtin.turtle" => Cautious,
            _ => Default,
        };
}

/// <summary>
/// Deterministic responder for one civilization. It only emits host commands; it never
/// mutates the world and has no hidden state.
/// </summary>
public static class OpponentDiplomacyPolicy
{
    public static IReadOnlyList<DiplomacyCommand> Decide(
        WorldState world,
        string civilizationId,
        string opponentCivilizationId,
        bool canCommunicate,
        DiplomacyProfile profile)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(profile);

        if (!canCommunicate)
        {
            return Array.Empty<DiplomacyCommand>();
        }

        var commands = new List<DiplomacyCommand>();

        foreach (var proposal in world.TreatyProposals.Values
                     .Where(proposal =>
                         string.Equals(
                             proposal.TargetCivilizationId,
                             civilizationId,
                             StringComparison.Ordinal) &&
                         string.Equals(
                             proposal.ProposerCivilizationId,
                             opponentCivilizationId,
                             StringComparison.Ordinal))
                     .OrderBy(proposal => proposal.Id))
        {
            var accept = proposal.Kind switch
            {
                TreatyKind.NonAggression => profile.AcceptsNonAggression,
                TreatyKind.Trade => profile.AcceptsTradeAgreement,
                _ => false,
            };
            commands.Add(new RespondTreatyCommand(civilizationId, proposal.Id, accept));
        }

        foreach (var offer in world.TradeOffers.Values
                     .Where(offer =>
                         string.Equals(
                             offer.TargetCivilizationId,
                             civilizationId,
                             StringComparison.Ordinal) &&
                         string.Equals(
                             offer.ProposerCivilizationId,
                             opponentCivilizationId,
                             StringComparison.Ordinal))
                     .OrderBy(offer => offer.Id))
        {
            commands.Add(new RespondTradeCommand(
                civilizationId,
                offer.Id,
                ShouldAcceptTradeOffer(world, civilizationId, offer, profile)));
        }

        return commands;
    }

    private static bool ShouldAcceptTradeOffer(
        WorldState world,
        string civilizationId,
        TradeOfferState offer,
        DiplomacyProfile profile)
    {
        if (!profile.AcceptsTradeOffers)
        {
            return false;
        }

        var hasTradeTreaty = world.Treaties.Values.Any(treaty =>
            treaty.Kind == TreatyKind.Trade &&
            treaty.Involves(civilizationId) &&
            treaty.Involves(offer.ProposerCivilizationId));
        if (!hasTradeTreaty)
        {
            return false;
        }

        if (!world.Regions.TryGetValue(offer.RegionId, out var region))
        {
            return false;
        }

        if (region.GetMaterial(civilizationId) < offer.WantMaterial ||
            region.GetEnergy(civilizationId) < offer.WantEnergy)
        {
            return false;
        }

        var received = (Int128)offer.GiveMaterial * profile.MaterialValue +
                       (Int128)offer.GiveEnergy * profile.EnergyValue;
        var paid = (Int128)offer.WantMaterial * profile.MaterialValue +
                   (Int128)offer.WantEnergy * profile.EnergyValue;
        return paid > 0 &&
               received * 100 >= paid * profile.MinimumTradeValuePercent;
    }
}
