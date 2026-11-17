namespace SeedField.Simulation;

public static partial class WorldSaveSerializer
{
    private static void LoadDiplomacy(WorldState world, WorldSaveData data)
    {
        var maxTreatyProposalId = 0L;
        foreach (var proposalData in data.TreatyProposals ?? Array.Empty<TreatyProposalSaveData>())
        {
            if (proposalData.Id <= 0 ||
                !Enum.IsDefined(proposalData.Kind) ||
                string.IsNullOrWhiteSpace(proposalData.ProposerCivilizationId) ||
                string.IsNullOrWhiteSpace(proposalData.TargetCivilizationId) ||
                string.Equals(
                    proposalData.ProposerCivilizationId,
                    proposalData.TargetCivilizationId,
                    StringComparison.Ordinal) ||
                proposalData.ExpiresTick <= proposalData.ProposedTick)
            {
                throw Invalid(
                    "INVALID_TREATY_PROPOSAL",
                    $"Treaty proposal '{proposalData.Id}' is invalid.");
            }

            if (world.TreatyProposals.ContainsKey(proposalData.Id))
            {
                throw Invalid(
                    "DUPLICATE_TREATY_PROPOSAL",
                    $"Treaty proposal '{proposalData.Id}' appears twice.");
            }

            world.TreatyProposals.Add(
                proposalData.Id,
                new TreatyProposalState(
                    proposalData.Id,
                    proposalData.Kind,
                    proposalData.ProposerCivilizationId,
                    proposalData.TargetCivilizationId,
                    proposalData.ProposedTick,
                    proposalData.ExpiresTick));
            maxTreatyProposalId = Math.Max(maxTreatyProposalId, proposalData.Id);
        }

        world.NextTreatyProposalId = ResolveNextId(
            data.NextTreatyProposalId,
            maxTreatyProposalId,
            "INVALID_NEXT_TREATY_PROPOSAL_ID");

        var maxTreatyId = 0L;
        foreach (var treatyData in data.Treaties ?? Array.Empty<TreatySaveData>())
        {
            if (treatyData.Id <= 0 ||
                !Enum.IsDefined(treatyData.Kind) ||
                string.IsNullOrWhiteSpace(treatyData.CivilizationA) ||
                string.IsNullOrWhiteSpace(treatyData.CivilizationB) ||
                string.Equals(
                    treatyData.CivilizationA,
                    treatyData.CivilizationB,
                    StringComparison.Ordinal) ||
                treatyData.SignedTick < 0)
            {
                throw Invalid("INVALID_TREATY", $"Treaty '{treatyData.Id}' is invalid.");
            }

            if (world.Treaties.ContainsKey(treatyData.Id))
            {
                throw Invalid(
                    "DUPLICATE_TREATY",
                    $"Treaty '{treatyData.Id}' appears twice.");
            }

            world.Treaties.Add(
                treatyData.Id,
                new TreatyState(
                    treatyData.Id,
                    treatyData.Kind,
                    treatyData.CivilizationA,
                    treatyData.CivilizationB,
                    treatyData.ProposerCivilizationId,
                    treatyData.SignedTick));
            maxTreatyId = Math.Max(maxTreatyId, treatyData.Id);
        }

        world.NextTreatyId = ResolveNextId(
            data.NextTreatyId,
            maxTreatyId,
            "INVALID_NEXT_TREATY_ID");

        var maxTradeOfferId = 0L;
        foreach (var offerData in data.TradeOffers ?? Array.Empty<TradeOfferSaveData>())
        {
            if (offerData.Id <= 0 ||
                string.IsNullOrWhiteSpace(offerData.ProposerCivilizationId) ||
                string.IsNullOrWhiteSpace(offerData.TargetCivilizationId) ||
                string.Equals(
                    offerData.ProposerCivilizationId,
                    offerData.TargetCivilizationId,
                    StringComparison.Ordinal) ||
                !world.Regions.ContainsKey(offerData.RegionId) ||
                offerData.GiveMaterial < 0 ||
                offerData.GiveEnergy < 0 ||
                offerData.WantMaterial < 0 ||
                offerData.WantEnergy < 0 ||
                (offerData.GiveMaterial == 0 && offerData.GiveEnergy == 0) ||
                (offerData.WantMaterial == 0 && offerData.WantEnergy == 0) ||
                offerData.ExpiresTick <= offerData.ProposedTick)
            {
                throw Invalid(
                    "INVALID_TRADE_OFFER",
                    $"Trade offer '{offerData.Id}' is invalid.");
            }

            if (world.TradeOffers.ContainsKey(offerData.Id))
            {
                throw Invalid(
                    "DUPLICATE_TRADE_OFFER",
                    $"Trade offer '{offerData.Id}' appears twice.");
            }

            var hasTradeTreaty = world.Treaties.Values.Any(treaty =>
                treaty.Kind == TreatyKind.Trade &&
                treaty.Involves(offerData.ProposerCivilizationId) &&
                treaty.Involves(offerData.TargetCivilizationId));
            if (!hasTradeTreaty)
            {
                throw Invalid(
                    "TRADE_OFFER_WITHOUT_TREATY",
                    $"Trade offer '{offerData.Id}' has no active trade treaty.");
            }

            var negotiationId = offerData.NegotiationId > 0
                ? offerData.NegotiationId
                : offerData.Id;
            var round = offerData.Round > 0 ? offerData.Round : 1;
            world.TradeOffers.Add(
                offerData.Id,
                new TradeOfferState(
                    offerData.Id,
                    offerData.ProposerCivilizationId,
                    offerData.TargetCivilizationId,
                    offerData.RegionId,
                    offerData.GiveMaterial,
                    offerData.GiveEnergy,
                    offerData.WantMaterial,
                    offerData.WantEnergy,
                    negotiationId,
                    round,
                    offerData.ProposedTick,
                    offerData.ExpiresTick));
            maxTradeOfferId = Math.Max(maxTradeOfferId, offerData.Id);
        }

        world.NextTradeOfferId = ResolveNextId(
            data.NextTradeOfferId,
            maxTradeOfferId,
            "INVALID_NEXT_TRADE_OFFER_ID");
    }
}
