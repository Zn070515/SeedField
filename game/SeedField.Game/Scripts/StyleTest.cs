using Godot;

namespace SeedField.Game;

/// <summary>
/// A/B camera style test controller. Presentation-only: it never reads or mutates
/// WorldState, and every number shown is demo data.
/// </summary>
public partial class StyleTest : Node2D
{
    private string _route = "oblique";
    private string _zoom = "mid";
    private string _overlay = "default";
    private bool _capture;
    private int _frames;

    public override void _Ready()
    {
        ParseArgs();

        var world = new StyleTestWorld
        {
            Name = "StyleWorld",
            Route = _route,
            Overlay = _overlay,
            Zoom = _zoom,
        };
        AddChild(world);

        var camera = new Camera2D
        {
            Name = "StyleCamera",
            Position = world.FocusPoint,
            Zoom = new Vector2(world.CameraZoom, world.CameraZoom),
            Enabled = true,
            PositionSmoothingEnabled = false,
        };
        AddChild(camera);
        camera.MakeCurrent();

        var layer = new CanvasLayer { Name = "HudLayer" };
        AddChild(layer);
        var hud = new StyleTestHud
        {
            Name = "StyleHud",
            Route = _route,
            Zoom = _zoom,
            Overlay = _overlay,
            WorldZoom = world.CameraZoom,
        };
        layer.AddChild(hud);
        hud.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!_capture)
        {
            return;
        }

        _frames++;
        if (_frames < 4)
        {
            return;
        }

        var path = ProjectSettings.GlobalizePath(
            $"res://screenshots/art-{_route}-{_zoom}-{_overlay}.png");
        var image = GetViewport().GetTexture().GetImage();
        if (image is null)
        {
            GD.PushError($"ART_SCREENSHOT_FAILED viewport image unavailable: {path}");
        }
        else
        {
            var error = image.SavePng(path);
            if (error == Error.Ok)
            {
                GD.Print($"ART_SCREENSHOT_SAVED {path}");
            }
            else
            {
                GD.PushError($"ART_SCREENSHOT_FAILED {error}: {path}");
            }
        }

        GetTree().Quit();
    }

    private void ParseArgs()
    {
        foreach (var argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith("--route=", StringComparison.Ordinal))
            {
                _route = argument["--route=".Length..];
            }
            else if (argument.StartsWith("--zoom=", StringComparison.Ordinal))
            {
                _zoom = argument["--zoom=".Length..];
            }
            else if (argument.StartsWith("--overlay=", StringComparison.Ordinal))
            {
                _overlay = argument["--overlay=".Length..];
            }
            else if (argument == "--capture-art")
            {
                _capture = true;
            }
        }

        if (_route is not ("oblique" or "topdown"))
        {
            _route = "oblique";
        }

        if (_zoom is not ("far" or "mid" or "near"))
        {
            _zoom = "mid";
        }

        if (_overlay is not ("default" or "selected" or "tactical" or "debug"))
        {
            _overlay = "default";
        }
    }
}
