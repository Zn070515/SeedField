using System.Text.Json;
using System.Text.RegularExpressions;
using SeedField.Simulation;

namespace SeedField.Runtime.Js.Tests;

public sealed class ControllerContractTests
{
    [Fact]
    public void TypeScriptContractCoversEveryObservationField()
    {
        var contract = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "contracts",
            "seedfield-controller.d.ts"));
        var observation = RichObservation();
        using var document = JsonDocument.Parse(
            JsController.SerializeFullObservation(observation));
        var fields = new HashSet<string>(StringComparer.Ordinal);
        CollectFields(document.RootElement, fields);

        Assert.NotEmpty(fields);
        foreach (var field in fields)
        {
            Assert.True(
                Regex.IsMatch(contract, $@"\b{Regex.Escape(field)}\b"),
                $"TypeScript contract is missing observation field '{field}'.");
        }
    }

    [Fact]
    public void TypeScriptContractListsEveryIntentKindAndField()
    {
        var contract = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "contracts",
            "seedfield-controller.d.ts"));

        foreach (var kind in new[]
                 {
                     "move", "attack", "send", "wait", "transfer",
                     "sendEnergy", "drawEnergy", "mine",
                 })
        {
            Assert.Contains($"\"{kind}\"", contract, StringComparison.Ordinal);
        }

        foreach (var field in new[]
                 {
                     "entityId", "target", "targetId", "toCivilizationId",
                     "topic", "payload", "targetRegionId", "amount",
                 })
        {
            Assert.True(
                Regex.IsMatch(contract, $@"\b{Regex.Escape(field)}\b"),
                $"TypeScript contract is missing intent field '{field}'.");
        }
    }

    private static void CollectFields(JsonElement element, HashSet<string> fields)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    fields.Add(property.Name);
                    CollectFields(property.Value, fields);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectFields(item, fields);
                }

                break;
        }
    }

    private static Observation RichObservation() =>
        new(
            7,
            "A",
            new[]
            {
                new ObservedEntity(
                    1, "A", "r0", new Vec2(10, 20), 30, 40, true, true, 50,
                    true, 60, true, true, true, 70),
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
            },
            Deposits: new[]
            {
                new ObservedDeposit(
                    20, "r0", OreKind.Copper, new Vec2(5, 6), 100, 50, 5),
            });

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SeedField.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}
