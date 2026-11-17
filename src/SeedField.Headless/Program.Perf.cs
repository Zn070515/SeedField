using System.Diagnostics;
using SeedField.Runtime.Js;
using SeedField.Simulation;

namespace SeedField.Headless;

internal static partial class Program
{
    private static int RunObservationBenchmark(int devices)
    {
        var world = BuildPerfWorld(devices);
        var engine = new SimulationEngine(world);
        var aIds = world.EnumerateEntities()
            .Where(entity => entity.CivilizationId == "A")
            .Select(entity => entity.Id)
            .ToList();

        for (var tick = 0; tick < 2; tick++)
        {
            foreach (var id in aIds)
            {
                engine.BuildDeviceObservation(id);
            }

            engine.Step();
        }

        var stopwatch = Stopwatch.StartNew();
        for (var tick = 0; tick < 20; tick++)
        {
            foreach (var id in aIds)
            {
                engine.BuildDeviceObservation(id);
            }

            engine.Step();
        }

        stopwatch.Stop();
        var observations = aIds.Count * 20;
        var milliseconds = stopwatch.Elapsed.TotalMilliseconds;
        Console.WriteLine(
            $"OBS_BENCH devices={devices} observations={observations} " +
            $"totalMs={milliseconds:0.00} avgMs={milliseconds / observations:0.000} " +
            $"tick={world.Tick}");
        return 0;
    }

    private static int RunSerializeBenchmark(int devices)
    {
        var world = BuildPerfWorld(devices);
        var engine = new SimulationEngine(world);
        var observations = world.EnumerateEntities()
            .Where(entity => entity.CivilizationId == "A")
            .Select(entity => engine.BuildDeviceObservation(entity.Id))
            .ToList();
        for (var index = 0; index < 2; index++)
        {
            foreach (var observation in observations)
            {
                JsController.SerializeObservation(observation);
            }
        }

        var stopwatch = Stopwatch.StartNew();
        for (var index = 0; index < 10; index++)
        {
            foreach (var observation in observations)
            {
                JsController.SerializeObservation(observation);
            }
        }

        stopwatch.Stop();
        var total = stopwatch.Elapsed.TotalMilliseconds;
        var count = observations.Count * 10;
        Console.WriteLine(
            $"SERIALIZE_BENCH devices={devices} serializations={count} " +
            $"totalMs={total:0.00} avgMs={total / count:0.000}");
        return 0;
    }

    private static int RunJintBenchmark(int devices)
    {
        var world = BuildPerfWorld(devices);
        var engine = new SimulationEngine(world);
        var controllers = world.EnumerateEntities()
            .Where(entity => entity.CivilizationId == "A")
            .Select(entity => new JsController("A", ScriptA))
            .ToList();
        var aIds = world.EnumerateEntities()
            .Where(entity => entity.CivilizationId == "A")
            .Select(entity => entity.Id)
            .ToList();

        for (var tick = 0; tick < 2; tick++)
        {
            var intents = new List<Intent>();
            for (var index = 0; index < aIds.Count; index++)
            {
                intents.AddRange(controllers[index]
                    .Decide(engine.BuildControllerObservation(aIds[index]))
                    .Intents);
            }

            engine.Step(intents);
        }

        var observationMs = 0.0;
        var serializeMs = 0.0;
        var runMs = 0.0;
        var stopwatch = Stopwatch.StartNew();
        for (var tick = 0; tick < 10; tick++)
        {
            var intents = new List<Intent>();
            for (var index = 0; index < aIds.Count; index++)
            {
                var phase = Stopwatch.StartNew();
                var observation = engine.BuildControllerObservation(aIds[index]);
                phase.Stop();
                observationMs += phase.Elapsed.TotalMilliseconds;

                phase.Restart();
                var json = JsController.SerializeObservation(observation);
                phase.Stop();
                serializeMs += phase.Elapsed.TotalMilliseconds;

                phase.Restart();
                var run = controllers[index].RunJson(json);
                if (run.Success)
                {
                    intents.AddRange(
                        JsController.ParseDecisionJson(run.IntentsJson!, "A").Intents);
                }
                else
                {
                    intents.AddRange(ControllerDecision.Fault(
                        run.FaultCode!,
                        run.FaultMessage!).Intents);
                }

                phase.Stop();
                runMs += phase.Elapsed.TotalMilliseconds;
            }

            engine.Step(intents);
        }

        stopwatch.Stop();
        foreach (var controller in controllers)
        {
            controller.Dispose();
        }

        var ticks = 10;
        var milliseconds = stopwatch.Elapsed.TotalMilliseconds;
        Console.WriteLine(
            $"JINT_BENCH devices={devices} entities={world.EnumerateEntities().Count()} " +
            $"ticks={ticks} totalMs={milliseconds:0.00} avgTickMs={milliseconds / ticks:0.00} " +
            $"ticksPerSec={1000.0 / (milliseconds / ticks):0.0} " +
            $"obsMs={observationMs / ticks:0.00} serMs={serializeMs / ticks:0.00} " +
            $"runMs={runMs / ticks:0.00}");
        return 0;
    }

    private static WorldState BuildPerfWorld(int devices)
    {
        var builder = new WorldBuilder().AddRegion("frontier", 20_000, 20_000);
        var columns = (int)Math.Ceiling(Math.Sqrt(devices));
        for (var index = 0; index < devices; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var position = new Vec2(1_000 + column * 1_800, 1_000 + row * 1_800);
            builder.AddEntity(new EntityDefinition(
                new EntityId(index + 1),
                "A",
                "frontier",
                position,
                MaxHealth: 120,
                Energy: 5_000,
                EnergyCapacity: 5_000,
                Material: 0,
                Sensor: new SensorModule(6_000),
                Drive: new DriveModule(120, EnergyCostPerMove: 1),
                Comm: new CommModule(1, EnergyCostPerMessage: 1)));

            builder.AddEntity(new EntityDefinition(
                new EntityId(devices + index + 1),
                "B",
                "frontier",
                new Vec2(10_000 + column * 700, 1_000 + row * 1_800),
                MaxHealth: 120,
                Energy: 5_000,
                EnergyCapacity: 5_000,
                Material: 0,
                Sensor: new SensorModule(6_000),
                Weapon: new WeaponModule(5_000, Damage: 8, EnergyCost: 2, CooldownTicks: 2),
                Drive: new DriveModule(90, EnergyCostPerMove: 1),
                Comm: new CommModule(1, EnergyCostPerMessage: 1)));
        }

        return builder.Build();
    }
}
