using SeedField.Simulation;

namespace SeedField.Runtime.Js.Tests;

public sealed class GameSaveTests
{
    [Fact]
    public void GameSaveRestoresWorldAndControllerVersion()
    {
        var world = BuildWorld();
        using var slot = new ControllerSlot("A", WaitScript);
        Assert.True(slot.Deploy(MoveScript).Success);
        Assert.Equal(2, slot.Version);

        var json = GameSaveSerializer.Save(world, new[] { slot });
        var loaded = GameSaveSerializer.Load(json);
        using var loadedSlot = Assert.Single(loaded.Controllers);

        Assert.Equal(2, loadedSlot.Version);
        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded.World));

        var observation = new SimulationEngine(loaded.World).BuildObservation("A");
        Assert.IsType<MoveIntent>(Assert.Single(loadedSlot.Decide(observation).Intents));
    }

    [Fact]
    public void GameSaveV1IsMigratedToCurrentSchema()
    {
        var world = BuildWorld();
        using var slot = new ControllerSlot("A", WaitScript);
        var json = GameSaveSerializer.Save(world, new[] { slot });
        var v1 = json.Replace("\"schemaVersion\": 3", "\"schemaVersion\": 1");

        var loaded = GameSaveSerializer.Load(v1);

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded.World));
        using var loadedSlot = Assert.Single(loaded.Controllers);
        Assert.Equal(1, loadedSlot.Version);
    }

    [Fact]
    public void GameSavePersistsOpponentPackageMetadata()
    {
        var world = BuildWorld();
        using var slot = new ControllerSlot("A", WaitScript);
        var json = GameSaveSerializer.Save(
            world,
            new[] { slot },
            "builtin.turtle",
            "0.1.0");

        var loaded = GameSaveSerializer.Load(json);

        Assert.Equal("builtin.turtle", loaded.OpponentPackageId);
        Assert.Equal("0.1.0", loaded.OpponentPackageVersion);
    }

    [Fact]
    public void GameSaveRejectsUnknownSchemaVersion()
    {
        var world = BuildWorld();
        using var slot = new ControllerSlot("A", WaitScript);
        var json = GameSaveSerializer.Save(world, new[] { slot });
        var unsupported = json.Replace("\"schemaVersion\": 3", "\"schemaVersion\": 99");

        var exception = Assert.Throws<SaveFormatException>(() =>
            GameSaveSerializer.Load(unsupported));

        Assert.Equal("UNSUPPORTED_GAME_SAVE_VERSION", exception.Code);
    }

    [Fact]
    public void GameSaveRejectsInvalidControllerScript()
    {
        var world = BuildWorld();
        using var slot = new ControllerSlot("A", WaitScript);
        var json = GameSaveSerializer.Save(world, new[] { slot });
        var broken = json.Replace("function decide", "function notDecide");

        var exception = Assert.Throws<SaveFormatException>(() =>
            GameSaveSerializer.Load(broken));

        Assert.Equal("INVALID_CONTROLLER_SCRIPT", exception.Code);
    }

    private static WorldState BuildWorld() =>
        new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 100,
                Energy: 1_000,
                EnergyCapacity: 1_000,
                Material: 0,
                Drive: new DriveModule(100, EnergyCostPerMove: 1)))
            .Build();

    private const string WaitScript = """
        function decide(obs) {
            return [{ type: "wait", entityId: 1 }];
        }
        """;

    private const string MoveScript = """
        function decide(obs) {
            return [{ type: "move", entityId: 1, target: { x: 500, y: 500 } }];
        }
        """;
}
