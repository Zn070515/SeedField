using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main
{
    /// <summary>
    /// M1 admission panel. It renders only tutorial facts derived from the
    /// authoritative simulation; it never decides completion itself.
    /// </summary>
    private void DrawTutorialPanel()
    {
        const float x = 24f;
        const float y = 470f;
        const float width = 344f;
        const float rowHeight = 15f;
        const float header = 30f;
        const float hint = 18f;

        var rows = TutorialSteps.All.Count;
        var height = header + (rows * rowHeight) + (hint * 2) + 8f;
        var panel = new Rect2(x - 10f, y - 22f, width, height);
        DrawRect(panel, new Color(0.04f, 0.07f, 0.11f, 0.92f));
        DrawRect(panel, new Color(Border, 0.55f), filled: false, width: 1f, antialiased: true);

        var complete = _tutorial.IsComplete;
        DrawString(
            HudFont,
            new Vector2(x, y - 4f),
            complete ? "TUTORIAL · PvE ADMISSION COMPLETE" : "TUTORIAL · PvE ADMISSION",
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

        if (complete)
        {
            DrawString(
                HudFont,
                new Vector2(x, rowY + 6f),
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
                new Vector2(x, rowY + 4f),
                $"NEXT · {next.Title}: {next.Requirement}",
                HorizontalAlignment.Left,
                width - 20f,
                11,
                Muted);
        }
    }

    private TutorialStepDefinition? NextTutorialStep()
    {
        foreach (var definition in TutorialSteps.All)
        {
            if (!_tutorial.HasCompleted(definition.Step))
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
