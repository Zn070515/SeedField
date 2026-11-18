using System.Buffers;
using System.Text;
using System.Text.Json;
using SeedField.Simulation;

namespace SeedField.Runtime.Js;

/// <summary>
/// Compact controller-boundary observation JSON. Property names are replaced by
/// positional arrays and a shared string table; trusted JS in the controller
/// harness expands it back to the exact object shape scripts already receive.
/// This shrinks the average controller payload ~4x and removes most of Jint's
/// JSON.parse allocation without changing the script contract.
/// </summary>
internal static partial class ObservationJson
{
    private static readonly JsonWriterOptions CompactWriterOptions = new()
    {
        Indented = false,
        SkipValidation = true,
    };

    [ThreadStatic]
    private static ArrayBufferWriter<byte>? _compactBuffer;

    public static string Serialize(Observation observation)
    {
        var buffer = _compactBuffer ??= new ArrayBufferWriter<byte>(8 * 1024);
        buffer.Clear();
        using var writer = new Utf8JsonWriter(buffer, CompactWriterOptions);
        var strings = new StringTable();

        writer.WriteStartObject();
        writer.WriteNumber("tick", observation.Tick);
        writer.WriteString("civilizationId", observation.CivilizationId);

        writer.WriteStartArray("entities");
        foreach (var entity in observation.Entities)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(entity.EntityId);
            writer.WriteNumberValue(strings.Index(entity.CivilizationId));
            writer.WriteNumberValue(strings.Index(entity.RegionId));
            writer.WriteNumberValue(entity.Position.X);
            writer.WriteNumberValue(entity.Position.Y);
            writer.WriteNumberValue(entity.Health);
            writer.WriteNumberValue(entity.MaxHealth);
            writer.WriteNumberValue(entity.IsSelf ? 1 : 0);
            writer.WriteNumberValue(entity.HasSensor ? 1 : 0);
            writer.WriteNumberValue(entity.SensorRangeMm);
            writer.WriteNumberValue(entity.HasWeapon ? 1 : 0);
            writer.WriteNumberValue(entity.WeaponRangeMm);
            writer.WriteNumberValue(entity.HasDrive ? 1 : 0);
            writer.WriteNumberValue(entity.HasComm ? 1 : 0);
            writer.WriteNumberValue(entity.HasDrill ? 1 : 0);
            writer.WriteNumberValue(entity.DrillRangeMm);
            writer.WriteNumberValue(entity.Armor);
            writer.WriteNumberValue(entity.Shield);
            writer.WriteNumberValue(entity.MaxShield);
            writer.WriteNumberValue(strings.Index(entity.WeaponKind));
            writer.WriteNumberValue(entity.HasSmelter ? 1 : 0);
            writer.WriteNumberValue(entity.HasFabricator ? 1 : 0);
            writer.WriteNumberValue(entity.ProductionEnabled ? 1 : 0);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("messages");
        foreach (var message in observation.Messages)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(message.MessageId);
            writer.WriteNumberValue(message.FromEntityId);
            writer.WriteNumberValue(strings.Index(message.FromCivilizationId));
            writer.WriteNumberValue(strings.Index(message.TargetRegionId));
            writer.WriteNumberValue(strings.Index(message.Topic));
            writer.WriteStringValue(message.Payload);
            writer.WriteNumberValue(message.SentTick);
            writer.WriteNumberValue(message.DeliveredTick);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("shipments");
        foreach (var shipment in observation.Shipments)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(shipment.ShipmentId);
            writer.WriteNumberValue(strings.Index(shipment.FromRegionId));
            writer.WriteNumberValue(strings.Index(shipment.ToRegionId));
            writer.WriteNumberValue(shipment.Amount);
            writer.WriteNumberValue(shipment.SentTick);
            writer.WriteNumberValue(shipment.ArriveTick);
            writer.WriteStringValue(shipment.Route);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("energyShipments");
        foreach (var shipment in observation.EnergyShipments)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(shipment.ShipmentId);
            writer.WriteNumberValue(strings.Index(shipment.FromRegionId));
            writer.WriteNumberValue(strings.Index(shipment.ToRegionId));
            writer.WriteNumberValue(shipment.Amount);
            writer.WriteNumberValue(shipment.SentTick);
            writer.WriteNumberValue(shipment.ArriveTick);
            writer.WriteStringValue(shipment.Route);
            writer.WriteEndArray();
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
            writer.WriteStartArray();
            writer.WriteNumberValue(strings.Index(point.Id));
            writer.WriteNumberValue(strings.Index(point.RegionId));
            writer.WriteNumberValue(point.Position.X);
            writer.WriteNumberValue(point.Position.Y);
            writer.WriteNumberValue(point.RadiusMm);
            writer.WriteNumberValue(point.HoldTicksRequired);
            writer.WriteNumberValue(strings.IndexOrNull(point.HoldingCivilizationId));
            writer.WriteNumberValue(point.HoldTicks);
            writer.WriteNumberValue(strings.IndexOrNull(point.OwnerCivilizationId));
            writer.WriteEndArray();
        }

