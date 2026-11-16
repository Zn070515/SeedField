using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class CoreProtectionTests
{
    [Fact]
    public void ForeignDeploymentIntoACoreRegionIsRejected()
    {
        var world = NewCoreWorld();
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "probe",
            0,
            "Probe",
            10,
            10,
            Power: new PowerModule(1, 10)));

        var exception = Assert.Throws<BlueprintValidationException>(() =>
            DeploymentService.Deploy(world, blueprint, "B", "core", new Vec2(100, 100)));

        Assert.Equal("CORE_PROTECTED", exception.Code);
        Assert.Empty(world.EnumerateEntities());
    }

    [Fact]
    public void ForeignEntityInsideACoreRegionCannotAct()
    {
        var world = new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddRegion("wilds", 10_000, 10_000)
            .AddCore("A", "core", new SeedCoreConfig(1, 1, 100, 100))
            .AddEntity(Unit(1, "A", "core"))
            .AddEntity(Unit(2, "B", "core"))
            .Build();

        var engine = new SimulationEngine(world);

        var foreign = engine.Step(new Intent[]
        {
            new MoveIntent(new EntityId(2), new Vec2(500, 500)),
        });
        Assert.Equal(SimulationErrorCode.CoreProtected, Assert.Single(foreign.IntentResults).ErrorCode);
        Assert.Equal(new Vec2(100, 100), world.FindEntity(new EntityId(2))!.Position);

        var owner = engine.Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
        });
        Assert.True(Assert.Single(owner.IntentResults).Accepted);
        Assert.Equal(new Vec2(500, 500), world.FindEntity(new EntityId(1))!.Position);
    }

    [Fact]
    public void OwnerCanDeployIntoItsOwnCoreRegion()
    {
        var world = NewCoreWorld();
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "core-unit",
            0,
            "Core Unit",
            10,
            10,
            Power: new PowerModule(1, 10)));

        var entity = DeploymentService.Deploy(world, blueprint, "A", "core", new Vec2(100, 100));

        Assert.Equal("A", entity.CivilizationId);
        Assert.Equal("core", entity.RegionId);
    }

    [Fact]
    public void SaveRejectsForeignEntityInsideACoreRegion()
    {
        var world = new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddRegion("wilds", 10_000, 10_000)
            .AddCore("A", "core", new SeedCoreConfig(1, 1, 100, 100))
            .AddEntity(Unit(1, "A", "core"))
            .AddEntity(Unit(2, "B", "core"))
            .Build();

        var json = WorldSaveSerializer.Save(world);
        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Load(json));

        Assert.Equal("FOREIGN_ENTITY_IN_CORE", exception.Code);
    }

    private static WorldState NewCoreWorld() =>
        new WorldBuilder()
            .AddRegion("core", 1_000, 1_000, isCoreRegion: true)
            .AddRegion("wilds", 10_000, 10_000)
            .AddCore("A", "core", new SeedCoreConfig(1, 1, 100, 100))
            .Build();

    private static EntityDefinition Unit(long id, string civilizationId, string regionId) =>
        new(
            new EntityId(id),
            civilizationId,
            regionId,
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: 100,
            EnergyCapacity: 100,
            Material: 0,
            Drive: new DriveModule(1_000, EnergyCostPerMove: 1));
}
