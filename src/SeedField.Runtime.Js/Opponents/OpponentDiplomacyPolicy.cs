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
    long EnergyValue = 1,
    bool ProposesNonAggression = false,
    bool ProposesTradeAgreement = false,
    bool ProposesTradeOffers = false,
    int ProposalIntervalTicks = 240,
    int OfferIntervalTicks = 240,
    int CounterConcessionPercent = 0,
    long OfferGiveMaterial = 0,
    long OfferGiveEnergy = 0,
    long OfferWantMaterial = 0,
    long OfferWantEnergy = 0,
    int ProposalExpiryTicks = 200,
    int OfferExpiryTicks = 200,
    int CounterExpiryTicks = 200);

public static class OpponentDiplomacyProfiles
{
    /// <summary>Aggressive raider: refuses NAP, trades only at a profit.</summary>
    public static DiplomacyProfile Aggressive { get; } = new(
        AcceptsNonAggression: false,
        AcceptsTradeAgreement: true,
        AcceptsTradeOffers: true,
        MinimumTradeValuePercent: 120,
        ProposesTradeAgreement: true,
        ProposesTradeOffers: true,
        CounterConcessionPercent: 10,
        OfferGiveMaterial: 20,
        OfferWantMaterial: 30);

    /// <summary>Defensive opponent: accepts NAP and fair trades.</summary>
    public static DiplomacyProfile Cautious { get; } = new(
        AcceptsNonAggression: true,
        AcceptsTradeAgreement: true,
        AcceptsTradeOffers: true,
        MinimumTradeValuePercent: 100,
        ProposesNonAggression: true,
        ProposesTradeAgreement: true,
        ProposesTradeOffers: true,
        OfferGiveMaterial: 20,
        OfferWantMaterial: 20);

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
            if (ShouldAcceptTradeOffer(world, civilizationId, offer, profile))
            {
                commands.Add(new RespondTradeCommand(civilizationId, offer.Id, Accept: true));
                continue;
            }

