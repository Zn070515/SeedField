using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class PowerMaintenanceTests
{
    [Fact]
    public void HighPriorityIntentPreemptsLowerPriorityWithinTheSameTick()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(PowerNode(1, priority: 0, driveCost: 5))
            .AddEntity(PowerNode(2, priority: 10, driveCost: 5))
            .Build();
        world.Regions["r0"].AddEnergy("A", 5);

        var result = new SimulationEngine(world).Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
            new MoveIntent(new EntityId(2), new Vec2(500, 500)),
        });

        // Processing is preemptive, but results keep the caller's input order.
        Assert.Equal(new EntityId(1), result.IntentResults[0].Intent.EntityId);
        Assert.Equal(SimulationErrorCode.NoEnergy, result.IntentResults[0].ErrorCode);
        Assert.Equal(new EntityId(2), result.IntentResults[1].Intent.EntityId);
        Assert.True(result.IntentResults[1].Accepted);

        Assert.NotEqual(new Vec2(100, 100), world.FindEntity(new EntityId(2))!.Position);
        Assert.Equal(new Vec2(100, 100), world.FindEntity(new EntityId(1))!.Position);
        Assert.Equal(0, world.Regions["r0"].GetEnergy("A"));
    }

    [Fact]
    public void BreakerLocksOutAfterTripLimitAndRequiresMaintenance()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 20)
            .SetPowerPolicy(
                "A",
                reserveEnergy: 0,
                breakerTripLimit: 2,
                breakerMaintenanceMaterialCost: 7)
            .AddEntity(PowerNode(1))
            .Build();
        var engine = new SimulationEngine(world);

        var firstTrip = engine.Step(Array.Empty<Intent>());
        var state = world.PowerGridStates[("r0", "A")];
        Assert.Contains(firstTrip.Events, item => item is PowerBlackout);
        Assert.Equal(1, state.TripCount);
        Assert.False(state.MaintenanceRequired);

        world.Regions["r0"].AddEnergy("A", 1);
        engine.Step(Array.Empty<Intent>());
        Assert.False(state.BreakerOpen);

        engine.Step(new Intent[]
        {
            new DrawEnergyIntent(new EntityId(1), 1),
        });
        var secondTrip = engine.Step(Array.Empty<Intent>());

        Assert.Equal(2, state.TripCount);
        Assert.True(state.MaintenanceRequired);
        Assert.True(state.BreakerOpen);
        Assert.Contains(secondTrip.Events, item => item is BreakerLockedOut);

        var closeRejected = engine.ExecutePowerCommand(
            new CloseBreakerCommand("A", "r0"));
        Assert.False(closeRejected.Success);
        Assert.Equal(
            SimulationErrorCode.BreakerMaintenanceRequired,
            closeRejected.ErrorCode);

        var maintained = engine.ExecutePowerCommand(
            new MaintainBreakerCommand("A", "r0"));
        Assert.True(maintained.Success);
        Assert.Contains(maintained.Events, item => item is BreakerMaintained);
        Assert.Equal(13, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(7, world.Statistics.MaterialSpentTotal);
        Assert.Equal(0, state.TripCount);
        Assert.False(state.MaintenanceRequired);
        Assert.True(state.BreakerOpen);
        Assert.False(state.ManualHold);

        world.Regions["r0"].AddEnergy("A", 5);
        var closed = engine.ExecutePowerCommand(
            new CloseBreakerCommand("A", "r0"));
        Assert.True(closed.Success);
        Assert.False(state.BreakerOpen);
    }

    [Fact]
    public void MaintenanceWithoutMaterialKeepsTheLockout()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 3)
            .SetPowerPolicy(
                "A",
                reserveEnergy: 0,
                breakerTripLimit: 1,
                breakerMaintenanceMaterialCost: 50)
            .AddEntity(PowerNode(1))
            .Build();
        var engine = new SimulationEngine(world);
        engine.Step(Array.Empty<Intent>());

        var state = world.PowerGridStates[("r0", "A")];
        Assert.True(state.MaintenanceRequired);

        var maintained = engine.ExecutePowerCommand(
            new MaintainBreakerCommand("A", "r0"));
        Assert.False(maintained.Success);
        Assert.Equal(SimulationErrorCode.InsufficientMaterial, maintained.ErrorCode);
        Assert.Equal(3, world.Regions["r0"].GetMaterial("A"));
        Assert.True(state.MaintenanceRequired);
        Assert.Equal(1, state.TripCount);
    }

    [Fact]
    public void BreakerWearAndMaintenancePolicySurviveSaveLoad()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddStock("r0", "A", 50)
            .SetPowerPolicy(
                "A",
                reserveEnergy: 0,
                breakerTripLimit: 1,
                breakerMaintenanceMaterialCost: 5)
            .AddEntity(PowerNode(1))
            .Build();
        new SimulationEngine(world).Step(Array.Empty<Intent>());

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.Save(world));

        Assert.Equal(WorldStateHasher.Compute(world), WorldStateHasher.Compute(loaded));
        Assert.Equal(1, loaded.PowerPolicies["A"].BreakerTripLimit);
        Assert.Equal(5, loaded.PowerPolicies["A"].BreakerMaintenanceMaterialCost);
        var state = loaded.PowerGridStates[("r0", "A")];
        Assert.Equal(1, state.TripCount);
        Assert.True(state.MaintenanceRequired);
    }

    [Fact]
    public void V11SaveMigratesToNoBreakerLockout()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .SetPowerPolicy("A", reserveEnergy: 10)
            .Build();
        var data = WorldSaveSerializer.ToData(world) with
        {
            SchemaVersion = 11,
            PowerPolicies = new[] { new PowerPolicySaveData("A", 10) },
            PowerGridStates = new[]
            {
                new PowerGridStateSaveData("r0", "A", BreakerOpen: true, ManualHold: false),
            },
        };

        var loaded = WorldSaveSerializer.Load(WorldSaveSerializer.SaveData(data));

        Assert.Equal(0, loaded.PowerPolicies["A"].BreakerTripLimit);
        Assert.Equal(0, loaded.PowerPolicies["A"].BreakerMaintenanceMaterialCost);
        var state = loaded.PowerGridStates[("r0", "A")];
        Assert.Equal(0, state.TripCount);
        Assert.False(state.MaintenanceRequired);
    }

    private static EntityDefinition PowerNode(
        long id,
        int priority = 0,
        long driveCost = 0) =>
        new(
            new EntityId(id),
            "A",
            "r0",
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: 0,
            EnergyCapacity: 100,
            Material: 0,
            Drive: driveCost > 0 ? new DriveModule(100, driveCost) : null,
            Power: new PowerModule(0, 100, priority));
}
