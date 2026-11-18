using SeedField.Simulation;

namespace SeedField.Runtime.Js.Tests;

/// <summary>
/// M1 non-programmer path: built-in templates must be selectable by blueprint and
/// produce real intents under the same observation contract as player scripts.
/// </summary>
public sealed class DeviceProgramsTests
{
    [Fact]
    public void KnownBuiltInIdsResolveSources()
    {
        Assert.True(DevicePrograms.TryGetSource(DevicePrograms.RelayId, out var relay));
        Assert.Contains("topic: \"status\"", relay, StringComparison.Ordinal);

        Assert.True(DevicePrograms.TryGetSource(DevicePrograms.IdleId, out _));
        Assert.True(DevicePrograms.TryGetSource(DevicePrograms.ScoutId, out _));
        Assert.True(DevicePrograms.TryGetSource(DevicePrograms.SentryId, out _));
    }

    [Fact]
    public void LegacyProgramIdsFallBackToCivilizationScript()
    {
        var source = DevicePrograms.ResolveSource(
            "program.strike",
            "function decide(obs) { return []; }");

        Assert.Equal("function decide(obs) { return []; }", source);
    }

    [Fact]
    public void KnownProgramIdOverridesCivilizationScript()
    {
        var source = DevicePrograms.ResolveSource(
            DevicePrograms.RelayId,
            "function decide(obs) { return []; }");

        Assert.Equal(DevicePrograms.RelaySource, source);
    }

    [Theory]
    [InlineData(true, true, true, true, DevicePrograms.SentryId)]
    [InlineData(true, false, true, true, DevicePrograms.ScoutId)]
    [InlineData(true, false, false, true, DevicePrograms.RelayId)]
    [InlineData(false, false, false, true, DevicePrograms.RelayId)]
    [InlineData(false, false, false, false, DevicePrograms.IdleId)]
    public void RoleInferencePicksTheLeastSpecializedTemplate(
        bool sensor,
        bool weapon,
        bool drive,
        bool comm,
        string expected)
    {
        var blueprint = new DeviceBlueprint(
            "test",
            0,
            "Test",
            MaxHealth: 10,
            EnergyCapacity: 0,
            Sensor: sensor ? new SensorModule(100) : null,
            Weapon: weapon ? new WeaponModule(100, 1, 1, 1) : null,
            Drive: drive ? new DriveModule(100, 1) : null,
            Comm: comm ? new CommModule(1, 0) : null);

        Assert.Equal(expected, DevicePrograms.SuggestId(blueprint));
    }

    [Fact]
    public void SentryTemplateAttacksAnEnemyInRange()
    {
        var world = World(
            Entity(
                1,
                "A",
                new Vec2(100, 100),
                sensor: new SensorModule(1_000),
                weapon: new WeaponModule(500, 5, 1, 1)),
            Entity(2, "B", new Vec2(300, 100)));
        var engine = new SimulationEngine(world);
        using var controller = new JsController("A", DevicePrograms.SentrySource);

        var decision = controller.Decide(engine.BuildControllerObservation(new EntityId(1)));

        Assert.False(decision.HasFaults);
        var attack = Assert.IsType<AttackIntent>(Assert.Single(decision.Intents));
        Assert.Equal(new EntityId(2), attack.TargetId);
    }

    [Fact]
    public void ScoutTemplateMovesAndReportsContacts()
    {
        var world = World(
            Entity(
                1,
                "A",
                new Vec2(100, 100),
                sensor: new SensorModule(2_000),
                drive: new DriveModule(100, 1),
                comm: new CommModule(1, 0)),
            Entity(2, "B", new Vec2(1_500, 100)));
        var engine = new SimulationEngine(world);
        using var controller = new JsController("A", DevicePrograms.ScoutSource);

        var decision = controller.Decide(engine.BuildControllerObservation(new EntityId(1)));

        Assert.False(decision.HasFaults);
        Assert.Contains(decision.Intents, intent => intent is MoveIntent);
        Assert.Contains(decision.Intents, intent => intent is SendMessageIntent);
    }

    [Fact]
    public void RelayTemplateSendsAHeartbeatButNotEveryTick()
    {
        var world = World(
            Entity(1, "A", new Vec2(100, 100), comm: new CommModule(1, 0)));
        var engine = new SimulationEngine(world);
        using var controller = new JsController("A", DevicePrograms.RelaySource);

        var first = controller.Decide(engine.BuildControllerObservation(new EntityId(1)));
        var second = controller.Decide(engine.BuildControllerObservation(new EntityId(1)));
        engine.Step();
        var third = controller.Decide(engine.BuildControllerObservation(new EntityId(1)));

        Assert.IsType<SendMessageIntent>(Assert.Single(first.Intents));
        Assert.Empty(second.Intents);
        Assert.Empty(third.Intents);
    }

