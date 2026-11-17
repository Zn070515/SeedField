using SeedField.Runtime.Js;
using SeedField.Simulation;

namespace SeedField.Runtime.Js.Tests;

public sealed class ControllerFactoryTests
{
    [Fact]
    public void ControllerSlotUsesTheFactoryForInitialLoadAndDeploy()
    {
        var created = 0;
        var slot = new ControllerSlot(
            "A",
            "function decide(){return [];}",
            factory: (civilizationId, _, _) =>
            {
                created++;
                return new FakeController(civilizationId);
            });

        slot.Decide(EmptyObservation());
        var deployed = slot.Deploy("function decide(){return [];}");

        Assert.True(deployed.Success);
        Assert.Equal(2, created);
        slot.Decide(EmptyObservation());
        slot.Dispose();
    }

    [Fact]
    public void DeviceControllerSlotPassesTheFactoryThrough()
    {
        var created = 0;
        using var slot = new DeviceControllerSlot(
            new EntityId(1),
            "B",
            "function decide(){return [];}",
            factory: (civilizationId, _, _) =>
            {
                created++;
                return new FakeController(civilizationId);
            });

        slot.Decide(EmptyObservation(viewerEntityId: 1));

        Assert.Equal(1, created);
    }

    private static Observation EmptyObservation(long? viewerEntityId = null) =>
        new(
            Tick: 0,
            CivilizationId: "A",
            Entities: Array.Empty<ObservedEntity>(),
            Messages: Array.Empty<ObservedMessage>(),
            Shipments: Array.Empty<ObservedShipment>(),
            EnergyShipments: Array.Empty<ObservedEnergyShipment>(),
            ViewerEntityId: viewerEntityId);

    private sealed class FakeController : IController
    {
        public FakeController(string civilizationId) => CivilizationId = civilizationId;

        public string CivilizationId { get; }

        public ControllerDecision Decide(Observation observation) => new([], []);

        public void Dispose()
        {
        }
    }
}
