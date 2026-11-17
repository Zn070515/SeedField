using System.Text.Json;
using SeedField.Simulation;

namespace SeedField.Runtime.Js;

internal static class ObservationJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(Observation observation) =>
        JsonSerializer.Serialize(ToDto(observation), Options);

    private static ObservationDto ToDto(Observation observation) =>
        new(
            observation.Tick,
            observation.CivilizationId,
            [.. observation.Entities.Select(entity => new EntityDto(
                entity.EntityId,
                entity.CivilizationId,
                entity.RegionId,
                entity.Position.X,
                entity.Position.Y,
                entity.Health,
                entity.MaxHealth,
                entity.IsSelf,
                entity.HasSensor,
                entity.SensorRangeMm,
                entity.HasWeapon,
                entity.WeaponRangeMm,
                entity.HasDrive,
                entity.HasComm))],
            [.. observation.Messages.Select(message => new MessageDto(
                message.MessageId,
                message.FromEntityId,
                message.FromCivilizationId,
                message.TargetRegionId,
                message.Topic,
                message.Payload,
                message.SentTick,
                message.DeliveredTick))],
            [.. observation.Shipments.Select(shipment => new ShipmentDto(
                shipment.ShipmentId,
                shipment.FromRegionId,
                shipment.ToRegionId,
                shipment.Amount,
                shipment.SentTick,
                shipment.ArriveTick,
                shipment.Route))],
            [.. observation.EnergyShipments.Select(shipment => new EnergyShipmentDto(
                shipment.ShipmentId,
                shipment.FromRegionId,
                shipment.ToRegionId,
                shipment.Amount,
                shipment.SentTick,
                shipment.ArriveTick,
                shipment.Route))],
            observation.ViewerEntityId,
            [.. (observation.ControlPoints ?? Array.Empty<ObservedControlPoint>())
                .Select(point => new ControlPointDto(
                    point.Id,
                    point.RegionId,
                    point.Position.X,
                    point.Position.Y,
                    point.RadiusMm,
                    point.HoldTicksRequired,
                    point.HoldingCivilizationId,
                    point.HoldTicks,
                    point.OwnerCivilizationId))],
            observation.Match is null
                ? null
                : new MatchDto(
                    observation.Match.CapturedPointsToWin,
                    observation.Match.WinnerCivilizationId),
            [.. (observation.Treaties ?? Array.Empty<ObservedTreaty>())
                .Select(treaty => new TreatyDto(
                    treaty.TreatyId,
                    treaty.Kind.ToString(),
                    treaty.OtherCivilizationId,
                    treaty.SignedTick))],
            [.. (observation.TreatyProposals ?? Array.Empty<ObservedTreatyProposal>())
                .Select(proposal => new TreatyProposalDto(
                    proposal.ProposalId,
                    proposal.Kind.ToString(),
                    proposal.ProposerCivilizationId,
                    proposal.TargetCivilizationId,
                    proposal.ProposedTick,
                    proposal.ExpiresTick))],
            [.. (observation.TradeOffers ?? Array.Empty<ObservedTradeOffer>())
                .Select(offer => new TradeOfferDto(
                    offer.TradeOfferId,
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
                    offer.Round))]);

    private sealed record ObservationDto(
        long Tick,
        string CivilizationId,
        List<EntityDto> Entities,
        List<MessageDto> Messages,
        List<ShipmentDto> Shipments,
        List<EnergyShipmentDto> EnergyShipments,
        long? ViewerEntityId,
        List<ControlPointDto> ControlPoints,
        MatchDto? Match,
        List<TreatyDto> Treaties,
        List<TreatyProposalDto> TreatyProposals,
        List<TradeOfferDto> TradeOffers);

    private sealed record TreatyDto(
        long TreatyId,
        string Kind,
        string OtherCivilizationId,
        long SignedTick);

    private sealed record TreatyProposalDto(
        long ProposalId,
        string Kind,
        string ProposerCivilizationId,
        string TargetCivilizationId,
        long ProposedTick,
        long ExpiresTick);

    private sealed record TradeOfferDto(
        long TradeOfferId,
        string ProposerCivilizationId,
        string TargetCivilizationId,
        string RegionId,
        long GiveMaterial,
        long GiveEnergy,
        long WantMaterial,
        long WantEnergy,
        long ProposedTick,
        long ExpiresTick,
        long NegotiationId,
        int Round);

    private sealed record MatchDto(
        int CapturedPointsToWin,
        string? WinnerCivilizationId);

    private sealed record ControlPointDto(
        string Id,
        string RegionId,
        int X,
        int Y,
        int RadiusMm,
        int HoldTicksRequired,
        string? HoldingCivilizationId,
        int HoldTicks,
        string? OwnerCivilizationId);

    private sealed record ShipmentDto(
        long ShipmentId,
        string FromRegionId,
        string ToRegionId,
        long Amount,
        long SentTick,
        long ArriveTick,
        string Route);

    private sealed record EnergyShipmentDto(
        long ShipmentId,
        string FromRegionId,
        string ToRegionId,
        long Amount,
        long SentTick,
        long ArriveTick,
        string Route);

    private sealed record EntityDto(
        long EntityId,
        string CivilizationId,
        string RegionId,
        int X,
        int Y,
        int Health,
        int MaxHealth,
        bool IsSelf,
        bool HasSensor,
        int SensorRangeMm,
        bool HasWeapon,
        int WeaponRangeMm,
        bool HasDrive,
        bool HasComm);

    private sealed record MessageDto(
        long MessageId,
        long FromEntityId,
        string FromCivilizationId,
        string TargetRegionId,
        string Topic,
        string Payload,
        long SentTick,
        long DeliveredTick);
}
