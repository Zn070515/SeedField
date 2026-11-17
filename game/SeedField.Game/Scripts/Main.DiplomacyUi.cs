using Godot;
using SeedField.Runtime.Js;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main : Node2D
{
    private void ToggleDiplomacy()
    {
        if (_diplomacyLayer.Visible)
        {
            CloseDiplomacy();
        }
        else
        {
            OpenDiplomacy();
        }
    }

    private void OpenDiplomacy()
    {
        if (_editorLayer.Visible)
        {
            CloseProgramEditor();
        }

        if (_workshopLayer.Visible)
        {
            CloseWorkshop();
        }

        if (_powerLayer.Visible)
        {
            ClosePower();
        }

        _diplomacyLayer.Visible = true;
        RefreshDiplomacy();
        _paused = true;
        _status = "diplomacy · paused";
        QueueRedraw();
    }

    private void CloseDiplomacy()
    {
        _diplomacyLayer.Visible = false;
        _paused = _captureRequested;
        _status = _paused ? "capture" : "running";
        QueueRedraw();
    }

    private void RefreshDiplomacy()
    {
        _diplomacy.Refresh(_world, _engine.HasCommunicationPath("A", "B"));
    }

    private void RefreshDiplomacyIfReady()
    {
        if (_diplomacy is not null)
        {
            RefreshDiplomacy();
        }
    }

    private void ProposeTreatyFromDiplomacy(TreatyKind kind)
    {
        ApplyDiplomacyResult(_engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("A", "B", kind, ExpiresInTicks: 200)));
    }

    private void BreakTreatyFromDiplomacy(long treatyId)
    {
        ApplyDiplomacyResult(_engine.ExecuteDiplomacyCommand(
            new BreakTreatyCommand("A", treatyId)));
    }

    private void RespondTreatyFromDiplomacy(long proposalId, bool accept)
    {
        ApplyDiplomacyResult(_engine.ExecuteDiplomacyCommand(
            new RespondTreatyCommand("A", proposalId, accept)));
    }

    private void RespondTradeFromDiplomacy(long offerId, bool accept)
    {
        ApplyDiplomacyResult(_engine.ExecuteDiplomacyCommand(
            new RespondTradeCommand("A", offerId, accept)));
    }

    private void CancelTradeFromDiplomacy(long offerId)
    {
        ApplyDiplomacyResult(_engine.ExecuteDiplomacyCommand(
            new CancelTradeCommand("A", offerId)));
    }

    private void ProposeTradeFromDiplomacy(
        long giveMaterial,
        long giveEnergy,
        long wantMaterial,
        long wantEnergy)
    {
        ApplyDiplomacyResult(_engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand(
                "A",
                "B",
                "frontier",
                giveMaterial,
                giveEnergy,
                wantMaterial,
                wantEnergy,
                ExpiresInTicks: 200)));
    }

    private void CounterTradeFromDiplomacy(
        long offerId,
        long giveMaterial,
        long giveEnergy,
        long wantMaterial,
        long wantEnergy)
    {
        ApplyDiplomacyResult(_engine.ExecuteDiplomacyCommand(
            new CounterTradeCommand(
                "A",
                offerId,
                giveMaterial,
                giveEnergy,
                wantMaterial,
                wantEnergy,
                ExpiresInTicks: 200)));
    }

    private void ApplyDiplomacyResult(DiplomacyCommandResult result)
    {
        _diplomacy.SetStatus(
            result.Success
                ? result.Message
                : $"failed [{result.ErrorCode}]: {result.Message}",
            !result.Success);
        RefreshDiplomacy();
        QueueRedraw();
    }

    private void SeedDiplomacyCapture()
    {
        var tradeProposal = _engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("B", "A", TreatyKind.Trade, ExpiresInTicks: 300));
        var tradeProposalId = tradeProposal.ProposalId ??
            _world.TreatyProposals.Values
                .Where(proposal =>
                    proposal.Kind == TreatyKind.Trade &&
                    string.Equals(
                        proposal.ProposerCivilizationId,
                        "B",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        proposal.TargetCivilizationId,
                        "A",
                        StringComparison.Ordinal))
                .OrderBy(proposal => proposal.Id)
                .Select(proposal => (long?)proposal.Id)
                .LastOrDefault();
        if (tradeProposalId is not null)
        {
            _engine.ExecuteDiplomacyCommand(
                new RespondTreatyCommand("A", tradeProposalId.Value, Accept: true));
        }
        _engine.ExecuteDiplomacyCommand(
            new ProposeTreatyCommand("B", "A", TreatyKind.NonAggression, ExpiresInTicks: 200));
        _engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("B", "A", "frontier", 40, 20, 30, 10, ExpiresInTicks: 200));
        _engine.ExecuteDiplomacyCommand(
            new ProposeTradeCommand("A", "B", "frontier", 25, 10, 15, 5, ExpiresInTicks: 200));

        RefreshDiplomacy();
        _diplomacy.SetStatus(
            "incoming NAP proposal and trade offers ready for review",
            false);
    }

}