        writer.WriteEndArray();

        if (observation.Match is { } match)
        {
            writer.WriteStartArray("match");
            writer.WriteNumberValue(match.CapturedPointsToWin);
            writer.WriteNumberValue(strings.IndexOrNull(match.WinnerCivilizationId));
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteNull("match");
        }

        writer.WriteStartArray("treaties");
        foreach (var treaty in observation.Treaties ?? Array.Empty<ObservedTreaty>())
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(treaty.TreatyId);
            writer.WriteNumberValue((int)treaty.Kind);
            writer.WriteNumberValue(strings.Index(treaty.OtherCivilizationId));
            writer.WriteNumberValue(treaty.SignedTick);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("treatyProposals");
        foreach (var proposal in observation.TreatyProposals ??
                                 Array.Empty<ObservedTreatyProposal>())
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(proposal.ProposalId);
            writer.WriteNumberValue((int)proposal.Kind);
            writer.WriteNumberValue(strings.Index(proposal.ProposerCivilizationId));
            writer.WriteNumberValue(strings.Index(proposal.TargetCivilizationId));
            writer.WriteNumberValue(proposal.ProposedTick);
            writer.WriteNumberValue(proposal.ExpiresTick);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("tradeOffers");
        foreach (var offer in observation.TradeOffers ?? Array.Empty<ObservedTradeOffer>())
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(offer.TradeOfferId);
            writer.WriteNumberValue(strings.Index(offer.ProposerCivilizationId));
            writer.WriteNumberValue(strings.Index(offer.TargetCivilizationId));
            writer.WriteNumberValue(strings.Index(offer.RegionId));
            writer.WriteNumberValue(offer.GiveMaterial);
            writer.WriteNumberValue(offer.GiveEnergy);
            writer.WriteNumberValue(offer.WantMaterial);
            writer.WriteNumberValue(offer.WantEnergy);
            writer.WriteNumberValue(offer.ProposedTick);
            writer.WriteNumberValue(offer.ExpiresTick);
            writer.WriteNumberValue(offer.NegotiationId);
            writer.WriteNumberValue(offer.Round);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("deposits");
        foreach (var deposit in observation.Deposits ?? Array.Empty<ObservedDeposit>())
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(deposit.DepositId);
            writer.WriteNumberValue(strings.Index(deposit.RegionId));
            writer.WriteNumberValue((int)deposit.Kind);
            writer.WriteNumberValue(deposit.Position.X);
            writer.WriteNumberValue(deposit.Position.Y);
            writer.WriteNumberValue(deposit.RadiusMm);
            writer.WriteNumberValue(deposit.Remaining);
            writer.WriteNumberValue(deposit.YieldPerTick);
            writer.WriteEndArray();
        }

        writer.WriteEndArray();

        writer.WriteNumber("material", observation.Material);
        writer.WriteNumber("ore", observation.Ore);
        writer.WriteNumber("components", observation.Components);
        writer.WriteNumber("oreCopper", observation.OreCopper);
        writer.WriteNumber("oreLead", observation.OreLead);
        writer.WriteNumber("oreTitanium", observation.OreTitanium);
        writer.WriteNumber("oreCoal", observation.OreCoal);

        // Last key so deleting it in JS leaves the original key order intact.
        writer.WriteStartArray("s");
        foreach (var value in strings.Values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private sealed class StringTable
    {
        private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

        public List<string> Values { get; } = new();

        public int Index(string value)
        {
            if (_index.TryGetValue(value, out var index))
            {
                return index;
            }

            index = Values.Count;
            Values.Add(value);
            _index[value] = index;
            return index;
        }

        public int IndexOrNull(string? value) => value is null ? -1 : Index(value);
    }
}
