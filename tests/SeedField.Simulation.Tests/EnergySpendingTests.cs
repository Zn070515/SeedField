using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

public sealed class EnergySpendingTests
{
    [Fact]
    public void TwoExpensiveMovesInOneTickCannotBothSpendTheSameEnergy()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(SingleEnergyEntity(energy: 100, moveCost: 60, moveSpeed: 500))
            .Build();

        var engine = new SimulationEngine(world);
        var result = engine.Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(5_000, 500)),
            new MoveIntent(new EntityId(1), new Vec2(500, 5_000)),
        });

        var accepted = result.IntentResults.Where(item => item.Accepted).ToList();
        var rejected = result.IntentResults.Where(item => !item.Accepted).ToList();

        Assert.Single(accepted);
        var rejection = Assert.Single(rejected);
        Assert.Equal(SimulationErrorCode.NoEnergy, rejection.ErrorCode);

        var entity = world.FindEntity(new EntityId(1))!;
        Assert.Equal(40, entity.Energy);
        Assert.Equal(60, world.Statistics.EnergySpentTotal);
        Assert.Single(result.Events.OfType<EntityMoved>());
    }

    [Fact]
    public void MoveThenAttackInOneTickSpendsInStableInputOrder()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(SingleEnergyEntity(energy: 100, moveCost: 60, moveSpeed: 500))
            .AddEntity(new EntityDefinition(
                new EntityId(2),
                "B",
                "r0",
                new Vec2(500, 500),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 0,
                Material: 0,
                Weapon: new WeaponModule(10_000, Damage: 1, EnergyCost: 60, CooldownTicks: 0)))
            .Build();

        var engine = new SimulationEngine(world);
        var result = engine.Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(300, 500)),
            new AttackIntent(new EntityId(1), new EntityId(2)),
        });

        Assert.True(result.IntentResults[0].Accepted);
        Assert.False(result.IntentResults[1].Accepted);
        Assert.Equal(SimulationErrorCode.NoEnergy, result.IntentResults[1].ErrorCode);
        Assert.Equal(40, world.FindEntity(new EntityId(1))!.Energy);
    }

    [Fact]
    public void RejectedIntentDoesNotChangeTheWorld()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 1_000, 1_000)
            .AddEntity(SingleEnergyEntity(energy: 0, moveCost: 1, moveSpeed: 500))
            .Build();

        var engine = new SimulationEngine(world);

        var result = engine.Step(new Intent[]
        {
            new MoveIntent(new EntityId(1), new Vec2(500, 500)),
        });

        Assert.False(Assert.Single(result.IntentResults).Accepted);
        var entity = world.FindEntity(new EntityId(1))!;
        Assert.Equal(new Vec2(100, 100), entity.Position);
        Assert.Equal(0, entity.Energy);
        Assert.Equal(0, world.Statistics.EnergySpentTotal);
        Assert.DoesNotContain(result.Events, item => item is EntityMoved);
    }

    private static EntityDefinition SingleEnergyEntity(long energy, long moveCost, int moveSpeed) =>
        new(
            new EntityId(1),
            "A",
            "r0",
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: energy,
            EnergyCapacity: 100,
            Material: 0,
            Drive: new DriveModule(moveSpeed, moveCost),
            Weapon: new WeaponModule(10_000, Damage: 1, EnergyCost: moveCost, CooldownTicks: 0),
            Sensor: new SensorModule(10_000),
            Comm: new CommModule(1, 1));
}
