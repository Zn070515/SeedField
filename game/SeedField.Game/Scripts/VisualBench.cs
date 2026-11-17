using System.Text.Json;
using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

/// <summary>
/// Presentation-only sprite benchmark. It never builds a WorldState; it measures
/// how many CanvasTexture sprites the renderer can hold at 1280x720.
/// </summary>
public partial class VisualBench : Node2D
{
    private const int ViewWidth = 1280;
    private const int ViewHeight = 720;
    private const float BenchScale = 0.15f;

    private readonly List<Sprite2D> _sprites = new();
    private int _devices = 10;
    private bool _masks = true;
    private float _seconds = 5f;
    private string _shot = string.Empty;
    private bool _capture;
    private double _elapsed;
    private double _sampleElapsed;
    private double _fpsSum;
    private double _processSum;
    private int _samples;
    private PointLight2D? _light;
    private bool _finished;

    public override void _Ready()
    {
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        ParseArgs();
        BuildSprites();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_finished)
        {
            return;
        }

        _elapsed += delta;
        if (_light is not null)
        {
            _light.Position = new Vector2(
                ViewWidth * 0.5f + Mathf.Sin((float)_elapsed * 1.2f) * 420f,
                ViewHeight * 0.5f + Mathf.Cos((float)_elapsed * 0.9f) * 220f);
        }

        if (_elapsed < 1.5)
        {
            return;
        }

        _sampleElapsed += delta;
        _fpsSum += Engine.GetFramesPerSecond();
        _processSum += Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0;
        _samples++;

        if (_sampleElapsed < _seconds)
        {
            return;
        }

        _finished = true;
        var fps = _samples == 0 ? 0 : _fpsSum / _samples;
        var processMs = _samples == 0 ? 0 : _processSum / _samples;
        var payload = JsonSerializer.Serialize(new
        {
            devices = _devices,
            masks = _masks,
            seconds = Math.Round(_sampleElapsed, 2),
            samples = _samples,
            fps = Math.Round(fps, 2),
            processMs = Math.Round(processMs, 3),
            drawCalls = Performance.GetMonitor(
                Performance.Monitor.RenderTotalDrawCallsInFrame),
            videoMemBytes = Performance.GetMonitor(
                Performance.Monitor.RenderVideoMemUsed),
            renderer = "godot-4.7.2",
            route = "oblique",
        });
        GD.Print($"VISUAL_BENCH {payload}");
        WriteLog(payload);

        if (_capture)
        {
            Capture();
        }

        GetTree().Quit();
    }

    private void ParseArgs()
    {
        foreach (var argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith("--devices=", StringComparison.Ordinal))
            {
                int.TryParse(argument["--devices=".Length..], out _devices);
            }
            else if (argument.StartsWith("--masks=", StringComparison.Ordinal))
            {
                _masks = argument["--masks=".Length..] != "off";
            }
            else if (argument.StartsWith("--seconds=", StringComparison.Ordinal))
            {
                float.TryParse(argument["--seconds=".Length..], out _seconds);
            }
            else if (argument.StartsWith("--shot=", StringComparison.Ordinal))
            {
                _shot = argument["--shot=".Length..];
                _capture = true;
            }
        }

        _devices = Mathf.Clamp(_devices, 1, 100);
        _seconds = Mathf.Clamp(_seconds, 2f, 30f);
    }

    private void BuildSprites()
    {
        var catalog = VisualAssetCatalog.Load();
        var assets = new[] { "worker_frame_sensor", "worker_frame_drill" };
        var columns = _devices <= 4
            ? _devices
            : Mathf.CeilToInt(Mathf.Sqrt(_devices * 1.6f));
        var rows = Mathf.CeilToInt(_devices / (float)columns);
        var cellX = ViewWidth / (float)columns;
        var cellY = (ViewHeight - 100f) / Mathf.Max(1, rows);

        if (_masks)
        {
            var modulate = new CanvasModulate { Color = new Color(0.42f, 0.48f, 0.60f) };
            AddChild(modulate);
            _light = new PointLight2D
            {
                Texture = BuildLightTexture(),
                Energy = 2.2f,
                TextureScale = 2.2f,
                Position = new Vector2(ViewWidth * 0.5f, ViewHeight * 0.5f),
            };
            AddChild(_light);
        }

        for (var index = 0; index < _devices; index++)
        {
            var name = assets[index % assets.Length];
            var frame = catalog.Get(name);
            if (frame is null)
            {
                continue;
            }

            var texture = frame.Diffuse;
            if (_masks && frame.Normal is not null)
            {
                texture = new CanvasTexture
                {
                    DiffuseTexture = frame.Diffuse,
                    NormalTexture = frame.Normal,
                };
            }

            var sprite = new Sprite2D
            {
                Texture = texture,
                Centered = true,
                Scale = new Vector2(BenchScale, BenchScale),
            };
            var column = index % columns;
            var row = index / columns;
            sprite.Position = new Vector2(
                cellX * (column + 0.5f),
                70f + cellY * (row + 0.5f));
            AddChild(sprite);
            _sprites.Add(sprite);

            if (_masks && frame.Emissive is not null)
            {
                var emissive = new Sprite2D
                {
                    Texture = frame.Emissive,
                    Centered = true,
                    Position = sprite.Position,
                    Scale = new Vector2(BenchScale, BenchScale),
                    Material = new CanvasItemMaterial
                    {
                        BlendMode = CanvasItemMaterial.BlendModeEnum.Add,
                    },
                };
                AddChild(emissive);
            }
        }
    }

    private static Texture2D BuildLightTexture()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 0.92f, 0.78f, 1f));
        gradient.SetColor(1, new Color(1f, 0.92f, 0.78f, 0f));
        return new GradientTexture2D
        {
            Gradient = gradient,
            Width = 256,
            Height = 256,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
        };
    }

    private void WriteLog(string payload)
    {
        var project = ProjectSettings.GlobalizePath("res://");
        var repo = Path.GetFullPath(Path.Combine(project, "..", ".."));
        var logs = Path.Combine(repo, "logs");
        Directory.CreateDirectory(logs);
        var masks = _masks ? "on" : "off";
        File.WriteAllText(
            Path.Combine(logs, $"visual-bench-{_devices}-{masks}.json"),
            payload + "\n");
    }

    private void Capture()
    {
        RenderingServer.ForceDraw();
        var image = GetViewport().GetTexture().GetImage();
        if (image is null)
        {
            GD.PushError("VISUAL_BENCH_SHOT viewport image unavailable");
            return;
        }

        var path = ProjectSettings.GlobalizePath($"res://screenshots/{_shot}");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var error = image.SavePng(path);
        GD.Print($"VISUAL_BENCH_SHOT {path} ({error})");
    }
}
