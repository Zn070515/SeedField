using Godot;
using SeedField.Runtime.Js;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main : Node2D
{
    private void BuildPowerPanel()
    {
        _powerLayer = new CanvasLayer { Layer = 17, Visible = false };
        AddChild(_powerLayer);

        var dimmer = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        dimmer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _powerLayer.AddChild(dimmer);

        _power = new PowerPanel();
        _power.CloseRequested += ClosePower;
        _power.OpenBreakerRequested += OpenBreakerFromPower;
        _power.CloseBreakerRequested += CloseBreakerFromPower;
        _power.MaintainBreakerRequested += MaintainBreakerFromPower;
        _powerLayer.AddChild(_power);
        RefreshPower();
    }

    private void TogglePower()
    {
        if (_powerLayer.Visible)
        {
            ClosePower();
        }
        else
        {
            OpenPower();
        }
    }

    private void OpenPower()
    {
        if (_editorLayer.Visible)
        {
            CloseProgramEditor();
        }

        if (_workshopLayer.Visible)
        {
            CloseWorkshop();
        }

        if (_diplomacyLayer.Visible)
        {
            CloseDiplomacy();
        }

        _powerLayer.Visible = true;
        RefreshPower();
        _paused = true;
        _status = "power grid · paused";
        QueueRedraw();
    }

    private void ClosePower()
    {
        _powerLayer.Visible = false;
        _paused = _captureRequested;
        _status = _paused ? "capture" : "running";
        QueueRedraw();
    }

    private void RefreshPower()
    {
        _power.Refresh(_world, "A");
    }

    private void OpenBreakerFromPower(string regionId)
    {
        ApplyPowerResult(_engine.ExecutePowerCommand(
            new OpenBreakerCommand("A", regionId)));
    }

    private void CloseBreakerFromPower(string regionId)
    {
        ApplyPowerResult(_engine.ExecutePowerCommand(
            new CloseBreakerCommand("A", regionId)));
    }

    private void MaintainBreakerFromPower(string regionId)
    {
        ApplyPowerResult(_engine.ExecutePowerCommand(
            new MaintainBreakerCommand("A", regionId)));
    }

    private void ApplyPowerResult(PowerCommandResult result)
    {
        foreach (var simulationEvent in result.Events)
        {
            _history.Add(new TimedEvent(simulationEvent.Tick, simulationEvent));
        }

        _power.SetStatus(
            result.Success
                ? result.Message
                : $"failed [{result.ErrorCode}]: {result.Message}",
            !result.Success);
        RefreshPower();
        QueueRedraw();
    }

    private void RefreshPowerIfReady()
    {
        if (_power is not null)
        {
            RefreshPower();
        }
    }

}
