using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

/// <summary>
/// M2 material collection: a drill mines the nearest deposit in range, spends
/// energy, produces material into the local regional stock and depletes the deposit.
/// </summary>
public sealed class MiningTests
{
    [Fact]
    public void DrillMinesTheNearestDepositIntoTheRegionalStock()
    {
        var world = World(
            depositId: 10,
            depositPosition: new Vec2(500, 500),
            entityPosition: new Vec2(400, 500));
        var engine = new SimulationEngine(world);

        var result = engine.Step(new Intent[] { new MineIntent(new EntityId(1)) });

        var mined = Assert.IsType<MaterialMined>(Assert.Single(result.Events));
        Assert.Equal(new EntityId(1), mined.EntityId);
        Assert.Equal(10, mined.DepositId);
        Assert.Equal(OreKind.Copper, mined.Kind);
        Assert.Equal(5, mined.Amount);
        Assert.Equal(45, mined.Remaining);
        Assert.Equal(5, world.Regions["r0"].GetMaterial("A"));
        Assert.True(result.IntentResults[0].Accepted);
    }

    [Fact]
    public void MiningDepletesTheDepositAndThenRejects()
    {
        var world = World(remaining: 5);
        var engine = new SimulationEngine(world);

        var first = engine.Step(new Intent[] { new MineIntent(new EntityId(1)) });
        var second = engine.Step(new Intent[] { new MineIntent(new EntityId(1)) });

        var firstMined = Assert.IsType<MaterialMined>(Assert.Single(first.Events));
        Assert.Equal(5, firstMined.Amount);
        Assert.True(second.IntentResults[0].Accepted is false);
        Assert.Equal(SimulationErrorCode.DepositDepleted, second.IntentResults[0].ErrorCode);
        Assert.Equal(5, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(0, world.Deposits.Values.Single().Remaining);
    }

    [Fact]
    public void OutOfRangeDepositIsRejected()
    {
        var world = World(entityPosition: new Vec2(2_000, 2_000));
        var engine = new SimulationEngine(world);

        var result = engine.Step(new Intent[] { new MineIntent(new EntityId(1)) });

        Assert.False(result.IntentResults[0].Accepted);
        Assert.Equal(SimulationErrorCode.DepositOutOfRange, result.IntentResults[0].ErrorCode);
        Assert.Equal(0, world.Regions["r0"].GetMaterial("A"));
    }

    [Fact]
    public void EntityWithoutADrillCannotMine()
    {
        var world = World(withDrill: false);
        var engine = new SimulationEngine(world);

        var result = engine.Step(new Intent[] { new MineIntent(new EntityId(1)) });

        Assert.False(result.IntentResults[0].Accepted);
        Assert.Equal(SimulationErrorCode.NoDrill, result.IntentResults[0].ErrorCode);
    }

    [Fact]
    public void MiningNeedsEnergyAndDoesNotProduceWithoutIt()
    {
        var world = World(energy: 0, drill: new DrillModule(5, 600, 2));
        var engine = new SimulationEngine(world);

        var result = engine.Step(new Intent[] { new MineIntent(new EntityId(1)) });

        Assert.False(result.IntentResults[0].Accepted);
        Assert.Equal(SimulationErrorCode.NoEnergy, result.IntentResults[0].ErrorCode);
        Assert.Equal(0, world.Regions["r0"].GetMaterial("A"));
        Assert.Equal(50, world.Deposits.Values.Single().Remaining);
    }

    [Fact]
    public void NearestTieIsBrokenByStableDepositId()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddDeposit(OreKind.Lead, "r0", new Vec2(600, 500), radiusMm: 100, remaining: 50, yieldPerTick: 5)
            .AddDeposit(OreKind.Copper, "r0", new Vec2(400, 500), radiusMm: 100, remaining: 50, yieldPerTick: 5)
            .AddEntity(Entity(new Vec2(500, 500), drill: new DrillModule(5, 600, 1)))
            .Build();
        var engine = new SimulationEngine(world);

        var result = engine.Step(new Intent[] { new MineIntent(new EntityId(1)) });

        var mined = Assert.IsType<MaterialMined>(Assert.Single(result.Events));
        Assert.Equal(1, mined.DepositId);
    }

    [Fact]
    public void SaveRoundTripKeepsDepositRemainingAndDrill()
    {
        var world = World(remaining: 6);
        var engine = new SimulationEngine(world);
        engine.Step(new Intent[] { new MineIntent(new EntityId(1)) });

        var json = WorldSaveSerializer.Save(world);
        var loaded = WorldSaveSerializer.Load(json);

        Assert.Equal(
            WorldStateHasher.Compute(world),
            WorldStateHasher.Compute(loaded));
        Assert.Equal(1, loaded.Deposits.Values.Single().Remaining);
        Assert.NotNull(loaded.FindEntity(new EntityId(1))!.Drill);
    }

    [Fact]
    public void MiningIsDeterministicAcrossRuns()
    {
        ulong Hash()
        {
            var world = World();
            var engine = new SimulationEngine(world);
            for (var tick = 0; tick < 3; tick++)
            {
                engine.Step(new Intent[] { new MineIntent(new EntityId(1)) });
            }

            return WorldStateHasher.Compute(world);
        }

        Assert.Equal(Hash(), Hash());
    }

    [Fact]
    public void DeviceObservationIncludesDepositsInsideDrillRange()
    {
        var world = World();
        var engine = new SimulationEngine(world);

        var observation = engine.BuildControllerObservation(new EntityId(1));

        var deposit = Assert.Single(observation.Deposits!);
        Assert.Equal(1, deposit.DepositId);
        Assert.Equal(OreKind.Copper, deposit.Kind);
        Assert.Equal(50, deposit.Remaining);
        Assert.True(Assert.Single(observation.Entities).HasDrill);
    }

    [Fact]
    public void DeviceObservationExcludesDepositsOutsideSightAndDrillRange()
    {
        var world = World(entityPosition: new Vec2(9_000, 9_000));
        var engine = new SimulationEngine(world);

        var observation = engine.BuildControllerObservation(new EntityId(1));

        Assert.Empty(observation.Deposits!);
    }

    [Fact]
    public void CivilizationObservationIncludesDepositsInOwnedRegions()
    {
        var world = World();
        var engine = new SimulationEngine(world);

        var observation = engine.BuildObservation("A");
        var other = engine.BuildObservation("B");

        Assert.Single(observation.Deposits!);
        Assert.Empty(other.Deposits!);
    }

    private static WorldState World(
        int depositId = 1,
        Vec2? depositPosition = null,
        Vec2? entityPosition = null,
        long remaining = 50,
        long energy = 100,
        DrillModule? drill = null,
        bool withDrill = true)
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddDeposit(
                OreKind.Copper,
                "r0",
                depositPosition ?? new Vec2(500, 500),
                radiusMm: 200,
                remaining: remaining,
                yieldPerTick: 5,
                depositId: depositId)
            .AddEntity(Entity(
                entityPosition ?? new Vec2(400, 500),
                energy,
                withDrill ? drill ?? new DrillModule(5, 600, 1) : null))
            .Build();
        return world;
    }

    private static EntityDefinition Entity(
        Vec2 position,
        long energy = 100,
        DrillModule? drill = null) =>
        new(
            new EntityId(1),
            "A",
            "r0",
            position,
            MaxHealth: 100,
            Energy: energy,
            EnergyCapacity: 100,
            Material: 0,
            Drill: drill);
}
