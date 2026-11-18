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
        "hasDrive", "hasComm", "hasDrill", "drillRangeMm",
        "armor", "shield", "maxShield", "weaponKind", "hasSmelter", "hasFabricator",
        "productionEnabled",
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

    private static readonly JsObjectLayout DepositLayout = new(new[]
    {
        "depositId", "regionId", "kind", "x", "y", "radiusMm", "remaining",
        "yieldPerTick",
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
        "treatyProposals", "tradeOffers", "deposits", "material", "ore", "components",
    });

    // JsObject.Create copies the span into a property slot table. Reusing one
    // scratch buffer per worker keeps the observation graph allocation focused
    // on Jint objects instead of short-lived value arrays.
    [ThreadStatic] private static JsValue[]? _depositScratch;
    [ThreadStatic] private static JsValue[]? _entityScratch;
    [ThreadStatic] private static JsValue[]? _messageScratch;
    [ThreadStatic] private static JsValue[]? _shipmentScratch;
    [ThreadStatic] private static JsValue[]? _controlPointScratch;
    [ThreadStatic] private static JsValue[]? _matchScratch;
    [ThreadStatic] private static JsValue[]? _treatyScratch;
    [ThreadStatic] private static JsValue[]? _proposalScratch;
    [ThreadStatic] private static JsValue[]? _tradeOfferScratch;

    public static JsObject Build(Engine engine, Observation observation)
    {
        var entities = new JsValue[observation.Entities.Count];
        for (var index = 0; index < entities.Length; index++)
        {
            var entity = observation.Entities[index];
            var values = _entityScratch ??= new JsValue[23];
            values[0] = entity.EntityId;
            values[1] = entity.CivilizationId;
            values[2] = entity.RegionId;
            values[3] = entity.Position.X;
            values[4] = entity.Position.Y;
            values[5] = entity.Health;
            values[6] = entity.MaxHealth;
            values[7] = entity.IsSelf;
            values[8] = entity.HasSensor;
            values[9] = entity.SensorRangeMm;
            values[10] = entity.HasWeapon;
            values[11] = entity.WeaponRangeMm;
            values[12] = entity.HasDrive;
            values[13] = entity.HasComm;
            values[14] = entity.HasDrill;
            values[15] = entity.DrillRangeMm;
            values[16] = entity.Armor;
            values[17] = entity.Shield;
            values[18] = entity.MaxShield;
            values[19] = entity.WeaponKind;
            values[20] = entity.HasSmelter;
            values[21] = entity.HasFabricator;
            values[22] = entity.ProductionEnabled;
            entities[index] = JsObject.Create(engine, EntityLayout, values);
        }

        var messages = new JsValue[observation.Messages.Count];
        for (var index = 0; index < messages.Length; index++)
        {
            var message = observation.Messages[index];
            var values = _messageScratch ??= new JsValue[8];
            values[0] = message.MessageId;
            values[1] = message.FromEntityId;
            values[2] = message.FromCivilizationId;
            values[3] = message.TargetRegionId;
            values[4] = message.Topic;
            values[5] = message.Payload;
            values[6] = message.SentTick;
            values[7] = message.DeliveredTick;
            messages[index] = JsObject.Create(engine, MessageLayout, values);
        }

        var shipments = new JsValue[observation.Shipments.Count];
        for (var index = 0; index < shipments.Length; index++)
        {
            var shipment = observation.Shipments[index];
            var values = _shipmentScratch ??= new JsValue[7];
            values[0] = shipment.ShipmentId;
            values[1] = shipment.FromRegionId;
            values[2] = shipment.ToRegionId;
            values[3] = shipment.Amount;
            values[4] = shipment.SentTick;
            values[5] = shipment.ArriveTick;
            values[6] = shipment.Route;
            shipments[index] = JsObject.Create(engine, ShipmentLayout, values);
        }

        var energyShipments = new JsValue[observation.EnergyShipments.Count];
        for (var index = 0; index < energyShipments.Length; index++)
        {
            var shipment = observation.EnergyShipments[index];
            var values = _shipmentScratch ??= new JsValue[7];
            values[0] = shipment.ShipmentId;
            values[1] = shipment.FromRegionId;
            values[2] = shipment.ToRegionId;
            values[3] = shipment.Amount;
            values[4] = shipment.SentTick;
            values[5] = shipment.ArriveTick;
            values[6] = shipment.Route;
            energyShipments[index] = JsObject.Create(engine, ShipmentLayout, values);
        }

        var points = observation.ControlPoints ?? Array.Empty<ObservedControlPoint>();
        var controlPoints = new JsValue[points.Count];
        for (var index = 0; index < controlPoints.Length; index++)
        {
            var point = points[index];
            var values = _controlPointScratch ??= new JsValue[9];
            values[0] = point.Id;
            values[1] = point.RegionId;
            values[2] = point.Position.X;
            values[3] = point.Position.Y;
            values[4] = point.RadiusMm;
            values[5] = point.HoldTicksRequired;
            values[6] = Nullable(point.HoldingCivilizationId);
            values[7] = point.HoldTicks;
            values[8] = Nullable(point.OwnerCivilizationId);
            controlPoints[index] = JsObject.Create(engine, ControlPointLayout, values);
        }

        var matchValues = _matchScratch ??= new JsValue[2];
        JsValue match;
        if (observation.Match is null)
        {
            match = JsValue.Null;
        }
        else
        {
            matchValues[0] = observation.Match.CapturedPointsToWin;
            matchValues[1] = Nullable(observation.Match.WinnerCivilizationId);
            match = JsObject.Create(engine, MatchLayout, matchValues);
        }

        var treaties = observation.Treaties ?? Array.Empty<ObservedTreaty>();
        var treatyValues = new JsValue[treaties.Count];
        for (var index = 0; index < treatyValues.Length; index++)
        {
            var treaty = treaties[index];
            var values = _treatyScratch ??= new JsValue[4];
            values[0] = treaty.TreatyId;
            values[1] = treaty.Kind.ToString();
            values[2] = treaty.OtherCivilizationId;
            values[3] = treaty.SignedTick;
            treatyValues[index] = JsObject.Create(engine, TreatyLayout, values);
        }

        var proposals = observation.TreatyProposals ??
                        Array.Empty<ObservedTreatyProposal>();
        var proposalValues = new JsValue[proposals.Count];
        for (var index = 0; index < proposalValues.Length; index++)
        {
            var proposal = proposals[index];
            var values = _proposalScratch ??= new JsValue[6];
            values[0] = proposal.ProposalId;
            values[1] = proposal.Kind.ToString();
            values[2] = proposal.ProposerCivilizationId;
            values[3] = proposal.TargetCivilizationId;
            values[4] = proposal.ProposedTick;
            values[5] = proposal.ExpiresTick;
            proposalValues[index] = JsObject.Create(engine, ProposalLayout, values);
        }

        var offers = observation.TradeOffers ?? Array.Empty<ObservedTradeOffer>();
        var offerValues = new JsValue[offers.Count];
        for (var index = 0; index < offerValues.Length; index++)
        {
            var offer = offers[index];
            var values = _tradeOfferScratch ??= new JsValue[12];
            values[0] = offer.TradeOfferId;
            values[1] = offer.ProposerCivilizationId;
            values[2] = offer.TargetCivilizationId;
            values[3] = offer.RegionId;
            values[4] = offer.GiveMaterial;
            values[5] = offer.GiveEnergy;
            values[6] = offer.WantMaterial;
            values[7] = offer.WantEnergy;
            values[8] = offer.ProposedTick;
            values[9] = offer.ExpiresTick;
            values[10] = offer.NegotiationId;
            values[11] = offer.Round;
            offerValues[index] = JsObject.Create(engine, TradeOfferLayout, values);
        }

        var deposits = observation.Deposits ?? Array.Empty<ObservedDeposit>();
        var depositValues = new JsValue[deposits.Count];
        for (var index = 0; index < depositValues.Length; index++)
        {
            var deposit = deposits[index];
            var values = _depositScratch ??= new JsValue[8];
            values[0] = deposit.DepositId;
            values[1] = deposit.RegionId;
            values[2] = deposit.Kind.ToString();
            values[3] = deposit.Position.X;
            values[4] = deposit.Position.Y;
            values[5] = deposit.RadiusMm;
            values[6] = deposit.Remaining;
            values[7] = deposit.YieldPerTick;
            depositValues[index] = JsObject.Create(engine, DepositLayout, values);
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
                new JsArray(engine, depositValues),
                observation.Material,
                observation.Ore,
                observation.Components,
            });
    }

    private static JsValue Nullable(string? value) =>
        value is null ? JsValue.Null : value;
}
