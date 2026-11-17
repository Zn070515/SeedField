using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

public readonly record struct VisualEffect(
    Vec2 WorldPosition,
    Color Color,
    long StartTick,
    int DurationTicks,
    float RadiusMm,
    string? Label);

/// <summary>Tick-driven transient rings for construction, deploy, blackout and death.</summary>
public sealed class VisualEffectFeed
{
    private readonly List<VisualEffect> _effects = new();

    public void Add(
        Vec2 position,
        Color color,
        long tick,
        int durationTicks,
        float radiusMm,
        string? label = null)
    {
        _effects.Add(new VisualEffect(
            position,
            color,
            tick,
            durationTicks,
            radiusMm,
            label));
    }

    public void Prune(long tick)
    {
        _effects.RemoveAll(effect => tick - effect.StartTick > effect.DurationTicks);
    }

    public void Clear() => _effects.Clear();

    public void Draw(
        CanvasItem canvas,
        Func<Vec2, Vector2> map,
        long tick,
        float zoom,
        float mapScale)
    {
        foreach (var effect in _effects)
        {
            var age = tick - effect.StartTick;
            if (age < 0 || age > effect.DurationTicks)
            {
                continue;
            }

            var progress = age / (float)effect.DurationTicks;
            var alpha = 1f - progress;
            var center = map(effect.WorldPosition);
            var radius = effect.RadiusMm * mapScale * zoom *
                         (0.35f + progress * 0.85f);
            canvas.DrawArc(
                center,
                radius,
                0,
                Mathf.Tau,
                64,
                new Color(effect.Color, alpha * 0.85f),
                2.2f);
            canvas.DrawCircle(
                center,
                radius * 0.18f,
                new Color(effect.Color, alpha * 0.22f));
            if (effect.Label is not null)
            {
                canvas.DrawString(
                    ThemeDB.FallbackFont,
                    center + new Vector2(-24f, -radius - 6f),
                    effect.Label,
                    HorizontalAlignment.Left,
                    -1,
                    10,
                    new Color(effect.Color, alpha));
            }
        }
    }
}
