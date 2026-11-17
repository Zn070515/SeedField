using System.Diagnostics;
using Godot;
using SeedField.Runtime.Js;
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

    private bool _benchmarkMixed;
    private bool _benchmarkMixedQuiet;
    private bool _benchmarkMixedCapture;
    private int _benchmarkMixedDevices;
    private int _benchmarkMixedMaxFps;
    private double _benchmarkMixedSeconds = 10;
    private double _benchmarkMixedElapsed;
    private long _benchmarkMixedAllocStart;
    private int _benchmarkMixedGc0Start;
    private int _benchmarkMixedGc1Start;
    private int _benchmarkMixedGc2Start;
    private TimeSpan _benchmarkMixedPauseStart;
    private readonly List<double> _benchmarkFrameMs = new();
    private readonly List<double> _benchmarkTickMs = new();
    private readonly List<double> _benchmarkObsMs = new();
    private readonly List<double> _benchmarkDecideMs = new();
    private readonly List<double> _benchmarkStepMs = new();
    private readonly List<double> _benchmarkPresentMs = new();
    private readonly Dictionary<string, int> _benchmarkMixedFaultCodes = new();
    private long _benchmarkAllocObs;
    private long _benchmarkAllocDecide;
    private long _benchmarkAllocStep;
    private long _benchmarkAllocPresent;
    private readonly Stopwatch _benchmarkMixedWall = new();
    private long _benchmarkMixedLastTimestamp;
    private const double MixedWarmupSeconds = 1.5;

    private void ParseMixedBenchmark(string[] arguments)
    {
        var mixedArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--benchmark-mixed=", StringComparison.Ordinal));
        if (mixedArgument is null ||
            !int.TryParse(mixedArgument["--benchmark-mixed=".Length..], out var devices))
        {
            return;
        }

        _benchmarkMixed = true;
        _benchmarkMixedQuiet = arguments.Contains("--benchmark-mixed-quiet");
        _benchmarkMixedCapture = arguments.Contains("--benchmark-mixed-capture");
        _benchmarkMixedDevices = Mathf.Clamp(devices, 1, 1_000);
        var maxFpsArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--benchmark-mixed-max-fps=", StringComparison.Ordinal));
        if (maxFpsArgument is not null &&
            int.TryParse(
                maxFpsArgument["--benchmark-mixed-max-fps=".Length..],
                out var maxFps))
        {
            _benchmarkMixedMaxFps = Mathf.Clamp(maxFps, 0, 240);
        }
        var secondsArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--benchmark-mixed-seconds=", StringComparison.Ordinal));
        if (secondsArgument is not null &&
            int.TryParse(
                secondsArgument["--benchmark-mixed-seconds=".Length..],
                out var seconds))
        {
            _benchmarkMixedSeconds = Mathf.Clamp(seconds, 1, 60);
        }

        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
    }

    private void ConfigureMixedBenchmark()
    {
        ResetScenario(peaceful: false, preRun: true);
        if (_benchmarkMixedQuiet)
        {
            _slotA?.Dispose();
            _slotA = new ControllerSlot("A", Scenarios.BenchmarkQuietScript);
        }

        DeployBenchmarkDevices(_benchmarkMixedDevices, "bench-mixed");
        _paused = false;
        _omniscientDebug = true;
        _benchmarkMixedElapsed = 0;
        _benchmarkFrameMs.Clear();
        _benchmarkTickMs.Clear();
        _benchmarkObsMs.Clear();
        _benchmarkDecideMs.Clear();
        _benchmarkStepMs.Clear();
        _benchmarkPresentMs.Clear();
        _benchmarkMixedFaultCodes.Clear();
        _benchmarkAllocObs = 0;
        _benchmarkAllocDecide = 0;
        _benchmarkAllocStep = 0;
        _benchmarkAllocPresent = 0;
        _benchmarkMixedWall.Restart();
        _benchmarkMixedLastTimestamp = Stopwatch.GetTimestamp();
        Engine.MaxFps = _benchmarkMixedMaxFps;
        _benchmarkMixedAllocStart = GC.GetTotalAllocatedBytes(precise: true);
        _benchmarkMixedGc0Start = GC.CollectionCount(0);
        _benchmarkMixedGc1Start = GC.CollectionCount(1);
        _benchmarkMixedGc2Start = GC.CollectionCount(2);
        _benchmarkMixedPauseStart = GC.GetTotalPauseDuration();
        _status = "mixed benchmark · running";
        RefreshVisibleEntities();
    }

    private void ConfigureJintBenchmark()
    {
        ResetScenario(peaceful: true, preRun: false);
        DeployBenchmarkDevices(_benchmarkJintDevices, "bench-jint");
        _paused = true;
        _status = "jint benchmark · paused";
        UpdateFacing();
    }

    private void DeployBenchmarkDevices(int count, string blueprintId)
    {
        var blueprint = _world.Workshop.Publish(new DeviceBlueprint(
            blueprintId,
            0,
            "Bench Scout",
            120,
            5_000,
            Sensor: new SensorModule(5_000),
            Drive: new DriveModule(120, EnergyCostPerMove: 1),
            Comm: new CommModule(1, 1),
            Power: new PowerModule(EnergyPerTick: 2, StorageCapacity: 5_000),
            State: new StateModule(ComputePerTick: 2, StorageCapacity: 64),
            ControllerProgramId: "program.scout.v1",
            MaterialCost: 0));
        _world.Regions["frontier"].AddMaterial("A", count * 10L);
        var columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        for (var index = 0; index < count; index++)
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
                    1_500 + row * (5_000 / Mathf.Max(1, count / columns))));
        }

        RebuildDeviceControllers();
    }

    private void TrackMixedBenchmark()
    {
        if (!_benchmarkMixed)
        {
            return;
        }

        // Godot's delta can be clamped when a frame stalls, which makes a
        // controller-heavy run look shorter than it was. Measure real wall time.
        var timestamp = Stopwatch.GetTimestamp();
        var frameSeconds = (timestamp - _benchmarkMixedLastTimestamp) /
                           (double)Stopwatch.Frequency;
        _benchmarkMixedLastTimestamp = timestamp;
        _benchmarkMixedElapsed = _benchmarkMixedWall.Elapsed.TotalSeconds;
        if (_benchmarkMixedElapsed >= MixedWarmupSeconds)
        {
            _benchmarkFrameMs.Add(frameSeconds * 1000.0);
        }

        if (_benchmarkMixedElapsed >= MixedWarmupSeconds + _benchmarkMixedSeconds)
        {
            FinishMixedBenchmark();
        }
    }

    private void FinishMixedBenchmark()
    {
        _benchmarkMixed = false;
        if (_benchmarkFrameMs.Count == 0)
        {
            GD.Print("MIXED_BENCH no samples");
            GetTree().Quit();
            return;
        }

        var measuredSeconds = Math.Max(0.001, _benchmarkMixedElapsed - MixedWarmupSeconds);
        _benchmarkFrameMs.Sort();
        _benchmarkTickMs.Sort();
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - _benchmarkMixedAllocStart;
        var gc0 = GC.CollectionCount(0) - _benchmarkMixedGc0Start;
        var gc1 = GC.CollectionCount(1) - _benchmarkMixedGc1Start;
        var gc2 = GC.CollectionCount(2) - _benchmarkMixedGc2Start;
        var pauseMs = (GC.GetTotalPauseDuration() - _benchmarkMixedPauseStart).TotalMilliseconds;
        var avgFrameMs = _benchmarkFrameMs.Average();
        var avgTickMs = _benchmarkTickMs.Count > 0 ? _benchmarkTickMs.Average() : 0;
        var peakMb = System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64 /
                     (1024.0 * 1024.0);
        var faultCodes = _benchmarkMixedFaultCodes.Count == 0
            ? "none"
            : string.Join(
                ",",
                _benchmarkMixedFaultCodes
                    .OrderByDescending(pair => pair.Value)
                    .Take(5)
                    .Select(pair => $"{pair.Key}:{pair.Value}"));
        GD.Print(
            $"MIXED_BENCH devices={_benchmarkMixedDevices} " +
            $"quiet={_benchmarkMixedQuiet} maxFps={_benchmarkMixedMaxFps} " +
            $"entities={_world.EnumerateEntities().Count()} " +
            $"sprites={_visibleEntities.Count} frames={_benchmarkFrameMs.Count} " +
            $"ticks={_benchmarkTickMs.Count} seconds={measuredSeconds:0.0} " +
            $"fps={_benchmarkFrameMs.Count / measuredSeconds:0.0} " +
            $"ticksPerSec={_benchmarkTickMs.Count / measuredSeconds:0.0} " +
            $"avgFrameMs={avgFrameMs:0.00} " +
            $"p50FrameMs={MixedPercentile(_benchmarkFrameMs, 0.50):0.00} " +
            $"p95FrameMs={MixedPercentile(_benchmarkFrameMs, 0.95):0.00} " +
            $"p99FrameMs={MixedPercentile(_benchmarkFrameMs, 0.99):0.00} " +
            $"avgTickMs={avgTickMs:0.00} " +
            $"p95TickMs={MixedPercentile(_benchmarkTickMs, 0.95):0.00} " +
            $"obsMs={Average(_benchmarkObsMs):0.00} " +
            $"decideMs={Average(_benchmarkDecideMs):0.00} " +
            $"stepMs={Average(_benchmarkStepMs):0.00} " +
            $"presentMs={Average(_benchmarkPresentMs):0.00} " +
            $"allocObsMB={_benchmarkAllocObs / (1024.0 * 1024.0) / Math.Max(1, _benchmarkTickMs.Count):0.00} " +
            $"allocDecideMB={_benchmarkAllocDecide / (1024.0 * 1024.0) / Math.Max(1, _benchmarkTickMs.Count):0.00} " +
            $"allocStepMB={_benchmarkAllocStep / (1024.0 * 1024.0) / Math.Max(1, _benchmarkTickMs.Count):0.00} " +
            $"allocPresentMB={_benchmarkAllocPresent / (1024.0 * 1024.0) / Math.Max(1, _benchmarkTickMs.Count):0.00} " +
            $"allocMBPerFrame={allocated / (1024.0 * 1024.0) / _benchmarkFrameMs.Count:0.00} " +
            $"allocMBPerTick={allocated / (1024.0 * 1024.0) / Math.Max(1, _benchmarkTickMs.Count):0.00} " +
            $"gc0={gc0} gc1={gc1} gc2={gc2} gcPauseMs={pauseMs:0.0} " +
            $"drawCalls={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} " +
            $"faults={_faultCount} faultCodes={faultCodes} " +
            $"peakWorkingSetMb={peakMb:0.0} " +
            $"gcServer={System.Runtime.GCSettings.IsServerGC}");
        if (_benchmarkMixedCapture)
        {
            RenderingServer.ForceDraw();
            var image = GetViewport().GetTexture().GetImage();
            image.SavePng("res://screenshots/mixed-lod.png");
            GD.Print("MIXED_BENCH_CAPTURE res://screenshots/mixed-lod.png");
        }

        GetTree().Quit();
    }

    private static double Average(List<double> samples) =>
        samples.Count == 0 ? 0 : samples.Average();

    private static double MixedPercentile(List<double> sorted, double percentile)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
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
