using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main
{
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

        var fps = _benchmarkSamples == 0 ? 0 : _benchmarkFpsSum / _benchmarkSamples;
        var processMs = _benchmarkSamples == 0
            ? 0
            : _benchmarkProcessSum / _benchmarkSamples;
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
        _pan = new Vector2(640, 360) - (BaseMapOrigin + size / 2f);
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
