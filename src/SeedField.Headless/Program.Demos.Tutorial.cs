using SeedField.Simulation;

namespace SeedField.Headless;

internal static partial class Program
{
    /// <summary>
    /// M1 admission demo: the five minimum-usable branches are verified through real
    /// simulation facts (generation, sensor contact, delivery, state-backed action,
    /// movement), not by blueprint metadata.
    /// </summary>
    private static int RunTutorialDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(1_000, 1_000),
                MaxHealth: 100,
                Energy: 0,
                EnergyCapacity: 100,
                Material: 0,
                Sensor: new SensorModule(RangeMm: 2_000),
                Drive: new DriveModule(SpeedMmPerTick: 200, EnergyCostPerMove: 1),
                Comm: new CommModule(LatencyTicks: 1, EnergyCostPerMessage: 1),
                Power: new PowerModule(EnergyPerTick: 2, StorageCapacity: 100),
                State: new StateModule(ComputePerTick: 2, StorageCapacity: 10)))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "A",
                "r0",
                new Vec2(1_200, 1_000),
                MaxHealth: 100,
                Energy: 50,
                EnergyCapacity: 50,
                Material: 0,
                Comm: new CommModule(LatencyTicks: 1, EnergyCostPerMessage: 1)))
            .AddEntity(new EntityDefinition(
                new EntityId(3),
                "B",
                "r0",
                new Vec2(1_500, 1_000),
                MaxHealth: 100,
                Energy: 50,
                EnergyCapacity: 50,
                Material: 0))
            .Build();

        var engine = new SimulationEngine(world);
        var progress = new TutorialProgress("A");

        progress.Observe(world, engine.Step().Events);

        var firstActions = engine.Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(1_400, 1_000)),
            new SendMessageIntent(new EntityId(1), "A", "status", "arrived"),
        });
        progress.Observe(world, firstActions.Events);

        for (var tick = 0; tick < 4 && !progress.PveUnlocked; tick++)
        {
            progress.Observe(world, engine.Step().Events);
        }

        var energy = progress.CompletionTick(TutorialStep.Energy);
        var sensing = progress.CompletionTick(TutorialStep.Sensing);
        var communication = progress.CompletionTick(TutorialStep.Communication);
        var state = progress.CompletionTick(TutorialStep.State);
        var execution = progress.CompletionTick(TutorialStep.Execution);

        Console.WriteLine(
            $"TUTORIAL completed={progress.CompletedCount}/5 " +
            $"energy={energy} sensing={sensing} communication={communication} " +
            $"state={state} execution={execution} " +
            $"pveReady={progress.PveUnlocked}");

        return progress.PveUnlocked ? 0 : 1;
    }
}
