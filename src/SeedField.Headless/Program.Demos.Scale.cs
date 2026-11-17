using System.Diagnostics;
using SeedField.Runtime.Js;
using SeedField.Simulation;

namespace SeedField.Headless;

internal static partial class Program
{
    private const string ScaleWaitScript =
        "function decide(obs) { return [{ type: \"wait\", entityId: obs.viewerEntityId }]; }";

    private static int RunRegionScaleDemo()
    {
        var activeOnly = MeasureScale(activeRegions: 1);
        var allActive = MeasureScale(activeRegions: 8);
        Console.WriteLine(
            $"REGION_SCALE regions=8 devicesPerRegion=50 ticks=20 " +
            $"activeOnlyMs={activeOnly:0.00} allActiveMs={allActive:0.00} " +
            $"ratio={allActive / activeOnly:0.00}");
        return 0;
    }

    private static double MeasureScale(int activeRegions)
    {
        var (world, controllers) = BuildScaleWorld(8, 50);
        var engine = new SimulationEngine(world);
        for (var region = activeRegions; region < 8; region++)
        {
            engine.DeactivateRegion($"r{region}");
        }

        void Tick()
        {
            var intents = new List<Intent>(controllers.Count);
            foreach (var (id, controller) in controllers)
            {
                if (!engine.IsEntityActive(id))
                {
                    continue;
                }

                intents.AddRange(
                    controller.Decide(engine.BuildControllerObservation(id)).Intents);
            }

            engine.Step(intents);
        }

        for (var warm = 0; warm < 2; warm++)
        {
            Tick();
        }

        var stopwatch = Stopwatch.StartNew();
        for (var tick = 0; tick < 20; tick++)
        {
            Tick();
        }

        stopwatch.Stop();
        foreach (var controller in controllers.Values)
        {
            controller.Dispose();
        }

        return stopwatch.Elapsed.TotalMilliseconds / 20;
    }

    private static (WorldState World, Dictionary<EntityId, DeviceControllerSlot> Controllers)
        BuildScaleWorld(int regions, int devicesPerRegion)
    {
        var builder = new WorldBuilder();
        for (var region = 0; region < regions; region++)
        {
            builder.AddRegion($"r{region}", 20_000, 20_000);
            if (region > 0)
            {
                builder.AddLink(
                    $"l{region - 1}",
                    $"r{region - 1}",
                    $"r{region}",
                    latencyTicks: 1,
                    powerPerTick: 10);
            }
        }

        var id = 1L;
        for (var region = 0; region < regions; region++)
        {
            for (var index = 0; index < devicesPerRegion; index++)
            {
                builder.AddEntity(new EntityDefinition(
                    new EntityId(id++),
                    "A",
                    $"r{region}",
                    new Vec2(100 + (index % 10) * 100, 100 + (index / 10) * 100),
                    MaxHealth: 10,
                    Energy: 10,
                    EnergyCapacity: 100,
                    Material: 0,
                    Power: new PowerModule(EnergyPerTick: 1, StorageCapacity: 100)));
            }
        }

        var world = builder.Build();
        var controllers = world.EnumerateEntities()
            .ToDictionary(
                entity => entity.Id,
                entity => new DeviceControllerSlot(entity.Id, "A", ScaleWaitScript));
        return (world, controllers);
    }
}
