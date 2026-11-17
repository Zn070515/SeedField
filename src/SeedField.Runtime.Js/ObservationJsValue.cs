using Jint;
using Jint.Native;
using SeedField.Simulation;

namespace SeedField.Runtime.Js;

/// <summary>
/// Builds the controller observation as plain JS values using Jint's shared
/// object layouts. In-process controllers avoid JSON text, JSON.parse, and the
/// expander entirely; the worker/IPC boundary keeps using JSON.
/// </summary>
internal static class ObservationJsValue
{
    private static readonly JsObjectLayout EntityLayout = new(new[]
    {
        "entityId", "civilizationId", "regionId", "x", "y", "health", "maxHealth",
        "isSelf", "hasSensor", "sensorRangeMm", "hasWeapon", "weaponRangeMm",
        "hasDrive", "hasComm",
    });

    private static readonly JsObjectLayout MessageLayout = new(new[]
    {
        "messageId", "fromEntityId", "fromCivilizationId", "targetRegionId", "topic",
        "payload", "sentTick", "deliveredTick",
    });

    private static readonly JsObjectLayout ShipmentLayout = new(new[]
    {
        "shipmentId", "fromRegionId", "toRegionId", "amount", "sentTick",
        "arriveTick", "route",
    });

    private static readonly JsObjectLayout ControlPointLayout = new(new[]
    {
        "id", "regionId", "x", "y", "radiusMm", "holdTicksRequired",
        "holdingCivilizationId", "holdTicks", "ownerCivilizationId",
    });

    private static readonly JsObjectLayout MatchLayout = new(new[]
    {
        "capturedPointsToWin", "winnerCivilizationId",
    });

    private static readonly JsObjectLayout TreatyLayout = new(new[]
    {
        "treatyId", "kind", "otherCivilizationId", "signedTick",
    });

    private static readonly JsObjectLayout ProposalLayout = new(new[]
    {
        "proposalId", "kind", "proposerCivilizationId", "targetCivilizationId",
        "proposedTick", "expiresTick",
    });

    private static readonly JsObjectLayout TradeOfferLayout = new(new[]
    {
        "tradeOfferId", "proposerCivilizationId", "targetCivilizationId", "regionId",
        "giveMaterial", "giveEnergy", "wantMaterial", "wantEnergy", "proposedTick",
        "expiresTick", "negotiationId", "round",
    });

    private static readonly JsObjectLayout RootLayout = new(new[]
    {
        "tick", "civilizationId", "entities", "messages", "shipments",
        "energyShipments", "viewerEntityId", "controlPoints", "match", "treaties",
        "treatyProposals", "tradeOffers",
    });

    public static JsObject Build(Engine engine, Observation observation)
    {
        var entities = new JsValue[observation.Entities.Count];
        for (var index = 0; index < entities.Length; index++)
        {
            var entity = observation.Entities[index];
            entities[index] = JsObject.Create(
                engine,
                EntityLayout,
                new JsValue[]
                {
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
                    entity.HasComm,
                });
        }

        var messages = new JsValue[observation.Messages.Count];
        for (var index = 0; index < messages.Length; index++)
        {
            var message = observation.Messages[index];
            messages[index] = JsObject.Create(
                engine,
                MessageLayout,
                new JsValue[]
                {
                    message.MessageId,
                    message.FromEntityId,
                    message.FromCivilizationId,
                    message.TargetRegionId,
                    message.Topic,
                    message.Payload,
                    message.SentTick,
                    message.DeliveredTick,
                });
        }

        var shipments = new JsValue[observation.Shipments.Count];
        for (var index = 0; index < shipments.Length; index++)
        {
            var shipment = observation.Shipments[index];
            shipments[index] = JsObject.Create(
                engine,
                ShipmentLayout,
                new JsValue[]
                {
                    shipment.ShipmentId,
                    shipment.FromRegionId,
                    shipment.ToRegionId,
                    shipment.Amount,
                    shipment.SentTick,
                    shipment.ArriveTick,
                    shipment.Route,
                });
        }

        var energyShipments = new JsValue[observation.EnergyShipments.Count];
        for (var index = 0; index < energyShipments.Length; index++)
        {
            var shipment = observation.EnergyShipments[index];
            energyShipments[index] = JsObject.Create(
                engine,
                ShipmentLayout,
                new JsValue[]
                {
                    shipment.ShipmentId,
                    shipment.FromRegionId,
                    shipment.ToRegionId,
                    shipment.Amount,
                    shipment.SentTick,
                    shipment.ArriveTick,
                    shipment.Route,
                });
        }

        var points = observation.ControlPoints ?? Array.Empty<ObservedControlPoint>();
        var controlPoints = new JsValue[points.Count];
        for (var index = 0; index < controlPoints.Length; index++)
        {
            var point = points[index];
            controlPoints[index] = JsObject.Create(
                engine,
                ControlPointLayout,
                new JsValue[]
                {
                    point.Id,
                    point.RegionId,
                    point.Position.X,
                    point.Position.Y,
                    point.RadiusMm,
                    point.HoldTicksRequired,
                    Nullable(point.HoldingCivilizationId),
                    point.HoldTicks,
                    Nullable(point.OwnerCivilizationId),
                });
        }

        var match = observation.Match is null
            ? JsValue.Null
            : JsObject.Create(
                engine,
                MatchLayout,
                new JsValue[]
                {
                    observation.Match.CapturedPointsToWin,
                    Nullable(observation.Match.WinnerCivilizationId),
                });

        var treaties = observation.Treaties ?? Array.Empty<ObservedTreaty>();
        var treatyValues = new JsValue[treaties.Count];
        for (var index = 0; index < treatyValues.Length; index++)
        {
            var treaty = treaties[index];
            treatyValues[index] = JsObject.Create(
                engine,
                TreatyLayout,
                new JsValue[]
                {
                    treaty.TreatyId,
                    treaty.Kind.ToString(),
                    treaty.OtherCivilizationId,
                    treaty.SignedTick,
                });
        }

        var proposals = observation.TreatyProposals ??
                        Array.Empty<ObservedTreatyProposal>();
        var proposalValues = new JsValue[proposals.Count];
        for (var index = 0; index < proposalValues.Length; index++)
        {
            var proposal = proposals[index];
            proposalValues[index] = JsObject.Create(
                engine,
                ProposalLayout,
                new JsValue[]
                {
                    proposal.ProposalId,
                    proposal.Kind.ToString(),
                    proposal.ProposerCivilizationId,
                    proposal.TargetCivilizationId,
                    proposal.ProposedTick,
                    proposal.ExpiresTick,
                });
        }

        var offers = observation.TradeOffers ?? Array.Empty<ObservedTradeOffer>();
        var offerValues = new JsValue[offers.Count];
        for (var index = 0; index < offerValues.Length; index++)
        {
            var offer = offers[index];
            offerValues[index] = JsObject.Create(
                engine,
                TradeOfferLayout,
                new JsValue[]
                {
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
                    offer.Round,
                });
        }

        return JsObject.Create(
            engine,
            RootLayout,
            new JsValue[]
            {
                observation.Tick,
                observation.CivilizationId,
                new JsArray(engine, entities),
                new JsArray(engine, messages),
                new JsArray(engine, shipments),
                new JsArray(engine, energyShipments),
                observation.ViewerEntityId is { } viewer ? viewer : JsValue.Null,
                new JsArray(engine, controlPoints),
                match,
                new JsArray(engine, treatyValues),
                new JsArray(engine, proposalValues),
                new JsArray(engine, offerValues),
            });
    }

    private static JsValue Nullable(string? value) =>
        value is null ? JsValue.Null : value;
}
