using System.Buffers;
using System.Text;
using System.Text.Json;
using SeedField.Simulation;

namespace SeedField.Runtime.Js;

/// <summary>
/// Object-shaped observation JSON. Kept for tests, tools, and debugging; the
/// controller boundary uses the compact form and expands it in trusted JS.
/// </summary>
internal static partial class ObservationJson
{
    private static readonly JsonWriterOptions FullWriterOptions = new()
    {
        Indented = false,
        SkipValidation = true,
    };

    [ThreadStatic]
    private static ArrayBufferWriter<byte>? _fullBuffer;

    public static string SerializeFull(Observation observation)
    {
        var buffer = _fullBuffer ??= new ArrayBufferWriter<byte>(16 * 1024);
        buffer.Clear();
        using var writer = new Utf8JsonWriter(buffer, FullWriterOptions);

        writer.WriteStartObject();
        writer.WriteNumber("tick", observation.Tick);
        writer.WriteString("civilizationId", observation.CivilizationId);
        writer.WriteStartArray("entities");
        foreach (var entity in observation.Entities)
        {
            writer.WriteStartObject();
            writer.WriteNumber("entityId", entity.EntityId);
            writer.WriteString("civilizationId", entity.CivilizationId);
            writer.WriteString("regionId", entity.RegionId);
            writer.WriteNumber("x", entity.Position.X);
            writer.WriteNumber("y", entity.Position.Y);
            writer.WriteNumber("health", entity.Health);
            writer.WriteNumber("maxHealth", entity.MaxHealth);
            writer.WriteBoolean("isSelf", entity.IsSelf);
            writer.WriteBoolean("hasSensor", entity.HasSensor);
            writer.WriteNumber("sensorRangeMm", entity.SensorRangeMm);
            writer.WriteBoolean("hasWeapon", entity.HasWeapon);
            writer.WriteNumber("weaponRangeMm", entity.WeaponRangeMm);
            writer.WriteBoolean("hasDrive", entity.HasDrive);
            writer.WriteBoolean("hasComm", entity.HasComm);
            writer.WriteBoolean("hasDrill", entity.HasDrill);
            writer.WriteNumber("drillRangeMm", entity.DrillRangeMm);
            writer.WriteNumber("armor", entity.Armor);
            writer.WriteNumber("shield", entity.Shield);
            writer.WriteNumber("maxShield", entity.MaxShield);
            writer.WriteString("weaponKind", entity.WeaponKind);
            writer.WriteBoolean("hasSmelter", entity.HasSmelter);
            writer.WriteBoolean("hasFabricator", entity.HasFabricator);
            writer.WriteBoolean("productionEnabled", entity.ProductionEnabled);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("messages");
        foreach (var message in observation.Messages)
        {
            writer.WriteStartObject();
            writer.WriteNumber("messageId", message.MessageId);
            writer.WriteNumber("fromEntityId", message.FromEntityId);
            writer.WriteString("fromCivilizationId", message.FromCivilizationId);
            writer.WriteString("targetRegionId", message.TargetRegionId);
            writer.WriteString("topic", message.Topic);
            writer.WriteString("payload", message.Payload);
            writer.WriteNumber("sentTick", message.SentTick);
            writer.WriteNumber("deliveredTick", message.DeliveredTick);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("shipments");
        foreach (var shipment in observation.Shipments)
        {
            writer.WriteStartObject();
            writer.WriteNumber("shipmentId", shipment.ShipmentId);
            writer.WriteString("fromRegionId", shipment.FromRegionId);
            writer.WriteString("toRegionId", shipment.ToRegionId);
            writer.WriteNumber("amount", shipment.Amount);
            writer.WriteNumber("sentTick", shipment.SentTick);
            writer.WriteNumber("arriveTick", shipment.ArriveTick);
            writer.WriteString("route", shipment.Route);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("energyShipments");
        foreach (var shipment in observation.EnergyShipments)
        {
            writer.WriteStartObject();
            writer.WriteNumber("shipmentId", shipment.ShipmentId);
            writer.WriteString("fromRegionId", shipment.FromRegionId);
            writer.WriteString("toRegionId", shipment.ToRegionId);
            writer.WriteNumber("amount", shipment.Amount);
            writer.WriteNumber("sentTick", shipment.SentTick);
            writer.WriteNumber("arriveTick", shipment.ArriveTick);
            writer.WriteString("route", shipment.Route);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        if (observation.ViewerEntityId is { } viewer)
        {
            writer.WriteNumber("viewerEntityId", viewer);
        }
        else
        {
            writer.WriteNull("viewerEntityId");
        }

        writer.WriteStartArray("controlPoints");
        foreach (var point in observation.ControlPoints ?? Array.Empty<ObservedControlPoint>())
        {
            writer.WriteStartObject();
            writer.WriteString("id", point.Id);
            writer.WriteString("regionId", point.RegionId);
            writer.WriteNumber("x", point.Position.X);
            writer.WriteNumber("y", point.Position.Y);
            writer.WriteNumber("radiusMm", point.RadiusMm);
            writer.WriteNumber("holdTicksRequired", point.HoldTicksRequired);
            writer.WriteString("holdingCivilizationId", point.HoldingCivilizationId);
            writer.WriteNumber("holdTicks", point.HoldTicks);
            writer.WriteString("ownerCivilizationId", point.OwnerCivilizationId);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        if (observation.Match is { } match)
        {
            writer.WriteStartObject("match");
            writer.WriteNumber("capturedPointsToWin", match.CapturedPointsToWin);
            writer.WriteString("winnerCivilizationId", match.WinnerCivilizationId);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull("match");
        }

        writer.WriteStartArray("treaties");
        foreach (var treaty in observation.Treaties ?? Array.Empty<ObservedTreaty>())
        {
            writer.WriteStartObject();
            writer.WriteNumber("treatyId", treaty.TreatyId);
            writer.WriteString("kind", treaty.Kind.ToString());
            writer.WriteString("otherCivilizationId", treaty.OtherCivilizationId);
            writer.WriteNumber("signedTick", treaty.SignedTick);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("treatyProposals");
        foreach (var proposal in observation.TreatyProposals ??
                                 Array.Empty<ObservedTreatyProposal>())
        {
            writer.WriteStartObject();
            writer.WriteNumber("proposalId", proposal.ProposalId);
            writer.WriteString("kind", proposal.Kind.ToString());
            writer.WriteString("proposerCivilizationId", proposal.ProposerCivilizationId);
            writer.WriteString("targetCivilizationId", proposal.TargetCivilizationId);
            writer.WriteNumber("proposedTick", proposal.ProposedTick);
            writer.WriteNumber("expiresTick", proposal.ExpiresTick);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("tradeOffers");
        foreach (var offer in observation.TradeOffers ?? Array.Empty<ObservedTradeOffer>())
        {
            writer.WriteStartObject();
            writer.WriteNumber("tradeOfferId", offer.TradeOfferId);
            writer.WriteString("proposerCivilizationId", offer.ProposerCivilizationId);
            writer.WriteString("targetCivilizationId", offer.TargetCivilizationId);
            writer.WriteString("regionId", offer.RegionId);
            writer.WriteNumber("giveMaterial", offer.GiveMaterial);
            writer.WriteNumber("giveEnergy", offer.GiveEnergy);
            writer.WriteNumber("wantMaterial", offer.WantMaterial);
            writer.WriteNumber("wantEnergy", offer.WantEnergy);
            writer.WriteNumber("proposedTick", offer.ProposedTick);
            writer.WriteNumber("expiresTick", offer.ExpiresTick);
            writer.WriteNumber("negotiationId", offer.NegotiationId);
            writer.WriteNumber("round", offer.Round);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("deposits");
        foreach (var deposit in observation.Deposits ?? Array.Empty<ObservedDeposit>())
        {
            writer.WriteStartObject();
            writer.WriteNumber("depositId", deposit.DepositId);
            writer.WriteString("regionId", deposit.RegionId);
            writer.WriteString("kind", deposit.Kind.ToString());
            writer.WriteNumber("x", deposit.Position.X);
            writer.WriteNumber("y", deposit.Position.Y);
            writer.WriteNumber("radiusMm", deposit.RadiusMm);
            writer.WriteNumber("remaining", deposit.Remaining);
            writer.WriteNumber("yieldPerTick", deposit.YieldPerTick);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteNumber("material", observation.Material);
        writer.WriteNumber("ore", observation.Ore);
        writer.WriteNumber("components", observation.Components);
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
