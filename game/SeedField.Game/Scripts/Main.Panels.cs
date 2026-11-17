using Godot;
using SeedField.Runtime.Js;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main : Node2D
{
    private void BuildProgramEditor()
    {
        _editorLayer = new CanvasLayer { Layer = 20, Visible = false };
        AddChild(_editorLayer);

        _editorDimmer = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        _editorDimmer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _editorLayer.AddChild(_editorDimmer);

        _editor = new ProgramEditor();
        _editor.DeployRequested += DeployProgram;
        _editor.CloseRequested += CloseProgramEditor;
        _editorLayer.AddChild(_editor);
        _editor.Source = _deployedSourceA;
    }

    private void RebuildDeviceControllers()
    {
        foreach (var controller in _deviceControllers)
        {
            controller.Dispose();
        }

        _deviceControllers.Clear();

        foreach (var entity in _world.EnumerateEntities())
        {
            RegisterDeviceController(entity);
        }
    }

    private void RegisterDeviceController(EntityState entity)
    {
        var source = entity.CivilizationId == "A"
            ? _slotA.DeployedScript
            : _slotB.DeployedScript;
        _deviceControllers.Add(new DeviceControllerSlot(
            entity.Id,
            entity.CivilizationId,
            source));
    }

    private void BuildWorkshopPanel()
    {
        _workshopLayer = new CanvasLayer { Layer = 19, Visible = false };
        AddChild(_workshopLayer);

        var dimmer = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        dimmer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _workshopLayer.AddChild(dimmer);

        _workshop = new WorkshopPanel();
        _workshop.DeployRequested += DeployFromWorkshop;
        _workshop.UpgradeRequested += UpgradeFromWorkshop;
        _workshop.CancelConstructionRequested += CancelConstructionFromWorkshop;
        _workshop.CloseRequested += CloseWorkshop;
        _workshopLayer.AddChild(_workshop);
        _workshop.Refresh(_world);
    }

    private void BuildDiplomacyPanel()
    {
        _diplomacyLayer = new CanvasLayer { Layer = 18, Visible = false };
        AddChild(_diplomacyLayer);

        var dimmer = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        dimmer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _diplomacyLayer.AddChild(dimmer);

        _diplomacy = new DiplomacyPanel();
        _diplomacy.ProposeTreatyRequested += ProposeTreatyFromDiplomacy;
        _diplomacy.BreakTreatyRequested += BreakTreatyFromDiplomacy;
        _diplomacy.RespondTreatyRequested += RespondTreatyFromDiplomacy;
        _diplomacy.RespondTradeRequested += RespondTradeFromDiplomacy;
        _diplomacy.CancelTradeRequested += CancelTradeFromDiplomacy;
        _diplomacy.ProposeTradeRequested += ProposeTradeFromDiplomacy;
        _diplomacy.CounterTradeRequested += CounterTradeFromDiplomacy;
        _diplomacy.CloseRequested += CloseDiplomacy;
        _diplomacyLayer.AddChild(_diplomacy);
        RefreshDiplomacy();
    }

}
