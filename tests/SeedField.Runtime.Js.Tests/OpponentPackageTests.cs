using SeedField.Simulation;

namespace SeedField.Runtime.Js.Tests;

public sealed class OpponentPackageTests
{
    [Fact]
    public void BuiltInSkirmisherPackageIsValidAndRunnable()
    {
        var package = BuiltInOpponents.Skirmisher;

        Assert.Equal("offline-opponent", package.Manifest.Kind);
        Assert.Equal(64, package.Manifest.ContentHash.Length);
        Assert.Contains("decide", package.ProgramSource);

        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "B",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Drive: new DriveModule(100, EnergyCostPerMove: 1)))
            .Build();
        var engine = new SimulationEngine(world);
        using var controller = new JsController("B", package.ProgramSource);

        var decision = controller.Decide(engine.BuildDeviceObservation(new EntityId(1)));

        Assert.False(decision.HasFaults);
    }

    [Fact]
    public void ThirdPartyImportStaysClosedUntilIsolationGatesPass()
    {
        Assert.False(OpponentPackage.ThirdPartyImportEnabled);
    }

    [Fact]
    public void BuiltInOpponentsRemainTrustedBuiltIns()
    {
        Assert.Equal(2, BuiltInOpponents.All.Count);
        Assert.All(
            BuiltInOpponents.All,
            package => Assert.StartsWith(
                "builtin.",
                package.Manifest.PackageId,
                StringComparison.Ordinal));
        Assert.All(
            BuiltInOpponents.All,
            package => Assert.DoesNotContain(
                "require(",
                package.ProgramSource,
                StringComparison.Ordinal));
    }

    [Fact]
    public void UnsafeEntryPointPathIsRejected()
    {
        var exception = Assert.Throws<OpponentPackageValidationException>(() =>
            OpponentPackage.Create(
                Manifest(entryPoints: new[] { "../evil.js" }),
                new Dictionary<string, string> { ["../evil.js"] = "function decide(){return [];}" }));

        Assert.Equal("UNSAFE_PATH", exception.Code);
    }

    [Fact]
    public void MissingEntryProgramIsRejected()
    {
        var exception = Assert.Throws<OpponentPackageValidationException>(() =>
            OpponentPackage.Create(
                Manifest(entryPoints: new[] { "programs/missing.js" }),
                new Dictionary<string, string>()));

        Assert.Equal("MISSING_ENTRY", exception.Code);
    }

    [Fact]
    public void UnsupportedPackageSchemaIsRejected()
    {
        var exception = Assert.Throws<OpponentPackageValidationException>(() =>
            OpponentPackage.Create(
                Manifest(schemaVersion: 2),
                new Dictionary<string, string> { ["programs/controller.js"] = "function decide(){return [];}" }));

        Assert.Equal("UNSUPPORTED_PACKAGE_SCHEMA", exception.Code);
    }

    [Fact]
    public void OversizedProgramIsRejected()
    {
        var exception = Assert.Throws<OpponentPackageValidationException>(() =>
            OpponentPackage.Create(
                Manifest(),
                new Dictionary<string, string>
                {
                    ["programs/controller.js"] = new string('a', OpponentPackage.MaxProgramBytes + 1),
                }));

        Assert.Equal("PACKAGE_TOO_LARGE", exception.Code);
    }

    private static OpponentPackageManifest Manifest(
        int schemaVersion = OpponentPackage.CurrentSchemaVersion,
        string[]? entryPoints = null) =>
        new(
            schemaVersion,
            "test.package",
            "0.1.0",
            "offline-opponent",
            OpponentPackage.CurrentEngineCompat,
            entryPoints ?? new[] { "programs/controller.js" },
            new[] { "tactical-control" },
            string.Empty);
}
