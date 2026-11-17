using System.Diagnostics;
using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main
{
    private bool _benchmarkMain;
    private int _benchmarkDevices;
    private double _benchmarkElapsed;
    private double _benchmarkSampleElapsed;
    private double _benchmarkFpsSum;
    private double _benchmarkProcessSum;
    private int _benchmarkSamples;
    private bool _benchmarkJint;
    private int _benchmarkJintDevices;
    private int _benchmarkJintTicks;
    private double _benchmarkJintMs;
    private int _benchmarkJintFaults;

    private void ConfigureJintBenchmark()
    {
        ResetScenario(peaceful: true, preRun: false);
        var blueprint = _world.Workshop.Publish(new DeviceBlueprint(
            "bench-jint",
            0,
            "Bench Jint Scout",
            120,
            5_000,
            Sensor: new SensorModule(5_000),
            Drive: new DriveModule(120, EnergyCostPerMove: 1),
            Comm: new CommModule(1, 1),
            Power: new PowerModule(EnergyPerTick: 2, StorageCapacity: 5_000),
            State: new StateModule(ComputePerTick: 2, StorageCapacity: 64),
            ControllerProgramId: "program.scout.v1",
            MaterialCost: 0));
        _world.Regions["frontier"].AddMaterial("A", _benchmarkJintDevices * 10L);
        var columns = Mathf.CeilToInt(Mathf.Sqrt(_benchmarkJintDevices));
        for (var index = 0; index < _benchmarkJintDevices; index++)
        {
            var column = index % columns;
            var row = index / columns;
            DeploymentService.Deploy(
                _world,
                blueprint,
                "A",
                "frontier",
                new Vec2(
                    2_000 + column * (12_000 / Mathf.Max(1, columns - 1)),
                    1_500 + row * (5_000 / Mathf.Max(1, _benchmarkJintDevices / columns))));
        }

        RebuildDeviceControllers();
        _paused = true;
        _status = "jint benchmark · paused";
        UpdateFacing();
    }

    private void RunJintBenchmarkTick()
    {
        var stopwatch = Stopwatch.StartNew();
        StepOnce();
        stopwatch.Stop();
        _benchmarkJintMs += stopwatch.Elapsed.TotalMilliseconds;
        _benchmarkJintFaults = _faultCount;
        _benchmarkJintTicks++;
        if (_benchmarkJintTicks < 30)
        {
            return;
        }

        var average = _benchmarkJintMs / _benchmarkJintTicks;
        GD.Print(
            $"MAIN_JINT_BENCH devices={_benchmarkJintDevices} " +
            $"entities={_world.EnumerateEntities().Count()} ticks={_benchmarkJintTicks} " +
            $"avgTickMs={average:0.00} ticksPerSec={1000.0 / average:0.0} " +
            $"faults={_benchmarkJintFaults} processMs=" +
            $"{Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0:0.00} " +
            $"drawCalls={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} " +
            $"vsync=off");
        GetTree().Quit();
    }

    private void UpdateMainBenchmark(double delta)
    {
        _benchmarkElapsed += delta;
        if (_benchmarkElapsed < 1.5)
        {
            return;
        }

        _benchmarkSampleElapsed += delta;
        _benchmarkFpsSum += Engine.GetFramesPerSecond();
        _benchmarkProcessSum +=
            Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0;
        _benchmarkSamples++;
        if (_benchmarkSampleElapsed < 5.0)
        {
            return;
        }

        var fps = _benchmarkFpsSum / _benchmarkSamples;
        var processMs = _benchmarkProcessSum / _benchmarkSamples;
        GD.Print(
            $"MAIN_BENCH devices={_benchmarkDevices} sprites={_visibleEntities.Count} " +
            $"fps={fps:0.0} processMs={processMs:0.00} " +
            $"drawCalls={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} " +
            $"videoMem={Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed)} " +
            $"viewport=1280x720 vsync=off");
        GetTree().Quit();
    }

    private void CenterView()
    {
        var size = new Vector2(
            Scenarios.WidthMm * MapScale,
            Scenarios.HeightMm * MapScale);
        _pan = new Vector2(ViewWidth / 2f, ViewHeight / 2f) - (BaseMapOrigin + size / 2f);
    }

    private void ZoomAt(Vector2 screen, float factor)
    {
        var world = ScreenToWorld(screen);
        _zoom = Mathf.Clamp(_zoom * factor, MinZoom, MaxZoom);
        _pan = screen - (BaseMapOrigin +
            new Vector2(world.X * MapScale, world.Y * MapScale));
        QueueRedraw();
    }

    private Vec2 ScreenToWorld(Vector2 screen) => new(
        (int)Mathf.Round((screen.X - MapOrigin.X) / MapScale),
        (int)Mathf.Round((screen.Y - MapOrigin.Y) / MapScale));

    private string DetailTier => _zoom >= 1.15f
        ? "near"
        : _zoom <= 0.82f
            ? "far"
            : "mid";
}
