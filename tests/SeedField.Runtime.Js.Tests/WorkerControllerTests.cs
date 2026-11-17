using SeedField.Runtime.Js;
using SeedField.Runtime.Js.Worker;
using SeedField.Simulation;

namespace SeedField.Runtime.Js.Tests;

public sealed class WorkerControllerTests
{
    [Fact]
    public void WorkerReturnsParsedIntents()
    {
        using var worker = NewWorker(
            "function decide(obs){ return [{ type: 'wait', entityId: obs.viewerEntityId }]; }");

        var decision = worker.Decide(ObservationForEntity1());

        Assert.Empty(decision.Faults);
        var intent = Assert.IsType<WaitIntent>(Assert.Single(decision.Intents));
        Assert.Equal(new EntityId(1), intent.EntityId);
    }

    [Fact]
    public void WorkerIsDeterministicForTheSameObservation()
    {
        using var worker = NewWorker(
            "function decide(obs){ return [{ type: 'wait', entityId: obs.viewerEntityId }]; }");
        var observation = ObservationForEntity1();

        var first = worker.Decide(observation);
        var second = worker.Decide(observation);

        Assert.Single(first.Intents);
        Assert.Single(second.Intents);
        Assert.Equal(first.Intents[0].GetType(), second.Intents[0].GetType());
        Assert.Equal(first.Intents[0].EntityId, second.Intents[0].EntityId);
    }

    [Fact]
    public void InfiniteLoopReturnsALimitFaultWithoutHangingTheHost()
    {
        using var worker = NewWorker(
            "function decide(){ while (true) {} }",
            new WorkerControllerOptions
            {
                DecisionTimeoutMs = 3_000,
                ControllerOptions = new JsControllerOptions
                {
                    Timeout = TimeSpan.FromMilliseconds(100),
                },
            });

        var decision = worker.Decide(ObservationForEntity1());

        Assert.Contains(
            decision.Faults,
            fault => fault.Code is ScriptFaultCodes.Timeout
                or ScriptFaultCodes.StatementLimit);
    }

    [Fact]
    public void InvalidScriptFailsClosedAtStartup()
    {
        var exception = Assert.Throws<WorkerIsolationException>(() =>
            new WorkerController(
                "A",
                "this is not valid javascript",
                new WorkerControllerOptions()));

        Assert.Equal("WORKER_LOAD_FAILED", exception.Code);
    }

    [Fact]
    public void DisposeStopsTheWorkerProcess()
    {
        var worker = NewWorker(
            "function decide(){ return []; }");
        var processId = worker.ProcessId;
        Assert.NotNull(processId);

        worker.Dispose();

        Assert.True(worker.HasExited);
    }

    [Fact]
    public void WindowsWorkerIsAssignedToAJobObject()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var worker = NewWorker(
            "function decide(){ return []; }",
            new WorkerControllerOptions { RequireJobObject = true });

        Assert.True(worker.JobObjectAssigned, worker.LastWorkerError);
    }

    private static WorkerController NewWorker(
        string script,
        WorkerControllerOptions? options = null) =>
        new(
            "A",
            script,
            options ?? new WorkerControllerOptions
            {
                DecisionTimeoutMs = 2_000,
                ControllerOptions = new JsControllerOptions
                {
                    Timeout = TimeSpan.FromMilliseconds(200),
                },
            });

    private static Observation ObservationForEntity1()
    {
        var world = new WorldBuilder()
            .AddRegion("r0", 1_000, 1_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 0,
                EnergyCapacity: 10,
                Material: 0))
            .Build();
        return new SimulationEngine(world).BuildDeviceObservation(new EntityId(1));
    }
}
