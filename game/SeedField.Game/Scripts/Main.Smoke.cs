using Godot;
using SeedField.Runtime.Js;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main : Node2D
{
    private void SeedDiplomacyAiCapture()
    {
        _engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.Trade, ExpiresInTicks: 200));
        StepOnce();
        _engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "frontier", 20, 0, 30, 0, ExpiresInTicks: 200));
        StepOnce();
        _engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 200));
        StepOnce();
    }

    private void RunDiplomacySmoke()
    {
        var frontier = _world.Regions["frontier"];
        var beforeMaterial = frontier.GetMaterial("A") + frontier.GetMaterial("B");
        var beforeEnergy = frontier.GetEnergy("A") + frontier.GetEnergy("B");

        SeedDiplomacyCapture();

        var proposalId = _world.TreatyProposals.Values
            .Single(proposal => proposal.TargetCivilizationId == "A")
            .Id;
        var incomingOfferId = _world.TradeOffers.Values
            .Single(offer => offer.TargetCivilizationId == "A")
            .Id;
        var outgoingOfferId = _world.TradeOffers.Values
            .Single(offer => offer.ProposerCivilizationId == "A")
            .Id;

        RespondTreatyFromDiplomacy(proposalId, accept: true);
        RespondTradeFromDiplomacy(incomingOfferId, accept: true);
        CancelTradeFromDiplomacy(outgoingOfferId);

        var afterMaterial = frontier.GetMaterial("A") + frontier.GetMaterial("B");
        var afterEnergy = frontier.GetEnergy("A") + frontier.GetEnergy("B");
        var ok = _world.Treaties.Count == 2 &&
                 _world.TradeOffers.Count == 0 &&
                 afterMaterial == beforeMaterial &&
                 afterEnergy == beforeEnergy &&
                 _world.Statistics.TradeMaterialSettledTotal == 70 &&
                 _world.Statistics.TradeEnergySettledTotal == 30;

        GD.Print(
            $"DIPLOMACY_SMOKE treaties={_world.Treaties.Count} " +
            $"offers={_world.TradeOffers.Count} " +
            $"A=M{frontier.GetMaterial("A")}/E{frontier.GetEnergy("A")} " +
            $"B=M{frontier.GetMaterial("B")}/E{frontier.GetEnergy("B")} " +
            $"settled=M{_world.Statistics.TradeMaterialSettledTotal}/" +
            $"E{_world.Statistics.TradeEnergySettledTotal} ok={ok}");

        GetTree().Quit();
    }

    private void RunDiplomacyAiSmoke()
    {
        _engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.Trade, ExpiresInTicks: 200));
        StepOnce();
        var tradeAccepted = _world.Treaties.Values.Any(treaty =>
            treaty.Kind == TreatyKind.Trade);
        var settledBefore = _world.Statistics.TradeMaterialSettledTotal;

        _engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "frontier", 30, 0, 20, 0, ExpiresInTicks: 200));
        StepOnce();
        var offerAccepted =
            _world.TradeOffers.Count == 0 &&
            _world.Statistics.TradeMaterialSettledTotal > settledBefore;

        _engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.NonAggression, ExpiresInTicks: 200));
        StepOnce();
        var napAccepted = _world.Treaties.Values.Any(treaty =>
            treaty.Kind == TreatyKind.NonAggression);
        var napHandled = napAccepted == _opponentDiplomacy.AcceptsNonAggression;

        var ok = tradeAccepted && offerAccepted && napHandled;
        GD.Print(
            $"DIPLOMACY_AI_SMOKE opponent={_opponent.Manifest.PackageId} " +
            $"tradeAccepted={tradeAccepted} offerAccepted={offerAccepted} " +
            $"napAccepted={napAccepted} expectNapAccepted={_opponentDiplomacy.AcceptsNonAggression} " +
            $"napHandled={napHandled} " +
            $"settledM={_world.Statistics.TradeMaterialSettledTotal} ok={ok}");

        GetTree().Quit();
    }

    private void RunDiplomacyCounterSmoke()
    {
        _engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", TreatyKind.Trade, ExpiresInTicks: 200));
        StepOnce();
        var offer = _engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("B", "A", "frontier", 20, 0, 20, 0, ExpiresInTicks: 200));
        CounterTradeFromDiplomacy(offer.TradeOfferId!.Value, 22, 0, 20, 0);
        var countered = _world.TradeOffers.Values.Single();
        StepOnce();

        var settled = _world.Statistics.TradeMaterialSettledTotal == 42;
        var ok = countered.Round == 2 &&
                 _world.TradeOffers.Count == 0 &&
                 settled;
        GD.Print(
            $"DIPLOMACY_COUNTER_SMOKE round={countered.Round} " +
            $"settledM={_world.Statistics.TradeMaterialSettledTotal} ok={ok}");

        GetTree().Quit();
    }

    private void RunPowerBreakerSmoke()
    {
        OpenBreakerFromPower("frontier");
        var opened = _world.PowerGridStates.TryGetValue(
                ("frontier", "A"),
                out var state) &&
            state.BreakerOpen &&
            state.ManualHold;

        StepOnce();
        var held = state is not null && state.BreakerOpen && state.ManualHold;

        CloseBreakerFromPower("frontier");
        var closed = state is not null && !state.BreakerOpen && !state.ManualHold;

        MaintainBreakerFromPower("frontier");
        // The wear was already zero here, so the state check alone would pass even
        // if the handler never ran. Require the command's event to prove the UI
        // wiring executed the maintenance path.
        var maintained =
            state is not null &&
            _history.Any(item => item.Event is BreakerMaintained) &&
            state.TripCount == 0 &&
            !state.MaintenanceRequired;

        var ok = opened && held && closed && maintained;
        GD.Print(
            $"POWER_BREAKER_SMOKE opened={opened} held={held} closed={closed} " +
            $"maintained={maintained} ok={ok}");
        GetTree().Quit();
    }

    private void RunSaveSmoke()
    {
        var before = WorldStateHasher.Compute(_world);
        var tickBefore = _world.Tick;

        SaveGame();

        for (var i = 0; i < 10; i++)
        {
            StepOnce();
        }

        var mutated = WorldStateHasher.Compute(_world);
        LoadGame();
        var after = WorldStateHasher.Compute(_world);
        var ok = before == after && mutated != after && _world.Tick == tickBefore;

        GD.Print(
            $"SAVE_SMOKE ok={ok} before={before:x16} mutated={mutated:x16} after={after:x16} " +
            $"tick={_world.Tick} A=v{_slotA.Version} B=v{_slotB.Version}");

        GetTree().Quit();
    }

    private void SaveGame()
    {
        try
        {
            _engine.CatchUpInactiveRegions();
            var json = GameSaveSerializer.Save(
                _world,
                new[] { _slotA, _slotB },
                _opponent.Manifest.PackageId,
                _opponent.Manifest.Version);
            System.IO.File.WriteAllText(SaveAbsolutePath, json);
            var message = $"saved tick {_world.Tick} · A v{_slotA.Version}";
            _status = message;
            if (_editorLayer.Visible)
            {
                _editor.SetStatus(message, false);
            }
        }
        catch (Exception exception)
        {
            _status = $"save failed: {exception.Message}";
            if (_editorLayer.Visible)
            {
                _editor.SetStatus(_status, true);
            }
        }

        QueueRedraw();
    }

    private void LoadGame()
    {
        try
        {
            if (!System.IO.File.Exists(SaveAbsolutePath))
            {
                _status = "no save file";
            }
            else
            {
                var loaded = GameSaveSerializer.Load(System.IO.File.ReadAllText(SaveAbsolutePath));
                if (loaded.OpponentPackageId is { } opponentId &&
                    BuiltInOpponents.Find(opponentId) is { } opponent)
                {
                    _opponent = opponent;
                }

                _opponentDiplomacy = OpponentDiplomacyProfiles.ForPackage(
                    _opponent.Manifest.PackageId);

                var slotA = loaded.Controllers.FirstOrDefault(controller =>
                    controller.CivilizationId == "A");
                var slotB = loaded.Controllers.FirstOrDefault(controller =>
                    controller.CivilizationId == "B");

                if (slotA is null || slotB is null)
                {
                    foreach (var controller in loaded.Controllers)
                    {
                        controller.Dispose();
                    }

                    throw new SaveFormatException(
                        "MISSING_CONTROLLER",
                        "Save must contain controllers for civilizations A and B.");
                }

                foreach (var extra in loaded.Controllers.Where(controller =>
                             controller != slotA && controller != slotB))
                {
                    extra.Dispose();
                }

                _slotA?.Dispose();
                _slotB?.Dispose();
                _slotA = slotA;
                _slotB = slotB;
                _world = loaded.World;
                _engine = new SimulationEngine(_world);
                _deployedSourceA = _slotA.DeployedScript;
                RebuildDeviceControllers();
                _history.Clear();
                _faultCount = 0;
                _tickAccumulator = 0;
                _paused = true;
                _editor.Source = _deployedSourceA;
                _workshop?.Refresh(_world);
                RefreshDiplomacyIfReady();
                RefreshPowerIfReady();
                RefreshRecoveryIfReady();

                _status = $"loaded tick {_world.Tick} · A v{_slotA.Version}";
            }
        }
        catch (Exception exception)
        {
            _status = $"load failed: {exception.Message}";
        }

        if (_editorLayer.Visible)
        {
            _editor.SetStatus(_status, _status.StartsWith("load failed", StringComparison.Ordinal));
        }

        QueueRedraw();
    }

    private void ResetScenario()
    {
        _slotA?.Dispose();
        _slotB?.Dispose();

        if (string.IsNullOrWhiteSpace(_deployedSourceA))
        {
            _deployedSourceA = Scenarios.ScriptA;
        }

        _opponentDiplomacy = OpponentDiplomacyProfiles.ForPackage(
            _opponent.Manifest.PackageId);
        _world = Scenarios.BuildWorld();
        _engine = new SimulationEngine(_world);
        _slotA = new ControllerSlot("A", _deployedSourceA);
        _slotB = new ControllerSlot("B", _opponent.ProgramSource);
        RebuildDeviceControllers();

        _history.Clear();
        _faultCount = 0;
        _tickAccumulator = 0;
        _workshopSpawnIndex = 0;

        PreRunToCombat();
        RefreshDiplomacyIfReady();
        RefreshPowerIfReady();
        RefreshRecoveryIfReady();
    }

    private void PreRunToCombat()
    {
        while (_world.Tick < PreRunMaxTicks && _world.Statistics.ShotsFired < PreRunMinShots)
        {
            StepOnce();
        }

        _status = $"combat at tick {_world.Tick}";
        GD.Print($"SEEDFIELD_PRERUN tick={_world.Tick} shots={_world.Statistics.ShotsFired} entities={_world.EnumerateEntities().Count()}");
        PruneHistory();
        QueueRedraw();
    }

    private void StepOnce()
    {
        var intents = new List<Intent>();

        foreach (var controller in _deviceControllers)
        {
            if (_world.FindEntity(controller.EntityId) is null)
            {
                continue;
            }

            var decision = controller.Decide(
                _engine.BuildDeviceObservation(controller.EntityId));
            intents.AddRange(decision.Intents);
            _faultCount += decision.Faults.Count;
        }

        var result = _engine.Step(intents);
        foreach (var simulationEvent in result.Events)
        {
            _history.Add(new TimedEvent(result.Tick, simulationEvent));

            if (simulationEvent is EntityDeployed deployedEvent &&
                _world.FindEntity(deployedEvent.EntityId) is { } deployedEntity &&
                _deviceControllers.All(controller => controller.EntityId != deployedEntity.Id))
            {
                RegisterDeviceController(deployedEntity);
            }
        }

        RunOpponentDiplomacy();
        PruneHistory();
        RefreshRecoveryIfReady();
    }

    private void RunOpponentDiplomacy()
    {
        var commands = OpponentDiplomacyPolicy.Decide(
            _world,
            "B",
            "A",
            _engine.HasCommunicationPath("B", "A"),
            _opponentDiplomacy);

        foreach (var command in commands)
        {
            var result = _engine.ExecuteDiplomacyCommand(command);
            foreach (var simulationEvent in result.Events)
            {
                _history.Add(new TimedEvent(simulationEvent.Tick, simulationEvent));
            }
        }
    }

    private void PruneHistory()
    {
        _history.RemoveAll(item => _world.Tick - item.Tick > EventRetentionTicks);
        if (_history.Count > 400)
        {
            _history.RemoveRange(0, _history.Count - 400);
        }
    }

    private void CaptureScreenshot()
    {
        RenderingServer.ForceDraw();
        var image = GetViewport().GetTexture().GetImage();
        var path = ProjectSettings.GlobalizePath(_capturePath);
        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        var error = image.SavePng(path);
        GD.Print($"SCREENSHOT_SAVED {path} ({error})");
        _captureRequested = false;
        GetTree().Quit();
    }

}
