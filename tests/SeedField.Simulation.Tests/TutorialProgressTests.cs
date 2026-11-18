using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

/// <summary>
/// M1 admission: five minimum-usable branches must be verified by functional facts,
/// not by blueprint presence alone. Efficiency is deliberately irrelevant.
/// </summary>
public sealed class TutorialProgressTests
{
    [Fact]
    public void EnergyRequiresPowerThatActuallyChargesTheEntity()
    {
        var world = World(Entity(
            1,
            "A",
            new Vec2(0, 0),
            energy: 0,
            power: new PowerModule(EnergyPerTick: 1, StorageCapacity: 1)));
        var engine = new SimulationEngine(world);
        var progress = new TutorialProgress("A");

        progress.Observe(world, Array.Empty<SimulationEvent>());
        Assert.False(progress.HasCompleted(TutorialStep.Energy));

        var result = engine.Step();
        progress.Observe(world, result.Events);

        Assert.True(progress.HasCompleted(TutorialStep.Energy));
        Assert.Equal(1, progress.CompletedCount);
    }

    [Fact]
    public void EnergyModuleWithoutChargeIsNotEnough()
    {
        var world = World(Entity(
            1,
            "A",
            new Vec2(0, 0),
            energy: 0,
            power: new PowerModule(EnergyPerTick: 0, StorageCapacity: 100)));
        var progress = new TutorialProgress("A");

        progress.Observe(world, Array.Empty<SimulationEvent>());

        Assert.False(progress.HasCompleted(TutorialStep.Energy));
    }

    [Fact]
    public void SensingRequiresAForeignContactInsideSensorRange()
    {
        var far = World(
            Entity(1, "A", new Vec2(0, 0), sensor: new SensorModule(100)),
            Entity(2, "B", new Vec2(500, 0)));
        var near = World(
            Entity(1, "A", new Vec2(0, 0), sensor: new SensorModule(100)),
            Entity(2, "B", new Vec2(50, 0)));

        var farProgress = new TutorialProgress("A");
        farProgress.Observe(far, Array.Empty<SimulationEvent>());
        Assert.False(farProgress.HasCompleted(TutorialStep.Sensing));

        var nearProgress = new TutorialProgress("A");
        nearProgress.Observe(near, Array.Empty<SimulationEvent>());
        Assert.True(nearProgress.HasCompleted(TutorialStep.Sensing));
    }

    [Fact]
    public void SensingIgnoresOwnEntities()
    {
        var world = World(
            Entity(1, "A", new Vec2(0, 0), sensor: new SensorModule(100)),
            Entity(2, "A", new Vec2(10, 0)));
        var progress = new TutorialProgress("A");

        progress.Observe(world, Array.Empty<SimulationEvent>());

        Assert.False(progress.HasCompleted(TutorialStep.Sensing));
    }

    [Fact]
    public void CommunicationRequiresADeliveredMessage()
    {
        var world = World(Entity(1, "A", new Vec2(0, 0)));
        var progress = new TutorialProgress("A");
        var toB = new MessageDelivered(1, 1, new EntityId(1), "B", "r0", "contact");

        progress.Observe(world, new SimulationEvent[] { toB });
        Assert.False(progress.HasCompleted(TutorialStep.Communication));

        var toA = new MessageDelivered(2, 2, new EntityId(1), "A", "r0", "contact");
        progress.Observe(world, new SimulationEvent[] { toA });
        Assert.True(progress.HasCompleted(TutorialStep.Communication));
    }

    [Fact]
    public void StateRequiresAStateModuleOnTheActingEntity()
    {
        var withState = World(Entity(1, "A", new Vec2(0, 0), state: new StateModule(1, 10)));
        var withoutState = World(Entity(1, "A", new Vec2(0, 0)));
        var moved = new EntityMoved(1, new EntityId(1), new Vec2(0, 0), new Vec2(10, 0), 1);

        var stateProgress = new TutorialProgress("A");
        stateProgress.Observe(withState, new SimulationEvent[] { moved });
        Assert.True(stateProgress.HasCompleted(TutorialStep.State));

        var plainProgress = new TutorialProgress("A");
        plainProgress.Observe(withoutState, new SimulationEvent[] { moved });
        Assert.False(plainProgress.HasCompleted(TutorialStep.State));
    }

