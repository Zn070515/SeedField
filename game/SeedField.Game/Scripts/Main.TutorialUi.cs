using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main
{
    private IReadOnlyList<ChallengeStatus> _challengeStatuses =
        ConstructionChallenges.Evaluate(new TutorialProgress("A"));

    /// <summary>
    /// M1 admission panel. It renders only tutorial/challenge facts derived from the
    /// authoritative simulation; it never decides completion itself.
    /// </summary>
    private void DrawTutorialPanel()
    {
        const float x = 24f;
        const float y = 462f;
        const float width = 344f;
        const float rowHeight = 14f;
        const float header = 30f;
        const float hint = 18f;

        var rows = TutorialSteps.All.Count;
        var capstones = _challengeStatuses.Count(status => status.Challenge.Step is null);
        var height = header + ((rows + capstones) * rowHeight) + (hint * 2) + 8f;
        var panel = new Rect2(x - 10f, y - 22f, width, height);
        DrawRect(panel, new Color(0.04f, 0.07f, 0.11f, 0.92f));
        DrawRect(panel, new Color(Border, 0.55f), filled: false, width: 1f, antialiased: true);

        var complete = _tutorial.IsComplete;
        DrawString(
            HudFont,
            new Vector2(x, y - 4f),
            complete ? "TUTORIAL · PvE & CHALLENGES COMPLETE" : "TUTORIAL · PvE & CHALLENGES",
            HorizontalAlignment.Left,
            -1,
            12,
            complete ? Green : Amber);

        var rowY = y + 18f;
        foreach (var definition in TutorialSteps.All)
        {
            var done = _tutorial.HasCompleted(definition.Step);
            var mark = done ? "[x]" : "[ ]";
            var tick = done ? $"   t{_tutorial.CompletionTick(definition.Step)}" : string.Empty;
            DrawString(
                HudFont,
                new Vector2(x, rowY),
                $"{mark} {definition.Title}{tick}",
                HorizontalAlignment.Left,
                -1,
                12,
                done ? Green : Text);
            rowY += rowHeight;
        }

        foreach (var status in _challengeStatuses)
        {
            if (status.Challenge.Step is not null)
            {
                continue;
            }

            var gate = status.Challenge.Gate == ChallengeGate.All ? "AND" : "OR";
            var mark = status.Completed ? "[x]" : "[ ]";
            DrawString(
                HudFont,
                new Vector2(x, rowY),
                $"{mark} {gate} · {status.Challenge.Title}",
                HorizontalAlignment.Left,
                -1,
                11,
                status.Completed ? Green : Muted);
            rowY += rowHeight;
        }

        if (complete)
        {
            DrawString(
                HudFont,
                new Vector2(x, rowY + 4f),
                "PVE READY · free world unlocked",
                HorizontalAlignment.Left,
                -1,
                12,
                Green);
            return;
        }

        var next = NextTutorialStep();
        if (next is not null)
        {
            DrawString(
                HudFont,
                new Vector2(x, rowY + 2f),
                $"NEXT · {next.Title}: {next.Requirement}",
                HorizontalAlignment.Left,
                width - 20f,
                11,
                Muted);
        }
    }

    private void RefreshChallenges()
    {
        _challengeStatuses = ConstructionChallenges.Evaluate(_tutorial);
    }

    private TutorialStepDefinition? NextTutorialStep()
    {
        foreach (var definition in TutorialSteps.All)
        {
            if (definition.Step == _tutorial.NextStep)
            {
                return definition;
            }
        }

        return null;
    }

    private void DrawTutorialSummary()
    {
        var unlocked = _tutorial.PveUnlocked;
        var text = unlocked
            ? "TUTORIAL 5/5 · PvE READY"
            : $"TUTORIAL {_tutorial.CompletedCount}/5 · PvE LOCKED";
        DrawString(
            HudFont,
            new Vector2(1_120, 82),
            text,
            HorizontalAlignment.Left,
            -1,
            10,
            unlocked ? Green : Amber);
    }
}
