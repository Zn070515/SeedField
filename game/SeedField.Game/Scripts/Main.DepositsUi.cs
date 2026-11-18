using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main
{
    private void DrawDeposits()
    {
        foreach (var deposit in _world.Deposits.Values)
        {
            var center = Map(deposit.Position);
            var radius = Math.Max(9f, deposit.RadiusMm * MapScale);
            var color = OreVisualColor(deposit.Kind);
            var depleted = deposit.IsDepleted;
            DrawCircle(center, radius, new Color(color, depleted ? 0.05f : 0.14f));
            DrawArc(
                center,
                radius,
                0,
                Mathf.Tau,
                40,
                new Color(color, depleted ? 0.25f : 0.75f),
                1.6f,
                true);

            for (var index = 0; index < 6; index++)
            {
                var angle = (Mathf.Tau * index / 6f) + (deposit.Id * 0.7f);
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 0.55f;
                DrawCircle(center + offset, 2.6f, new Color(color, depleted ? 0.25f : 0.9f));
            }

            DrawString(
                HudFont,
                center + new Vector2(-24, radius + 15),
                depleted
                    ? "DEPLETED"
                    : $"{deposit.Kind.ToString().ToUpperInvariant()} {deposit.Remaining}",
                HorizontalAlignment.Left,
                -1,
                10,
                new Color(color, depleted ? 0.45f : 0.9f));
        }
    }

    private static Color OreVisualColor(OreKind kind) =>
        kind switch
        {
            OreKind.Copper => new Color("c47a45"),
            OreKind.Lead => new Color("8e8494"),
            OreKind.Titanium => new Color("78a8a0"),
            OreKind.Coal => new Color("8a8780"),
            _ => Muted,
        };
}