    [Fact]
    public void ExecutionRequiresAMoveOrShot()
    {
        var world = World(Entity(1, "A", new Vec2(0, 0)));
        var progress = new TutorialProgress("A");

        progress.Observe(world, Array.Empty<SimulationEvent>());
        Assert.False(progress.HasCompleted(TutorialStep.Execution));

        var shot = new ShotFired(
            3,
            new EntityId(1),
            new EntityId(2),
            5,
            new Vec2(0, 0),
            new Vec2(10, 0),
            1);
        progress.Observe(world, new SimulationEvent[] { shot });

        Assert.True(progress.HasCompleted(TutorialStep.Execution));
    }

    [Fact]
    public void AllFiveBranchesUnlockPveAndKeepTheirFirstCompletionTick()
    {
        var world = World(
            Entity(
                1,
                "A",
                new Vec2(0, 0),
                energy: 0,
                energyCapacity: 10,
                power: new PowerModule(1, 10),
                sensor: new SensorModule(100),
                state: new StateModule(1, 10)),
            Entity(2, "B", new Vec2(50, 0)));
        var engine = new SimulationEngine(world);
        var progress = new TutorialProgress("A");

        var events = new List<SimulationEvent>
        {
            new MessageDelivered(1, 1, new EntityId(1), "A", "r0", "contact"),
            new EntityMoved(1, new EntityId(1), new Vec2(0, 0), new Vec2(10, 0), 1),
        };
        progress.Observe(world, events);

        Assert.False(progress.HasCompleted(TutorialStep.Energy));
        Assert.Equal(4, progress.CompletedCount);

        engine.Step();
        progress.Observe(world, Array.Empty<SimulationEvent>());

        Assert.True(progress.PveUnlocked);
        Assert.Equal(5, progress.CompletedCount);
        Assert.Equal(DeviceBranch.All, progress.CoveredBranches);

        var energyTick = progress.CompletionTick(TutorialStep.Energy);
        engine.Step();
        progress.Observe(world, Array.Empty<SimulationEvent>());

        Assert.Equal(energyTick, progress.CompletionTick(TutorialStep.Energy));
        Assert.Empty(progress.Observe(world, Array.Empty<SimulationEvent>()));
    }

    [Fact]
    public void EventsFromOtherCivilizationsDoNotCount()
    {
        var world = World(
            Entity(1, "A", new Vec2(0, 0)),
            Entity(2, "B", new Vec2(10, 0), state: new StateModule(1, 10)));
        var progress = new TutorialProgress("A");
        var foreignMove = new EntityMoved(
            1,
            new EntityId(2),
            new Vec2(10, 0),
            new Vec2(20, 0),
            1);

        progress.Observe(world, new SimulationEvent[] { foreignMove });

        Assert.Equal(0, progress.CompletedCount);
    }

    [Fact]
    public void ProgressIsDeterministicForTheSameFacts()
    {
        var world = World(
            Entity(1, "A", new Vec2(0, 0), sensor: new SensorModule(10)),
            Entity(2, "B", new Vec2(5, 0)));
        var events = new SimulationEvent[]
        {
            new EntityMoved(1, new EntityId(1), new Vec2(0, 0), new Vec2(1, 0), 1),
        };

        var first = new TutorialProgress("A");
        var second = new TutorialProgress("A");
        var firstNew = first.Observe(world, events);
        var secondNew = second.Observe(world, events);

        Assert.Equal(firstNew, secondNew);
        Assert.Equal(first.Completed, second.Completed);
    }

    [Fact]
    public void RestoreKeepsCompletedTicks()
    {
        var progress = TutorialProgress.Restore(
            "A",
            new Dictionary<TutorialStep, long>
            {
                [TutorialStep.Execution] = 2,
                [TutorialStep.Energy] = 7,
            });

        Assert.Equal(2, progress.CompletedCount);
        Assert.Equal(7, progress.CompletionTick(TutorialStep.Energy));
        Assert.False(progress.PveUnlocked);
    }

    [Fact]
    public void RestoreRejectsNegativeTick()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TutorialProgress.Restore(
                "A",
                new Dictionary<TutorialStep, long>
                {
                    [TutorialStep.Energy] = -1,
                }));
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
        long energy = 100,
        long energyCapacity = 100,
        PowerModule? power = null,
        SensorModule? sensor = null,
        StateModule? state = null) =>
        new(
            new EntityId(id),
            civilizationId,
            "r0",
            position,
            MaxHealth: 10,
            Energy: energy,
            EnergyCapacity: energyCapacity,
            Material: 0,
            Sensor: sensor,
            Power: power,
            State: state);
}
