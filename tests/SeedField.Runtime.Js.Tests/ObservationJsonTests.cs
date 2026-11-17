using System.Text.Json;
using System.Text.Json.Nodes;
using SeedField.Simulation;

namespace SeedField.Runtime.Js.Tests;

public sealed class ObservationJsonTests
{
    [Fact]
    public void SerializesEveryObservationField()
    {
        var observation = new Observation(
            7,
            "A",
            new[]
            {
                new ObservedEntity(
                    1, "A", "r0", new Vec2(10, 20), 30, 40, true, true, 50,
                    true, 60, true, true),
            },
            new[]
            {
                new ObservedMessage(2, 1, "A", "r0", "topic", "payload", 5, 6),
            },
            new[]
            {
                new ObservedShipment(3, "r0", "r1", 7, 8, 9, "r0>r1"),
            },
            new[]
            {
                new ObservedEnergyShipment(4, "r0", "r1", 11, 12, 13, "r0>r1"),
            },
            ViewerEntityId: 1,
            ControlPoints: new[]
            {
                new ObservedControlPoint("cp", "r0", new Vec2(1, 2), 3, 4, "A", 5, "A"),
            },
            Match: new ObservedMatch(3, null),
            Treaties: new[]
            {
                new ObservedTreaty(10, TreatyKind.NonAggression, "B", 11),
            },
            TreatyProposals: new[]
            {
                new ObservedTreatyProposal(12, TreatyKind.Trade, "A", "B", 13, 14),
            },
            TradeOffers: new[]
            {
                new ObservedTradeOffer(15, "A", "B", "r0", 1, 2, 3, 4, 5, 6, 7, 8),
            });

        var json = JsController.SerializeFullObservation(observation);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(7, root.GetProperty("tick").GetInt64());
        Assert.Equal("A", root.GetProperty("civilizationId").GetString());
        Assert.Equal(1, root.GetProperty("viewerEntityId").GetInt64());

        var entity = root.GetProperty("entities")[0];
        Assert.Equal(1, entity.GetProperty("entityId").GetInt64());
        Assert.Equal("r0", entity.GetProperty("regionId").GetString());
        Assert.Equal(10, entity.GetProperty("x").GetInt32());
        Assert.Equal(20, entity.GetProperty("y").GetInt32());
        Assert.Equal(30, entity.GetProperty("health").GetInt32());
        Assert.Equal(40, entity.GetProperty("maxHealth").GetInt32());
        Assert.True(entity.GetProperty("isSelf").GetBoolean());
        Assert.True(entity.GetProperty("hasSensor").GetBoolean());
        Assert.Equal(50, entity.GetProperty("sensorRangeMm").GetInt32());
        Assert.True(entity.GetProperty("hasWeapon").GetBoolean());
        Assert.Equal(60, entity.GetProperty("weaponRangeMm").GetInt32());
        Assert.True(entity.GetProperty("hasDrive").GetBoolean());
        Assert.True(entity.GetProperty("hasComm").GetBoolean());

        var message = root.GetProperty("messages")[0];
        Assert.Equal("topic", message.GetProperty("topic").GetString());
        Assert.Equal("payload", message.GetProperty("payload").GetString());
        Assert.Equal(5, message.GetProperty("sentTick").GetInt64());
        Assert.Equal(6, message.GetProperty("deliveredTick").GetInt64());

        var shipment = root.GetProperty("shipments")[0];
        Assert.Equal(3, shipment.GetProperty("shipmentId").GetInt64());
        Assert.Equal("r0>r1", shipment.GetProperty("route").GetString());
        Assert.Equal(
            4,
            root.GetProperty("energyShipments")[0].GetProperty("shipmentId").GetInt64());

        var point = root.GetProperty("controlPoints")[0];
        Assert.Equal("cp", point.GetProperty("id").GetString());
        Assert.Equal("A", point.GetProperty("ownerCivilizationId").GetString());
        Assert.Equal(5, point.GetProperty("holdTicks").GetInt32());

        Assert.Equal(3, root.GetProperty("match").GetProperty("capturedPointsToWin").GetInt32());
        Assert.Equal(
            JsonValueKind.Null,
            root.GetProperty("match").GetProperty("winnerCivilizationId").ValueKind);
        Assert.Equal(
            "NonAggression",
            root.GetProperty("treaties")[0].GetProperty("kind").GetString());
        Assert.Equal(
            "Trade",
            root.GetProperty("treatyProposals")[0].GetProperty("kind").GetString());
        Assert.Equal(
            8,
            root.GetProperty("tradeOffers")[0].GetProperty("round").GetInt32());

        using var controller = new JsController(
            "A",
            """
            function decide(obs) {
                return [{
                    type: "send",
                    entityId: 1,
                    toCivilizationId: "B",
                    topic: "dump",
                    payload: JSON.stringify(obs)
                }];
            }
            """);
        var run = controller.RunJson(JsController.SerializeObservation(observation));
        Assert.True(run.Success, run.FaultMessage);
        var decision = JsController.ParseDecisionJson(run.IntentsJson!, "A");
        var payload = Assert.IsType<SendMessageIntent>(Assert.Single(decision.Intents)).Payload;
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(payload)),
            "Compact payload must expand to the full observation shape.");
    }

    [Fact]
    public void WritesEmptyArraysAndNullsForOptionalSections()
    {
        var observation = new Observation(
            1,
            "A",
            Array.Empty<ObservedEntity>(),
            Array.Empty<ObservedMessage>(),
            Array.Empty<ObservedShipment>(),
            Array.Empty<ObservedEnergyShipment>());

        using var document = JsonDocument.Parse(
            JsController.SerializeFullObservation(observation));
        var root = document.RootElement;

        Assert.Equal(JsonValueKind.Null, root.GetProperty("viewerEntityId").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("match").ValueKind);
        Assert.Equal(0, root.GetProperty("controlPoints").GetArrayLength());
        Assert.Equal(0, root.GetProperty("treaties").GetArrayLength());
        Assert.Equal(0, root.GetProperty("treatyProposals").GetArrayLength());
        Assert.Equal(0, root.GetProperty("tradeOffers").GetArrayLength());
    }
}