            var counter = TryBuildCounter(
                world,
                civilizationId,
                offer,
                profile);
            commands.Add(counter is not null
                ? counter
                : new RespondTradeCommand(civilizationId, offer.Id, Accept: false));
        }

        if (commands.Count == 0)
        {
            var proactive = BuildProactive(
                world,
                civilizationId,
                opponentCivilizationId,
                profile);
            if (proactive is not null)
            {
                commands.Add(proactive);
            }
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
               received * 100 >= paid * EffectivePercent(profile, offer.Round);
    }

    private static CounterTradeCommand? TryBuildCounter(
        WorldState world,
        string civilizationId,
        TradeOfferState offer,
        DiplomacyProfile profile)
    {
        if (!profile.AcceptsTradeOffers ||
            offer.Round >= TradeNegotiationRules.MaxRounds ||
            !world.Regions.TryGetValue(offer.RegionId, out var region))
        {
            return null;
        }

        var ratio = EffectivePercent(profile, offer.Round);
        var materialOnly = offer.GiveMaterial > 0 &&
                           offer.GiveEnergy == 0 &&
                           offer.WantMaterial > 0 &&
                           offer.WantEnergy == 0;
        var energyOnly = offer.GiveMaterial == 0 &&
                         offer.GiveEnergy > 0 &&
                         offer.WantMaterial == 0 &&
                         offer.WantEnergy > 0;

        if (materialOnly)
        {
            var give = offer.WantMaterial;
            var want = ScaleByPercent(give, ratio);
            if (want is null ||
                region.GetMaterial(civilizationId) < give)
            {
                return null;
            }

            return new CounterTradeCommand(
                civilizationId,
                offer.Id,
                give,
                0,
                want.Value,
                0,
                profile.CounterExpiryTicks);
        }

        if (energyOnly)
        {
            var give = offer.WantEnergy;
            var want = ScaleByPercent(give, ratio);
            if (want is null ||
                region.GetEnergy(civilizationId) < give)
            {
                return null;
            }

            return new CounterTradeCommand(
                civilizationId,
                offer.Id,
                0,
                give,
                0,
                want.Value,
                profile.CounterExpiryTicks);
        }

        return null;
    }

    private static DiplomacyCommand? BuildProactive(
        WorldState world,
        string civilizationId,
        string opponentCivilizationId,
        DiplomacyProfile profile)
    {
        if (ShouldProposeNow(world.Tick, profile.ProposalIntervalTicks))
        {
            if (profile.ProposesNonAggression &&
                !HasTreatyOrProposal(
                    world,
                    TreatyKind.NonAggression,
                    civilizationId,
                    opponentCivilizationId))
            {
                return new ProposeTreatyCommand(
                    civilizationId,
                    opponentCivilizationId,
                    TreatyKind.NonAggression,
                    profile.ProposalExpiryTicks);
            }

            if (profile.ProposesTradeAgreement &&
                !HasTreatyOrProposal(
                    world,
                    TreatyKind.Trade,
                    civilizationId,
                    opponentCivilizationId))
            {
                return new ProposeTreatyCommand(
                    civilizationId,
                    opponentCivilizationId,
                    TreatyKind.Trade,
                    profile.ProposalExpiryTicks);
            }
        }

        if (!ShouldProposeNow(world.Tick, profile.OfferIntervalTicks) ||
            !profile.ProposesTradeOffers ||
            !HasActiveTreaty(
                world,
                TreatyKind.Trade,
                civilizationId,
                opponentCivilizationId) ||
            world.TradeOffers.Values.Any(offer => offer.Involves(civilizationId)) ||
            (profile.OfferGiveMaterial == 0 && profile.OfferGiveEnergy == 0) ||
            (profile.OfferWantMaterial == 0 && profile.OfferWantEnergy == 0))
        {
            return null;
        }

        var region = world.Regions.Values.FirstOrDefault(region =>
            region.GetMaterial(civilizationId) >= profile.OfferGiveMaterial &&
            region.GetEnergy(civilizationId) >= profile.OfferGiveEnergy);
        if (region is null)
        {
            return null;
        }

        return new ProposeTradeCommand(
            civilizationId,
            opponentCivilizationId,
            region.Id,
            profile.OfferGiveMaterial,
            profile.OfferGiveEnergy,
            profile.OfferWantMaterial,
            profile.OfferWantEnergy,
            profile.OfferExpiryTicks);
    }

    private static bool ShouldProposeNow(long tick, int interval) =>
        interval > 0 && tick > 0 && tick % interval == 0;

    private static bool HasTreatyOrProposal(
        WorldState world,
        TreatyKind kind,
        string civilizationId,
        string opponentCivilizationId) =>
        world.Treaties.Values.Any(treaty =>
            treaty.Kind == kind &&
            treaty.Involves(civilizationId) &&
            treaty.Involves(opponentCivilizationId)) ||
        world.TreatyProposals.Values.Any(proposal =>
            proposal.Kind == kind &&
            proposal.Involves(civilizationId) &&
            proposal.Involves(opponentCivilizationId));

    private static bool HasActiveTreaty(
        WorldState world,
        TreatyKind kind,
        string civilizationId,
        string opponentCivilizationId) =>
        world.Treaties.Values.Any(treaty =>
            treaty.Kind == kind &&
            treaty.Involves(civilizationId) &&
            treaty.Involves(opponentCivilizationId));

    private static int EffectivePercent(DiplomacyProfile profile, int round) =>
        Math.Max(
            100,
            profile.MinimumTradeValuePercent -
            Math.Max(0, round - 1) * profile.CounterConcessionPercent);

    private static long? ScaleByPercent(long value, int percent)
    {
        var scaled = (Int128)value * percent;
        var rounded = (scaled + 99) / 100;
        return rounded > long.MaxValue ? null : (long)rounded;
    }
}
