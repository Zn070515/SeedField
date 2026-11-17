using Godot;
using SeedField.Runtime.Js;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main : Node2D
{
    private void ToggleWorkshop()
    {
        if (_workshopLayer.Visible)
        {
            CloseWorkshop();
        }
        else
        {
            OpenWorkshop();
        }
    }

    private void OpenWorkshop()
    {
        if (_editorLayer.Visible)
        {
            CloseProgramEditor();
        }

        if (_diplomacyLayer.Visible)
        {
            CloseDiplomacy();
        }

        if (_powerLayer.Visible)
        {
            ClosePower();
        }

        _workshopLayer.Visible = true;
        _workshop.Refresh(_world);
        _paused = true;
        _status = "workshop · paused";
        QueueRedraw();
    }

    private void CloseWorkshop()
    {
        _workshopLayer.Visible = false;
        _paused = _captureRequested;
        _status = _paused ? "capture" : "running";
        QueueRedraw();
    }

    private void DeployFromWorkshop(string blueprintId)
    {
        var versions = _world.Workshop.GetVersions(blueprintId);
        if (versions.Count == 0)
        {
            _workshop.SetStatus($"design '{blueprintId}' has no published version", true);
            return;
        }

        var latest = versions[^1];
        var result = _engine.ExecuteDeploymentCommand(new DeployCommand(
            "A",
            latest,
            "frontier",
            NextWorkshopSpawn()));

        if (result.Success && result.EntityId is { } entityId)
        {
            var entity = _world.FindEntity(entityId);
            if (entity is not null)
            {
                RegisterDeviceController(entity);
            }
        }

        _workshop.SetStatus(
            result.Success
                ? result.EntityId is { } deployedId
                    ? $"deployed {blueprintId} v{latest.Version} as entity {deployedId}"
                    : $"queued {blueprintId} v{latest.Version}: {result.Message}"
                : $"deploy failed [{result.ErrorCode}]: {result.Message}",
            !result.Success);

        _workshop.Refresh(_world);
        QueueRedraw();
    }

    private void UpgradeFromWorkshop(EntityId entityId)
    {
        var entity = _world.FindEntity(entityId);
        if (entity is null || string.IsNullOrEmpty(entity.BlueprintId))
        {
            _workshop.SetStatus($"entity {entityId} has no blueprint reference", true);
            return;
        }

        var versions = _world.Workshop.GetVersions(entity.BlueprintId);
        if (versions.Count == 0)
        {
            _workshop.SetStatus($"blueprint '{entity.BlueprintId}' is missing", true);
            return;
        }

        var latest = versions[^1];
        if (latest.Version == entity.BlueprintVersion)
        {
            _workshop.SetStatus($"entity {entityId} is already at v{latest.Version}", false);
        }
        else
        {
            var result = _engine.ExecuteDeploymentCommand(new UpgradeCommand(
                "A",
                entityId,
                latest));
            _workshop.SetStatus(
                result.Success
                    ? $"upgraded entity {entityId}: {result.Message}"
                    : $"upgrade failed [{result.ErrorCode}]: {result.Message}",
                !result.Success);
        }

        _workshop.Refresh(_world);
        QueueRedraw();
    }

    private void CancelConstructionFromWorkshop(long orderId)
    {
        var result = _engine.ExecuteDeploymentCommand(new CancelConstructionCommand(
            "A",
            orderId));

        _workshop.SetStatus(
            result.Success
                ? result.Message
                : $"cancel failed [{result.ErrorCode}]: {result.Message}",
            !result.Success);

        _workshop.Refresh(_world);
        QueueRedraw();
    }

    private Vec2 NextWorkshopSpawn()
    {
        var index = _workshopSpawnIndex++;
        return new Vec2(
            1_200 + (index % 4 * 300),
            2_200 + (index / 4 * 300));
    }

    private void ToggleProgramEditor()
    {
        if (_editorLayer.Visible)
        {
            CloseProgramEditor();
        }
        else
        {
            OpenProgramEditor();
        }
    }

    private void OpenProgramEditor()
    {
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

        _editorLayer.Visible = true;
        _paused = true;
        _status = "program editor · paused";
        _editor.FocusEditor();
        QueueRedraw();
    }

    private void CloseProgramEditor()
    {
        _editorLayer.Visible = false;
        _paused = _captureRequested;
        _status = _paused ? "capture" : "running";
        QueueRedraw();
    }

    private void DeployProgram()
    {
        var result = _slotA.Deploy(_editor.Source);
        if (result.Success)
        {
            _deployedSourceA = _editor.Source;
            RebuildDeviceControllers();
            _editor.SetStatus($"deployed v{result.Version} at tick {_world.Tick}", false);
        }
        else
        {
            _editor.SetStatus($"deploy failed; still v{result.Version}: {result.Error}", true);
        }

        QueueRedraw();
    }

}
