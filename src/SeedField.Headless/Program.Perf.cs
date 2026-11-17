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

            // Step() is engine work, not observation work; keep it out of the timed region.
            stopwatch.Stop();
            engine.Step();
            stopwatch.Start();
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
        // Serialize the compact controller view that Main actually ships, not the
        // full device view that only tests/tools use.
        var observations = world.EnumerateEntities()
            .Where(entity => entity.CivilizationId == "A")
            .Select(entity => engine.BuildControllerObservation(entity.Id))
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

    private static int RunJintBenchmark(int devices, bool compact = true)
    {
        var world = BuildPerfWorld(devices);
        var engine = new SimulationEngine(world);
        // A device-scoped script: the compact view contains only the viewer and sensed
        // enemies, so the civilization-level ScriptA would return no intents for almost
        // every device and would measure an idle script rather than real controller work.
        var controllers = world.EnumerateEntities()
            .Where(entity => entity.CivilizationId == "A")
            .Select(entity => new JsController("A", PerfDeviceScript))
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
                var observation = compact
                    ? engine.BuildControllerObservation(aIds[index])
                    : engine.BuildDeviceObservation(aIds[index]);
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
            $"runMs={runMs / ticks:0.00} view={(compact ? "compact" : "full")}");
        return 0;
    }

    /// <summary>
    /// Device-scoped stand-in for the shipped controller script. It only commands its
    /// own viewer, so every device does comparable work under the compact observation.
    /// </summary>
    private const string PerfDeviceScript = """
        function decide(obs) {
            var me = null;
            for (var i = 0; i < obs.entities.length; i++) {
                if (obs.entities[i].entityId === obs.viewerEntityId) {
                    me = obs.entities[i];
                }
            }
            if (!me) {
                return [];
            }

            var enemies = obs.entities.filter(function (e) { return !e.isSelf; });
            var target = null;
            var best = 1e18;
            for (var j = 0; j < enemies.length; j++) {
                var dx = enemies[j].x - me.x;
                var dy = enemies[j].y - me.y;
                var d = Math.sqrt(dx * dx + dy * dy);
                if (d < best) {
                    best = d;
                    target = enemies[j];
                }
            }

            if (!target) {
                return [{ type: "move", entityId: me.entityId, target: { x: 10000, y: 10000 } }];
            }

            if (me.hasWeapon && best <= me.weaponRangeMm) {
                return [{ type: "attack", entityId: me.entityId, targetId: target.entityId }];
            }

            return [{
                type: "move",
                entityId: me.entityId,
                target: { x: target.x, y: target.y }
            }];
        }
        """;

    private static WorldState BuildPerfWorld(int devices)
    {
        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(devices)));
        var rows = Math.Max(1, (int)Math.Ceiling(devices / (double)columns));

        // Size the region to the grid. A fixed 20 km region silently placed entities
        // outside its bounds once devices > ~121, and the spatial index clamps
        // out-of-bounds positions into border cells, which made the query degenerate.
        var width = Math.Max(20_000, 2_800 + (columns - 1) * 1_800);
        var height = Math.Max(20_000, 2_800 + (rows - 1) * 1_800);
        var builder = new WorldBuilder().AddRegion("frontier", width, height);
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
