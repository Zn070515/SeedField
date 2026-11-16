using System.Globalization;

namespace SeedField.Simulation;

/// <summary>
/// Integer 2D position in millimetres. Deliberately integer-only so tick results
/// are reproducible without depending on floating-point behaviour.
/// </summary>
public readonly record struct Vec2(int X, int Y)
{
    public long DistanceSquaredTo(Vec2 other)
    {
        var dx = (long)other.X - X;
        var dy = (long)other.Y - Y;
        return (dx * dx) + (dy * dy);
    }

    public bool IsWithinRange(Vec2 other, int rangeMm)
    {
        if (rangeMm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rangeMm), "Range must be non-negative.");
        }

        var range = (long)rangeMm;
        return DistanceSquaredTo(other) <= range * range;
    }

    /// <summary>
    /// Moves at most <paramref name="maxStep"/> millimetres toward <paramref name="target"/>.
    /// Uses integer arithmetic only; when the remaining distance is at most one step the
    /// exact target is returned.
    /// </summary>
    public Vec2 MoveToward(Vec2 target, int maxStep)
    {
        if (maxStep <= 0)
        {
            return this;
        }

        var dx = (long)target.X - X;
        var dy = (long)target.Y - Y;
        var distanceSquared = (dx * dx) + (dy * dy);
        var stepSquared = (long)maxStep * maxStep;

        if (distanceSquared == 0)
        {
            return this;
        }

        if (distanceSquared <= stepSquared)
        {
            return target;
        }

        var distance = IntegerMath.Sqrt(distanceSquared);
        if (distance == 0)
        {
            return target;
        }

        var stepX = dx * maxStep / distance;
        var stepY = dy * maxStep / distance;

        if (stepX == 0 && stepY == 0)
        {
            // Guarantee progress on very oblique vectors.
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                stepX = Math.Sign(dx);
            }
            else
            {
                stepY = Math.Sign(dy);
            }
        }

        return new Vec2((int)(X + stepX), (int)(Y + stepY));
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"({X},{Y})");
}

internal static class IntegerMath
{
    /// <summary>Integer square root (floor) for non-negative values.</summary>
    public static long Sqrt(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (value == 0)
        {
            return 0;
        }

        var root = value;
        var next = (root + (value / root)) / 2;
        while (next < root)
        {
            root = next;
            next = (root + (value / root)) / 2;
        }

        return root;
    }
}
