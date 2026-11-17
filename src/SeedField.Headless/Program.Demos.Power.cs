using SeedField.Runtime.Js;
using SeedField.Simulation;

namespace SeedField.Headless;

internal static partial class Program
{
    private static int RunPowerGridDemo()
    {
        var flowWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddRegion("r2", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 1, powerPerTick: 100)
            .AddLink("l12", "r1", "r2", latencyTicks: 1, powerPerTick: 100)
            .Build();
        flowWorld.Regions["r0"].AddEnergy("A", 90);
        new SimulationEngine(flowWorld).Step(Array.Empty<Intent>());

        var balanced =
            flowWorld.Regions["r0"].GetEnergy("A") == 30 &&
            flowWorld.Regions["r1"].GetEnergy("A") == 30 &&
            flowWorld.Regions["r2"].GetEnergy("A") == 30 &&
            flowWorld.Statistics.EnergyFlowedTotal == 60;
        var flowDiagnostics = PowerGridDiagnosticsBuilder.Build(flowWorld, "A");

        var blackoutWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddLink("l01", "r0", "r1", latencyTicks: 1, powerPerTick: 100)
            .AddEntity(EnergyNode(1, "r0", 0))
            .Build();
        var blackoutDiagnostics = PowerGridDiagnosticsBuilder.Build(blackoutWorld, "A");
        var blackout = blackoutDiagnostics.Regions.All(region => region.IsBlackedOut);
        var shortfallStep = new SimulationEngine(blackoutWorld).Step(new Intent[]
        {
            new DrawEnergyIntent(new EntityId(1), 10),
        });
        var shortfall = shortfallStep.Events
            .OfType<PowerShortfall>()
            .Any(item => item.Source is "grid" or "blackout");

        var ok = balanced && blackout && shortfall;
        Console.WriteLine(
            $"POWER_GRID balanced={balanced} flowed={flowWorld.Statistics.EnergyFlowedTotal} " +
            $"grid={flowDiagnostics.TotalGridEnergy} " +
            $"regions={flowDiagnostics.Regions.Count} " +
            $"components={PowerGridTopology.BuildComponents(flowWorld).Count} " +
            $"blackout={blackout} shortfall={shortfall} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunPowerRecoveryDemo()
    {
        var overflowWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(PowerNode(1, "r0", energy: 9, capacity: 10, generation: 2))
            .Build();
        new SimulationEngine(overflowWorld).Step(Array.Empty<Intent>());
        var overflow = overflowWorld.Statistics.EnergyOverflowedTotal == 1 &&
                       overflowWorld.Regions["r0"].GetEnergy("A") == 1;

        var drawWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(PowerNode(
                1,
                "r0",
                energy: 0,
                capacity: 100,
                generation: 0,
                driveCost: 5))
            .Build();
        drawWorld.Regions["r0"].AddEnergy("A", 20);
        var drawStep = new SimulationEngine(drawWorld).Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
        });
        var autoDraw = drawStep.IntentResults[0].Accepted &&
                       drawWorld.Statistics.EnergyAutoDrawnTotal == 5 &&
                       drawWorld.Regions["r0"].GetEnergy("A") == 15;

        var reserveWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetPowerPolicy("A", reserveEnergy: 10)
            .AddEntity(PowerNode(
                1,
                "r0",
                energy: 0,
                capacity: 100,
                generation: 0,
                driveCost: 5))
            .Build();
        reserveWorld.Regions["r0"].AddEnergy("A", 12);
        var reserveStep = new SimulationEngine(reserveWorld).Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
        });
        var reserveShed =
            reserveStep.IntentResults[0].ErrorCode == SimulationErrorCode.NoEnergy &&
            reserveStep.Events.OfType<PowerShortfall>()
                .Any(shortfall => shortfall.Source == "reserve");

        var transitWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddLink(
                "l01",
                "r0",
                "r1",
                latencyTicks: 1,
                powerPerTick: 100,
                powerLatencyTicks: 2)
            .Build();
        transitWorld.Regions["r0"].AddEnergy("A", 90);
        var transitEngine = new SimulationEngine(transitWorld);
        transitEngine.Step(Array.Empty<Intent>());
        var latency = transitWorld.InTransitPower.Count == 1 &&
                      transitWorld.Regions["r0"].GetEnergy("A") == 45 &&
                      transitWorld.Regions["r1"].GetEnergy("A") == 0;
        transitEngine.Step(Array.Empty<Intent>());
        var latencyArrived = transitWorld.InTransitPower.Count == 0 &&
                             transitWorld.Regions["r1"].GetEnergy("A") == 45;

        var lossWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddLink(
                "l01",
                "r0",
                "r1",
                latencyTicks: 1,
                powerPerTick: 100,
                powerLossPercent: 10)
            .Build();
        lossWorld.Regions["r0"].AddEnergy("A", 90);
        new SimulationEngine(lossWorld).Step(Array.Empty<Intent>());
        var lossArrived = lossWorld.Regions["r0"].GetEnergy("A") == 45 &&
                          lossWorld.Regions["r1"].GetEnergy("A") == 40 &&
                          lossWorld.Statistics.EnergyFlowedTotal == 40 &&
                          lossWorld.Statistics.EnergyFlowLossTotal == 5;

        var ok = overflow &&
                 autoDraw &&
                 reserveShed &&
                 latency &&
                 latencyArrived &&
                 lossArrived;
        Console.WriteLine(
            $"POWER_RECOVERY overflow={overflow} autoDraw={autoDraw} " +
            $"reserveShed={reserveShed} latency={latency} " +
            $"latencyArrived={latencyArrived} lossArrived={lossArrived} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunPowerLimitsDemo()
    {
        var capacityWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetGridCapacity("r0", "A", capacity: 2)
            .AddEntity(PowerNode(1, "r0", energy: 9, capacity: 10, generation: 5))
            .Build();
        new SimulationEngine(capacityWorld).Step(Array.Empty<Intent>());
        var capacityWaste = capacityWorld.Regions["r0"].GetEnergy("A") == 2 &&
                            capacityWorld.Statistics.EnergyWastedTotal == 2;

        var priorityWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetPowerPolicy(
                "A",
                reserveEnergy: 10,
                highPriorityThreshold: 5,
                priorityReserveEnergy: 5)
            .AddEntity(PowerNode(
                1,
                "r0",
                energy: 0,
                capacity: 100,
                generation: 0,
                driveCost: 3,
                priority: 10))
            .AddEntity(PowerNode(
                2,
                "r0",
                energy: 0,
                capacity: 100,
                generation: 0,
                driveCost: 3,
                priority: 0))
            .Build();
        priorityWorld.Regions["r0"].AddEnergy("A", 13);
        var priorityStep = new SimulationEngine(priorityWorld).Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
            new MoveIntent(new EntityId(2), new Vec2(500, 500)),
        });
        var priority = priorityStep.IntentResults.Single(item =>
                           item.Intent.EntityId == new EntityId(1)).Accepted &&
                       priorityStep.IntentResults.Single(item =>
                           item.Intent.EntityId == new EntityId(2)).ErrorCode ==
                       SimulationErrorCode.NoEnergy;

        var lossWorld = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddRegion("r1", 10_000, 10_000)
            .AddRegion("r2", 10_000, 10_000)
            .AddLink(
                "l01",
                "r0",
                "r1",
                latencyTicks: 1,
                powerPerTick: 100,
                powerLossPercent: 10)
            .AddLink(
                "l12",
                "r1",
                "r2",
                latencyTicks: 1,
                powerPerTick: 100,
                powerLossPercent: 10)
            .Build();
        lossWorld.Regions["r0"].AddEnergy("A", 100);
        new SimulationEngine(lossWorld).Step(Array.Empty<Intent>());
        var netCompensation = lossWorld.Regions["r1"].GetEnergy("A") == 34 &&
                              lossWorld.Statistics.EnergyFlowedTotal == 57;

        var ok = capacityWaste && priority && netCompensation;
        Console.WriteLine(
            $"POWER_LIMITS capacityWaste={capacityWaste} priority={priority} " +
            $"netCompensation={netCompensation} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunPowerBreakerDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetPowerPolicy("A", reserveEnergy: 10)
            .AddEntity(PowerNode(1, "r0", energy: 0, capacity: 100, generation: 0))
            .Build();
        var engine = new SimulationEngine(world);

        var trip = engine.Step(Array.Empty<Intent>());
        var tripped = trip.Events.OfType<PowerBlackout>().Any() &&
                      world.PowerGridStates[("r0", "A")].BreakerOpen;

        var blocked = engine.Step(new Intent[]
        {
            new DrawEnergyIntent(new EntityId(1), 5),
        });
        var drawBlocked = blocked.IntentResults[0].ErrorCode == SimulationErrorCode.NoEnergy &&
                          blocked.Events.OfType<PowerShortfall>()
                              .Any(item => item.Source == "blackout");

        world.Regions["r0"].AddEnergy("A", 5);
        engine.Step(Array.Empty<Intent>());
        var heldBelowReserve = world.PowerGridStates[("r0", "A")].BreakerOpen;

        world.Regions["r0"].AddEnergy("A", 10);
        var restored = engine.Step(Array.Empty<Intent>());
        var autoClosed = restored.Events.OfType<PowerRestored>().Any() &&
                         !world.PowerGridStates[("r0", "A")].BreakerOpen;

        var opened = engine.ExecutePowerCommand(new OpenBreakerCommand("A", "r0"));
        engine.Step(Array.Empty<Intent>());
        var manualHeld = opened.Success &&
                         world.PowerGridStates[("r0", "A")].BreakerOpen &&
                         world.PowerGridStates[("r0", "A")].ManualHold;

        var closed = engine.ExecutePowerCommand(new CloseBreakerCommand("A", "r0"));
        var manualClosed = closed.Success &&
                           !world.PowerGridStates[("r0", "A")].BreakerOpen;

        var ok = tripped &&
                 drawBlocked &&
                 heldBelowReserve &&
                 autoClosed &&
                 manualHeld &&
                 manualClosed;
        Console.WriteLine(
            $"POWER_BREAKER tripped={tripped} drawBlocked={drawBlocked} " +
            $"heldBelowReserve={heldBelowReserve} autoClosed={autoClosed} " +
            $"manualHeld={manualHeld} manualClosed={manualClosed} ok={ok}");

        return ok ? 0 : 1;
    }

    private static int RunPowerMaintenanceDemo()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 10)
            .SetPowerPolicy(
                "A",
                reserveEnergy: 0,
                breakerTripLimit: 2,
                breakerMaintenanceMaterialCost: 7)
            .AddEntity(PowerNode(1, "r0", energy: 0, capacity: 100, generation: 0))
            .Build();
        var engine = new SimulationEngine(world);

        var firstTrip = engine.Step(Array.Empty<Intent>());
        var state = world.PowerGridStates[("r0", "A")];
        var first = firstTrip.Events.OfType<PowerBlackout>().Any() &&
                    state.TripCount == 1 &&
                    !state.MaintenanceRequired;

        world.Regions["r0"].AddEnergy("A", 1);
        engine.Step(Array.Empty<Intent>());
        var reclosed = !state.BreakerOpen;

        engine.Step(new Intent[]
        {
            new DrawEnergyIntent(new EntityId(1), 1),
        });
        var secondTrip = engine.Step(Array.Empty<Intent>());
        var lockedOut = secondTrip.Events.OfType<BreakerLockedOut>().Any() &&
                        state.TripCount == 2 &&
                        state.MaintenanceRequired;

        var closeRejected = engine.ExecutePowerCommand(
            new CloseBreakerCommand("A", "r0"));
        var closeBlocked =
            closeRejected.ErrorCode == SimulationErrorCode.BreakerMaintenanceRequired;

        var maintained = engine.ExecutePowerCommand(
            new MaintainBreakerCommand("A", "r0"));
        var maintenanceOk = maintained.Success &&
                            world.Regions["r0"].GetMaterial("A") == 3 &&
                            world.Statistics.MaterialSpentTotal == 7 &&
                            state.TripCount == 0 &&
                            !state.MaintenanceRequired &&
                            state.BreakerOpen;

        world.Regions["r0"].AddEnergy("A", 5);
        var closed = engine.ExecutePowerCommand(
            new CloseBreakerCommand("A", "r0"));
        var closedOk = closed.Success && !state.BreakerOpen;

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));
        var hashOk = WorldStateHasher.Compute(world) == WorldStateHasher.Compute(loaded);

        var ok = first &&
                 reclosed &&
                 lockedOut &&
                 closeBlocked &&
                 maintenanceOk &&
                 closedOk &&
                 hashOk;
        Console.WriteLine(
            $"POWER_MAINTENANCE firstTrip={first} reclosed={reclosed} " +
            $"lockedOut={lockedOut} closeBlocked={closeBlocked} " +
            $"maintained={maintenanceOk} closed={closedOk} hashOk={hashOk} ok={ok}");

        return ok ? 0 : 1;
    }

    private static EntityDefinition PowerNode(
        long id,
        string regionId,
        long energy,
        long capacity,
        int generation,
        long driveCost = 0,
        int priority = 0) =>
        new(
            new EntityId(id),
            "A",
            regionId,
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: energy,
            EnergyCapacity: capacity,
            Material: 0,
            Drive: driveCost > 0 ? new DriveModule(100, driveCost) : null,
            Power: new PowerModule(generation, capacity, priority));

}
