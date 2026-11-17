namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    /// <summary>
    /// Unified host entry for treaties and trade. Device scripts never reach this path.
    /// </summary>
    public DiplomacyCommandResult ExecuteDiplomacyCommand(DiplomacyCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var events = new List<SimulationEvent>();
        ExpireDiplomacy(World.Tick, events);

        return command switch
        {
            ProposeTreatyCommand propose => ProposeTreaty(propose, events),
            RespondTreatyCommand respond => RespondTreaty(respond, events),
            BreakTreatyCommand breakTreaty => BreakTreaty(breakTreaty, events),
            ProposeTradeCommand proposeTrade => ProposeTrade(proposeTrade, events),
            RespondTradeCommand respondTrade => RespondTrade(respondTrade, events),
            CancelTradeCommand cancelTrade => CancelTrade(cancelTrade, events),
            CounterTradeCommand counterTrade => CounterTrade(counterTrade, events),
            _ => new DiplomacyCommandResult(
                false,
                null,
                null,
                null,
                SimulationErrorCode.InvalidAction,
                "Unknown diplomacy command.",
                events),
        };
    }

    private DiplomacyCommandResult BreakTreaty(
        BreakTreatyCommand command,
        List<SimulationEvent> events)
    {
        if (!World.Treaties.TryGetValue(command.TreatyId, out var treaty) ||
            !treaty.Involves(command.CivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TreatyNotFound,
                "Treaty does not exist for this civilization.",
                events);
        }

        World.Treaties.Remove(treaty.Id);
        events.Add(new TreatyBroken(
            World.Tick,
            treaty.Id,
            treaty.Kind,
            treaty.CivilizationA,
            treaty.CivilizationB,
            command.CivilizationId));

        if (treaty.Kind == TreatyKind.Trade)
        {
            var cancelledOffers = World.TradeOffers.Values
                .Where(offer =>
                    offer.Involves(treaty.CivilizationA) &&
                    offer.Involves(treaty.CivilizationB))
                .OrderBy(offer => offer.Id)
                .ToList();
            foreach (var offer in cancelledOffers)
            {
                var refunded = RefundTradeOffer(offer);
                World.TradeOffers.Remove(offer.Id);
                events.Add(new TradeCancelled(
                    World.Tick,
                    offer.Id,
                    offer.ProposerCivilizationId,
                    offer.TargetCivilizationId,
                    command.CivilizationId,
                    refunded.Material,
                    refunded.Energy));
            }
        }

        return new DiplomacyCommandResult(
            true,
            treaty.Id,
            null,
            null,
            SimulationErrorCode.None,
            $"Treaty {treaty.Id} broken.",
            events);
    }

    private DiplomacyCommandResult ProposeTrade(
        ProposeTradeCommand command,
        List<SimulationEvent> events)
    {
        if (string.IsNullOrWhiteSpace(command.CivilizationId) ||
            string.IsNullOrWhiteSpace(command.TargetCivilizationId) ||
            string.IsNullOrWhiteSpace(command.RegionId) ||
            string.Equals(
                command.CivilizationId,
                command.TargetCivilizationId,
                StringComparison.Ordinal) ||
            command.GiveMaterial < 0 ||
            command.GiveEnergy < 0 ||
            command.WantMaterial < 0 ||
            command.WantEnergy < 0 ||
            (command.GiveMaterial == 0 && command.GiveEnergy == 0) ||
            (command.WantMaterial == 0 && command.WantEnergy == 0) ||
            command.ExpiresInTicks <= 0)
        {
            return DiplomacyFailure(
                SimulationErrorCode.InvalidAction,
                "Trade offer needs two different civilizations, non-negative goods, " +
                "both sides non-empty and a positive expiry.",
                events);
        }

        if (!HasActiveTreaty(
                TreatyKind.Trade,
                command.CivilizationId,
                command.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TreatyDenied,
                "A trade treaty is required.",
                events);
        }

        if (!HasCommunicationPath(command.CivilizationId, command.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoRoute,
                "No communication path between the civilizations.",
                events);
        }

        if (!World.Regions.TryGetValue(command.RegionId, out var region))
        {
            return DiplomacyFailure(
                SimulationErrorCode.InvalidAction,
                $"Trade region '{command.RegionId}' does not exist.",
                events);
        }

        if (region.GetMaterial(command.CivilizationId) < command.GiveMaterial)
        {
            return DiplomacyFailure(
                SimulationErrorCode.InsufficientMaterial,
                "Not enough material to escrow the trade offer.",
                events);
        }

        if (region.GetEnergy(command.CivilizationId) < command.GiveEnergy)
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoEnergy,
                "Not enough energy to escrow the trade offer.",
                events);
        }

        region.TryConsumeMaterial(command.CivilizationId, command.GiveMaterial);
        region.TryConsumeEnergy(command.CivilizationId, command.GiveEnergy);

        var tick = World.Tick;
        var offerId = World.NextTradeOfferId++;
        var offer = new TradeOfferState(
            offerId,
            command.CivilizationId,
            command.TargetCivilizationId,
            command.RegionId,
            command.GiveMaterial,
            command.GiveEnergy,
            command.WantMaterial,
            command.WantEnergy,
            offerId,
            1,
            tick,
            tick + command.ExpiresInTicks);
        World.TradeOffers.Add(offer.Id, offer);
        events.Add(new TradeProposed(
            tick,
            offer.Id,
            offer.ProposerCivilizationId,
            offer.TargetCivilizationId,
            offer.RegionId,
            offer.GiveMaterial,
            offer.GiveEnergy,
            offer.WantMaterial,
            offer.WantEnergy,
            offer.ExpiresTick));

        return new DiplomacyCommandResult(
            true,
            null,
            null,
            offer.Id,
            SimulationErrorCode.None,
            $"Trade offer {offer.Id} created.",
            events);
    }

    private DiplomacyCommandResult RespondTrade(
        RespondTradeCommand command,
        List<SimulationEvent> events)
    {
        if (!World.TradeOffers.TryGetValue(command.TradeOfferId, out var offer) ||
            !string.Equals(
                offer.TargetCivilizationId,
                command.CivilizationId,
                StringComparison.Ordinal))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TradeNotFound,
                "Trade offer does not exist for this civilization.",
                events);
        }

        if (!HasCommunicationPath(
                offer.ProposerCivilizationId,
                offer.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoRoute,
                "No communication path between the civilizations.",
                events);
        }

        if (!command.Accept)
        {
            var refunded = RefundTradeOffer(offer);
            World.TradeOffers.Remove(offer.Id);
            events.Add(new TradeRejected(
                World.Tick,
                offer.Id,
                offer.ProposerCivilizationId,
                offer.TargetCivilizationId,
                refunded.Material,
                refunded.Energy));
            return new DiplomacyCommandResult(
                true,
                null,
                null,
                offer.Id,
                SimulationErrorCode.None,
                $"Trade offer {offer.Id} rejected.",
                events);
        }

        if (!HasActiveTreaty(
                TreatyKind.Trade,
                offer.ProposerCivilizationId,
                offer.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TreatyDenied,
                "A trade treaty is required.",
                events);
        }

        if (!World.Regions.TryGetValue(offer.RegionId, out var region))
        {
            return DiplomacyFailure(
                SimulationErrorCode.InvalidAction,
                $"Trade region '{offer.RegionId}' does not exist.",
                events);
        }

        if (region.GetMaterial(offer.TargetCivilizationId) < offer.WantMaterial)
        {
            return DiplomacyFailure(
                SimulationErrorCode.InsufficientMaterial,
                "Target civilization lacks the requested material.",
                events);
        }

        if (region.GetEnergy(offer.TargetCivilizationId) < offer.WantEnergy)
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoEnergy,
                "Target civilization lacks the requested energy.",
                events);
        }

        region.TryConsumeMaterial(offer.TargetCivilizationId, offer.WantMaterial);
        region.TryConsumeEnergy(offer.TargetCivilizationId, offer.WantEnergy);

        region.AddMaterial(offer.TargetCivilizationId, offer.GiveMaterial);
        region.AddEnergy(offer.TargetCivilizationId, offer.GiveEnergy);
        region.AddMaterial(offer.ProposerCivilizationId, offer.WantMaterial);
        region.AddEnergy(offer.ProposerCivilizationId, offer.WantEnergy);

        World.Statistics.TradeMaterialSettledTotal +=
            offer.GiveMaterial + offer.WantMaterial;
        World.Statistics.TradeEnergySettledTotal +=
            offer.GiveEnergy + offer.WantEnergy;

        World.TradeOffers.Remove(offer.Id);
        events.Add(new TradeAccepted(
            World.Tick,
            offer.Id,
            offer.ProposerCivilizationId,
            offer.TargetCivilizationId,
            offer.RegionId,
            offer.GiveMaterial,
            offer.GiveEnergy,
            offer.WantMaterial,
            offer.WantEnergy));

        return new DiplomacyCommandResult(
            true,
            null,
            null,
            offer.Id,
            SimulationErrorCode.None,
            $"Trade offer {offer.Id} accepted.",
            events);
    }

    private DiplomacyCommandResult CancelTrade(
        CancelTradeCommand command,
        List<SimulationEvent> events)
    {
        if (!World.TradeOffers.TryGetValue(command.TradeOfferId, out var offer) ||
            !string.Equals(
                offer.ProposerCivilizationId,
                command.CivilizationId,
                StringComparison.Ordinal))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TradeNotFound,
                "Trade offer does not exist for this civilization.",
                events);
        }

        var refunded = RefundTradeOffer(offer);
        World.TradeOffers.Remove(offer.Id);
        events.Add(new TradeCancelled(
            World.Tick,
            offer.Id,
            offer.ProposerCivilizationId,
            offer.TargetCivilizationId,
            command.CivilizationId,
            refunded.Material,
            refunded.Energy));

        return new DiplomacyCommandResult(
            true,
            null,
            null,
            offer.Id,
            SimulationErrorCode.None,
            $"Trade offer {offer.Id} cancelled.",
            events);
    }

    private DiplomacyCommandResult CounterTrade(
        CounterTradeCommand command,
        List<SimulationEvent> events)
    {
        if (!World.TradeOffers.TryGetValue(command.TradeOfferId, out var original) ||
            !string.Equals(
                original.TargetCivilizationId,
                command.CivilizationId,
                StringComparison.Ordinal))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TradeNotFound,
                "Trade offer does not exist for this civilization.",
                events);
        }

        if (original.Round >= TradeNegotiationRules.MaxRounds)
        {
            return DiplomacyFailure(
                SimulationErrorCode.NegotiationExhausted,
                "Trade negotiation has reached its round limit.",
                events);
        }

        if (command.GiveMaterial < 0 ||
            command.GiveEnergy < 0 ||
            command.WantMaterial < 0 ||
            command.WantEnergy < 0 ||
            (command.GiveMaterial == 0 && command.GiveEnergy == 0) ||
            (command.WantMaterial == 0 && command.WantEnergy == 0) ||
            command.ExpiresInTicks <= 0)
        {
            return DiplomacyFailure(
                SimulationErrorCode.InvalidAction,
                "Counter offer needs non-negative goods, both sides non-empty and a positive expiry.",
                events);
        }

        if (!HasCommunicationPath(
                command.CivilizationId,
                original.ProposerCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoRoute,
                "No communication path between the civilizations.",
                events);
        }

        if (!HasActiveTreaty(
                TreatyKind.Trade,
                command.CivilizationId,
                original.ProposerCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TreatyDenied,
                "A trade treaty is required.",
                events);
        }

        if (!World.Regions.TryGetValue(original.RegionId, out var region))
        {
            return DiplomacyFailure(
                SimulationErrorCode.InvalidAction,
                $"Trade region '{original.RegionId}' does not exist.",
                events);
        }

        if (region.GetMaterial(command.CivilizationId) < command.GiveMaterial)
        {
            return DiplomacyFailure(
                SimulationErrorCode.InsufficientMaterial,
                "Not enough material to escrow the counter offer.",
                events);
        }

        if (region.GetEnergy(command.CivilizationId) < command.GiveEnergy)
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoEnergy,
                "Not enough energy to escrow the counter offer.",
                events);
        }

        var refunded = RefundTradeOffer(original);
        World.TradeOffers.Remove(original.Id);
        region.TryConsumeMaterial(command.CivilizationId, command.GiveMaterial);
        region.TryConsumeEnergy(command.CivilizationId, command.GiveEnergy);

        var tick = World.Tick;
        var offerId = World.NextTradeOfferId++;
        var offer = new TradeOfferState(
            offerId,
            command.CivilizationId,
            original.ProposerCivilizationId,
            original.RegionId,
            command.GiveMaterial,
            command.GiveEnergy,
            command.WantMaterial,
            command.WantEnergy,
            original.NegotiationId,
            original.Round + 1,
            tick,
            tick + command.ExpiresInTicks);
        World.TradeOffers.Add(offer.Id, offer);
        events.Add(new TradeCountered(
            tick,
            original.Id,
            offer.Id,
            command.CivilizationId,
            offer.NegotiationId,
            offer.Round,
            refunded.Material,
            refunded.Energy,
            offer.GiveMaterial,
            offer.GiveEnergy,
            offer.WantMaterial,
            offer.WantEnergy));

        return new DiplomacyCommandResult(
            true,
            null,
            null,
            offer.Id,
            SimulationErrorCode.None,
            $"Trade offer {original.Id} countered with {offer.Id}.",
            events);
    }

    private (long Material, long Energy) RefundTradeOffer(TradeOfferState offer)
    {
        if (!World.Regions.TryGetValue(offer.RegionId, out var region))
        {
            return (0, 0);
        }

        region.AddMaterial(offer.ProposerCivilizationId, offer.GiveMaterial);
        region.AddEnergy(offer.ProposerCivilizationId, offer.GiveEnergy);
        return (offer.GiveMaterial, offer.GiveEnergy);
    }

    private void ExpireDiplomacy(long tick, List<SimulationEvent> events)
    {
        var expiredProposals = World.TreatyProposals.Values
            .Where(proposal => proposal.ExpiresTick <= tick)
            .OrderBy(proposal => proposal.Id)
            .ToList();
        foreach (var proposal in expiredProposals)
        {
            World.TreatyProposals.Remove(proposal.Id);
            events.Add(new TreatyProposalExpired(
                tick,
                proposal.Id,
                proposal.Kind,
                proposal.ProposerCivilizationId,
                proposal.TargetCivilizationId));
        }

        var expiredOffers = World.TradeOffers.Values
            .Where(offer => offer.ExpiresTick <= tick)
            .OrderBy(offer => offer.Id)
            .ToList();
        foreach (var offer in expiredOffers)
        {
            var refunded = RefundTradeOffer(offer);
            World.TradeOffers.Remove(offer.Id);
            events.Add(new TradeExpired(
                tick,
                offer.Id,
                offer.ProposerCivilizationId,
                offer.TargetCivilizationId,
                refunded.Material,
                refunded.Energy));
        }
    }

}
