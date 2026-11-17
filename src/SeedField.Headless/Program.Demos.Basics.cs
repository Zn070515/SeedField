using SeedField.Runtime.Js;
using SeedField.Runtime.Js.Worker;
using SeedField.Simulation;

namespace SeedField.Headless;

internal static partial class Program
{
    private static int RunWorkerOpponentDemo()
    {
        var inProcess = RunScenario();
        ControllerFactory workerFactory = (civilizationId, script, options) =>
            new WorkerController(
                civilizationId,
                script,
                new WorkerControllerOptions
                {
                    ControllerOptions = options ?? new JsControllerOptions(),
                });

        var first = RunScenario(workerFactory);
        var second = RunScenario(workerFactory);
        var match = inProcess.FinalHash == first.FinalHash &&
                    first.FinalHash == second.FinalHash;
        Console.WriteLine(
            $"WORKER_OPPONENT inProcess={inProcess.FinalHash:x16} " +
            $"worker1={first.FinalHash:x16} worker2={second.FinalHash:x16} " +
            $"match={match}");

        return match ? 0 : 1;
    }

    private static int RunMultiRegionDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 1_000, 1_000)
            .AddRegion("r1", 1_000, 1_000)
            .AddRegion("r2", 1_000, 1_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 2)
            .AddLink("l12", "r1", "r2", latencyTicks: 3)
            .AddEntity(CommsEndpoint(1, "r0"))
            .AddEntity(CommsEndpoint(2, "r1"))
            .AddEntity(CommsEndpoint(3, "r2"))
            .Build();

        var engine = new SimulationEngine(world);
        var send = engine.Step(new Intent[]
        {
            new SendMessageIntent(new EntityId(1), "A", "route", "payload", "r2"),
        });

        var queued = send.Events.OfType<MessageQueued>().Single();
        Console.WriteLine(
            $"MULTI_REGION queued id={queued.MessageId} sentTick={queued.Tick} " +
            $"deliverTick={queued.DeliverTick} target={queued.TargetRegionId} route={queued.Route}");

        while (world.Tick < queued.DeliverTick)
        {
            engine.Step(Array.Empty<Intent>());
        }

        var message = world.GetInbox("A").Single();
        var ok = message.TargetRegionId == "r2" &&
                 message.DeliveredTick == 6 &&
                 queued.Route == "l01>l12";
        Console.WriteLine(
            $"MULTI_REGION delivered id={message.Id} tick={message.DeliveredTick} " +
            $"target={message.TargetRegionId} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunLogisticsDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 1_000, 1_000)
            .AddRegion("r1", 1_000, 1_000)
            .AddRegion("r2", 1_000, 1_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 2)
            .AddLink("l12", "r1", "r2", latencyTicks: 3)
            .AddStock("r0", "A", 100)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Drive: new DriveModule(50, EnergyCostPerMove: 1)))
            .Build();

        var engine = new SimulationEngine(world);
        var result = engine.Step(new Intent[]
        {
            new TransferMaterialIntent(new EntityId(1), "r2", 40),
        });

        var shipped = result.Events.OfType<MaterialShipped>().Single();
        Console.WriteLine(
            $"LOGISTICS shipped amount={shipped.Amount} arriveTick={shipped.ArriveTick} " +
            $"route={shipped.Route} source={world.Regions["r0"].GetMaterial("A")} " +
            $"target={world.Regions["r2"].GetMaterial("A")}");

        while (world.Tick < shipped.ArriveTick)
        {
            engine.Step(Array.Empty<Intent>());
        }

        var source = world.Regions["r0"].GetMaterial("A");
        var target = world.Regions["r2"].GetMaterial("A");
        var ok = source == 60 &&
                 target == 40 &&
                 shipped.Route == "l01>l12" &&
                 world.InTransitMaterial.Count == 0;
        Console.WriteLine(
            $"LOGISTICS arrived tick={world.Tick} source={source} target={target} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunEnergyDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 1_000, 1_000)
            .AddRegion("r1", 1_000, 1_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 3)
            .AddEntity(EnergyNode(1, "r0", energy: 100))
            .AddEntity(EnergyNode(2, "r1", energy: 0))
            .Build();

        var engine = new SimulationEngine(world);
        var shipped = engine.Step(new Intent[]
        {
            new SendEnergyIntent(new EntityId(1), 40, "r1"),
        }).Events.OfType<EnergyShipped>().Single();

        Console.WriteLine(
            $"ENERGY shipped amount={shipped.Amount} arriveTick={shipped.ArriveTick} " +
            $"route={shipped.Route} sourceLocal={world.FindEntity(new EntityId(1))!.Energy} " +
            $"targetGrid={world.Regions["r1"].GetEnergy("A")}");

        while (world.Tick < shipped.ArriveTick)
        {
            engine.Step(Array.Empty<Intent>());
        }

        engine.Step(new Intent[]
        {
            new DrawEnergyIntent(new EntityId(2), 25),
        });

        var sourceLocal = world.FindEntity(new EntityId(1))!.Energy;
        var targetLocal = world.FindEntity(new EntityId(2))!.Energy;
        var targetGrid = world.Regions["r1"].GetEnergy("A");
        var ok = sourceLocal == 60 && targetLocal == 25 && targetGrid == 15;
        Console.WriteLine(
            $"ENERGY drew targetLocal={targetLocal} targetGrid={targetGrid} " +
            $"sourceLocal={sourceLocal} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunRegionLoadDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 100,
                EnergyCapacity: 100,
                Material: 0,
                Drive: new DriveModule(1_000, EnergyCostPerMove: 1)))
            .Build();

        var engine = new SimulationEngine(world);
        engine.DeactivateRegion("r0");

        var rejected = engine.Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
        });
        var inactiveCode = rejected.IntentResults[0].ErrorCode;

        engine.ActivateRegion("r0");
        var accepted = engine.Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
        });

        var position = world.FindEntity(new EntityId(1))!.Position;
        var ok = inactiveCode == SimulationErrorCode.RegionInactive &&
                 accepted.IntentResults[0].Accepted &&
                 position == new Vec2(500, 500);

        Console.WriteLine(
            $"REGION_LOAD inactive={inactiveCode} activeAccepted={accepted.IntentResults[0].Accepted} " +
            $"position={position} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunConstructionDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 100)
            .Build();
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "fabricator",
            0,
            "Fabricator",
            20,
            10,
            Power: new PowerModule(1, 10),
            MaterialCost: 20,
            BuildTicks: 3));
        var engine = new SimulationEngine(world);

        var result = engine.ExecuteDeploymentCommand(new DeployCommand(
            "A",
            blueprint,
            "r0",
            new Vec2(100, 100)));

        Console.WriteLine(
            $"CONSTRUCTION queued={result.Success} entity={result.EntityId} " +
            $"orders={world.ConstructionOrders.Count} material={world.Regions["r0"].GetMaterial("A")}");

        while (world.ConstructionOrders.Count > 0)
        {
            engine.Step(Array.Empty<Intent>());
        }

        var entity = world.EnumerateEntities().SingleOrDefault();
        var ok = result.Success &&
                 result.EntityId is null &&
                 entity is not null &&
                 world.Regions["r0"].GetMaterial("A") == 80;
        Console.WriteLine(
            $"CONSTRUCTION completed tick={world.Tick} entity={entity?.Id} " +
            $"material={world.Regions["r0"].GetMaterial("A")} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunConstructionCancelDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 100)
            .Build();
        var blueprint = world.Workshop.Publish(new DeviceBlueprint(
            "fabricator",
            0,
            "Fabricator",
            20,
            10,
            Power: new PowerModule(1, 10),
            MaterialCost: 20,
            BuildTicks: 3));
        var engine = new SimulationEngine(world);
        engine.ExecuteDeploymentCommand(new DeployCommand("A", blueprint, "r0", new Vec2(100, 100)));
        var orderId = world.ConstructionOrders.Single().Id;

        var result = engine.ExecuteDeploymentCommand(new CancelConstructionCommand("A", orderId));

        var ok = result.Success &&
                 world.ConstructionOrders.Count == 0 &&
                 world.Regions["r0"].GetMaterial("A") == 100 &&
                 world.Statistics.MaterialRefundedTotal == 20;
        Console.WriteLine(
            $"CONSTRUCTION_CANCEL ok={ok} orders={world.ConstructionOrders.Count} " +
            $"material={world.Regions["r0"].GetMaterial("A")} " +
            $"refunded={world.Statistics.MaterialRefundedTotal}");

        return ok ? 0 : 1;
    }

}
