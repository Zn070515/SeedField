using SeedField.Simulation;

namespace SeedField.Runtime.Js.Tests;

public sealed class DeviceControllerSlotTests
{
    [Fact]
    public void DeviceControllerDiscardsIntentsForOtherEntities()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "A"))
            .Build();
        var engine = new SimulationEngine(world);
        using var device = new DeviceControllerSlot(
            new EntityId(1),
            "A",
            """
            function decide(obs) {
                return [
                    { type: "move", entityId: obs.viewerEntityId, target: { x: 500, y: 500 } },
                    { type: "move", entityId: 2, target: { x: 600, y: 600 } }
                ];
            }
            """);

        var decision = device.Decide(engine.BuildDeviceObservation(new EntityId(1)));

        Assert.Equal(1, Assert.Single(decision.Intents).EntityId.Value);
        Assert.Contains(
            decision.Faults,
            fault => fault.Code == ScriptFaultCodes.DeviceScope);
    }

    [Fact]
    public void DeviceControllerRejectsObservationScopedToAnotherEntity()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(Unit(1, "A"))
            .AddEntity(Unit(2, "A"))
            .Build();
        var engine = new SimulationEngine(world);
        using var device = new DeviceControllerSlot(
            new EntityId(1),
            "A",
            "function decide(obs) { return [{ type: \"wait\", entityId: 1 }]; }");

        var decision = device.Decide(engine.BuildDeviceObservation(new EntityId(2)));

        Assert.Empty(decision.Intents);
        Assert.Contains(
            decision.Faults,
            fault => fault.Code == ScriptFaultCodes.DeviceScope);
    }

    private static EntityDefinition Unit(long id, string civilizationId) =>
        new(
            new EntityId(id),
            civilizationId,
            "r0",
            new Vec2(100, 100),
            MaxHealth: 10,
            Energy: 10,
            EnergyCapacity: 10,
            Material: 0);
}