    [Fact]
    public void RelayTemplateCompletesTheCommunicationBranchWithoutPlayerCode()
    {
        var world = World(
            Entity(1, "A", new Vec2(100, 100), comm: new CommModule(1, 0)),
            Entity(2, "A", new Vec2(200, 100), comm: new CommModule(1, 0)));
        var engine = new SimulationEngine(world);
        var progress = new TutorialProgress("A");
        using var relay = new DeviceControllerSlot(
            new EntityId(1),
            "A",
            DevicePrograms.RelaySource);

        for (var tick = 0; tick < 5; tick++)
        {
            var observation = engine.BuildControllerObservation(new EntityId(1));
            var decision = relay.Decide(observation);
            var result = engine.Step(decision.Intents);
            progress.Observe(world, result.Events);
        }

        Assert.True(progress.HasCompleted(TutorialStep.Communication));
    }

    [Fact]
    public void HarvesterTemplateEmitsMineIntent()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddDeposit(OreKind.Copper, "r0", new Vec2(500, 500), 200, 50, 5)
            .AddEntity(Entity(1, "A", new Vec2(400, 500)) with
            {
                Drill = new DrillModule(5, 600, 1),
            })
            .Build();
        var engine = new SimulationEngine(world);
        engine.Step();
        using var controller = new JsController("A", DevicePrograms.HarvesterSource);

        var decision = controller.Decide(
            engine.BuildControllerObservation(new EntityId(1)));

        Assert.IsType<MineIntent>(Assert.Single(decision.Intents));
    }

    [Fact]
    public void HarvesterTemplateMovesTowardAFarDeposit()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddDeposit(OreKind.Lead, "r0", new Vec2(1_500, 500), 200, 50, 5)
            .AddEntity(Entity(
                1,
                "A",
                new Vec2(500, 500),
                sensor: new SensorModule(2_000),
                drive: new DriveModule(100, 1)) with
            {
                Drill = new DrillModule(5, 100, 1),
            })
            .Build();
        var engine = new SimulationEngine(world);
        engine.Step();
        using var controller = new JsController("A", DevicePrograms.HarvesterSource);

        var decision = controller.Decide(
            engine.BuildControllerObservation(new EntityId(1)));

        var move = Assert.IsType<MoveIntent>(Assert.Single(decision.Intents));
        Assert.Equal(new Vec2(1_500, 500), move.Target);
    }

    [Fact]
    public void DrillRoleSuggestsHarvester()
    {
        var blueprint = new DeviceBlueprint(
            "drill",
            0,
            "Drill",
            MaxHealth: 10,
            EnergyCapacity: 0,
            Drill: new DrillModule(5, 600, 1));

        Assert.Equal(DevicePrograms.HarvesterId, DevicePrograms.SuggestId(blueprint));
    }

    [Fact]
    public void ScriptsCanPausePassiveProduction()
    {
        var world = World(Entity(1, "A", new Vec2(100, 100)));
        var engine = new SimulationEngine(world);
        using var controller = new JsController(
            "A",
            """
            function decide(obs) {
                return [{ type: "production", entityId: 1, enabled: false }];
            }
            """);

        var decision = controller.Decide(
            engine.BuildControllerObservation(new EntityId(1)));

        var intent = Assert.IsType<SetProductionIntent>(Assert.Single(decision.Intents));
        Assert.False(intent.Enabled);
    }

    [Fact]
    public void IdleTemplateReturnsNoIntents()
    {
        var world = World(Entity(1, "A", new Vec2(100, 100)));
        var engine = new SimulationEngine(world);
        using var controller = new JsController("A", DevicePrograms.IdleSource);

        var decision = controller.Decide(engine.BuildControllerObservation(new EntityId(1)));

        Assert.False(decision.HasFaults);
        Assert.Empty(decision.Intents);
    }

    private static WorldState World(params EntityDefinition[] entities)
    {
        var builder = new WorldBuilder().AddRegion("r0", 10_000, 10_000);
        foreach (var entity in entities)
        {
            builder.AddEntity(entity);
        }

        return builder.Build();
    }

    private static EntityDefinition Entity(
        int id,
        string civilizationId,
        Vec2 position,
        SensorModule? sensor = null,
        WeaponModule? weapon = null,
        DriveModule? drive = null,
        CommModule? comm = null) =>
        new(
            new EntityId(id),
            civilizationId,
            "r0",
            position,
            MaxHealth: 100,
            Energy: 1_000,
            EnergyCapacity: 1_000,
            Material: 0,
            Sensor: sensor,
            Weapon: weapon,
            Drive: drive,
            Comm: comm);
}
