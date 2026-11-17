using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class RuinSnapshotTests
{
    private static readonly RuinSnapshotEntry[] SingleEntry =
    {
        new("power", 1, 0, 0, 100),
    };

    [Fact]
    public void CaptureAndLoadRoundTripSnapshotEntries()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 100)
            .Build();
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "power",
            0,
            "Power",
            MaxHealth: 10,
            EnergyCapacity: 10,
            MaterialCost: 100));
        new SimulationEngine(world).ExecuteDeploymentCommand(new DeployCommand(
            "A",
            blueprint,
            "r0",
            new Vec2(1_200, 800)));

        var snapshot = RuinSnapshotSerializer.Capture(world, "A", "r0");
        var loaded = RuinSnapshotSerializer.Load(
            RuinSnapshotSerializer.Save(snapshot));

        var entry = Assert.Single(loaded.Entries);
        Assert.Equal("power", entry.BlueprintId);
        Assert.Equal(1, entry.BlueprintVersion);
        Assert.Equal(1_200, entry.X);
        Assert.Equal(800, entry.Y);
        Assert.Equal(100, entry.MaterialValue);
        Assert.Equal("A", loaded.SourceCivilizationId);
    }

    [Fact]
    public void ImportAgesAndLootsSnapshotEntriesIntoWrecks()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .Build();
        var engine = new SimulationEngine(world);
        var json = SmallSnapshotJson(material: 100);

        var result = engine.ExecuteRuinCommand(new ImportRuinCommand(
            "A",
            "r0",
            json,
            CenterX: 500,
            CenterY: 500,
            RadiusMm: 1_000,
            AgingPercent: 20,
            LootPercent: 50));

        Assert.True(result.Success);
        var imported = Assert.IsType<RuinImported>(Assert.Single(result.Events));
        Assert.Equal(1, imported.EntriesInSlice);
        Assert.Equal(1, imported.WrecksCreated);
        Assert.Equal(40, imported.MaterialTotal);
        var wreck = Assert.Single(world.Wrecks.Values);
        Assert.Equal("offline-A", wreck.OwnerCivilizationId);
        Assert.Equal(40, wreck.MaterialRecoverable);
        Assert.Equal(new Vec2(500, 500), wreck.Position);
    }

    [Fact]
    public void ImportRejectsCoreRegions()
    {
        var world = new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddCore("A", "core", new SeedCoreConfig(1, 1, 10, 10))
            .Build();
        var engine = new SimulationEngine(world);

        var result = engine.ExecuteRuinCommand(new ImportRuinCommand(
            "A",
            "core",
            SmallSnapshotJson(100),
            500,
            500,
            1_000,
            0,
            0));

        Assert.False(result.Success);
        Assert.Equal(SimulationErrorCode.RuinImportRejected, result.ErrorCode);
        Assert.Empty(world.Wrecks);
    }

    [Fact]
    public void ImportRejectsMalformedOrOversizedSnapshots()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .Build();
        var engine = new SimulationEngine(world);

        var malformed = engine.ExecuteRuinCommand(new ImportRuinCommand(
            "A", "r0", "{", 500, 500, 1_000, 0, 0));
        var unsupported = SmallSnapshotJson(100).Replace(
            "\"schemaVersion\": 1",
            "\"schemaVersion\": 99");
        var wrongSchema = engine.ExecuteRuinCommand(new ImportRuinCommand(
            "A",
            "r0",
            unsupported,
            500,
            500,
            1_000,
            0,
            0));

        Assert.Equal(SimulationErrorCode.RuinImportRejected, malformed.ErrorCode);
        Assert.Equal(SimulationErrorCode.RuinImportRejected, wrongSchema.ErrorCode);
        Assert.Empty(world.Wrecks);
    }

    [Fact]
    public void ImportSkipsEntriesThatAgeAndLootToZero()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .Build();
        var engine = new SimulationEngine(world);
        var result = engine.ExecuteRuinCommand(new ImportRuinCommand(
            "A",
            "r0",
            SmallSnapshotJson(10),
            CenterX: 500,
            CenterY: 500,
            RadiusMm: 1_000,
            AgingPercent: 90,
            LootPercent: 100));

        Assert.True(result.Success);
        Assert.Empty(world.Wrecks);
        var imported = Assert.IsType<RuinImported>(Assert.Single(result.Events));
        Assert.Equal(1, imported.EntriesInSlice);
        Assert.Equal(0, imported.WrecksCreated);
    }

    [Fact]
    public void ImportedWrecksSurviveSaveLoad()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .Build();
        var engine = new SimulationEngine(world);
        engine.ExecuteRuinCommand(new ImportRuinCommand(
            "A",
            "r0",
            SmallSnapshotJson(100),
            500,
            500,
            1_000,
            20,
            50));

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        var wreck = Assert.Single(loaded.Wrecks.Values);
        Assert.Equal(40, wreck.MaterialRecoverable);
    }

    private static string SmallSnapshotJson(long material) =>
        RuinSnapshotSerializer.Save(new RuinSnapshotData(
            RuinSnapshotSerializer.CurrentSchemaVersion,
            "offline-A",
            WidthMm: 1,
            HeightMm: 1,
            SingleEntry.Select(entry => entry with { MaterialValue = material }).ToArray()));
}
