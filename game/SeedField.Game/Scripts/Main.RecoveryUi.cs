using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main : Node2D
{
    private void BuildRecoveryPanel()
    {
        _recoveryLayer = new CanvasLayer { Layer = 18, Visible = false };
        AddChild(_recoveryLayer);

        var dimmer = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        dimmer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _recoveryLayer.AddChild(dimmer);

        _recovery = new RecoveryPanel();
        _recovery.CloseRequested += CloseRecovery;
        _recovery.RecoverRequested += RecoverFromPanel;
        _recovery.MaintainRequested += MaintainCoreFromPanel;
        _recoveryLayer.AddChild(_recovery);
        RefreshRecovery();
    }

    private void ToggleRecovery()
    {
        if (_recoveryLayer.Visible)
        {
            CloseRecovery();
        }
        else
        {
            OpenRecovery();
        }
    }

    private void OpenRecovery()
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

        if (_powerLayer.Visible)
        {
            ClosePower();
        }

        _recoveryLayer.Visible = true;
        RefreshRecovery();
        _paused = true;
        _status = "core recovery · paused";
        QueueRedraw();
    }

    private void CloseRecovery()
    {
        _recoveryLayer.Visible = false;
        _paused = _captureRequested;
        _status = _paused ? "capture" : "running";
        QueueRedraw();
    }

    private void RefreshRecovery()
    {
        _recovery.Refresh(_world, "A");
    }

    private void RefreshRecoveryIfReady()
    {
        if (_recovery is not null)
        {
            RefreshRecovery();
        }
    }

    private void RecoverFromPanel()
    {
        if (!_world.Cores.TryGetValue("A", out var core) ||
            !_world.Regions.TryGetValue(core.RegionId, out var region))
        {
            _recovery.SetStatus("no seed core in this scenario", true);
            return;
        }

        var position = new Vec2(region.WidthMm / 2, region.HeightMm / 2);
        ApplyRecoveryResult(_engine.ExecuteRecoveryCommand(
            new BootstrapCoreCommand("A", Scenarios.EmergencyRecovery, position)));
    }

    private void MaintainCoreFromPanel()
    {
        ApplyRecoveryResult(_engine.ExecuteRecoveryCommand(
            new MaintainCoreCommand("A")));
    }

    private void ApplyRecoveryResult(RecoveryCommandResult result)
    {
        if (result.Event is not null)
        {
            _history.Add(new TimedEvent(result.Event.Tick, result.Event));
        }

        _recovery.SetStatus(
            result.Success
                ? result.Message
                : $"failed [{result.ErrorCode}]: {result.Message}",
            !result.Success);
        RefreshRecovery();
        QueueRedraw();
    }

    private void RunRecoverySmoke()
    {
        OpenRecovery();
        for (var i = 0; i < 40; i++)
        {
            StepOnce();
        }

        RecoverFromPanel();
        var core = _world.Cores["A"];
        var recovered = core.RecoveriesInWindow == 1 && core.RecoveryStreak == 1;

        MaintainCoreFromPanel();
        var cleared = core.RecoveriesInWindow == 0 &&
                      core.RecoveryStreak == 0 &&
                      core.LastRecoveryTick == -1;

        var ok = recovered && cleared;
        GD.Print(
            $"RECOVERY_SMOKE recovered={recovered} cleared={cleared} " +
            $"material={core.MaterialReserve} ok={ok}");
        GetTree().Quit();
    }
}
