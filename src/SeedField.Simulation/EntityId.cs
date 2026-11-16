using System.Globalization;

namespace SeedField.Simulation;

/// <summary>Stable, world-wide entity identifier. Never reused within a world.</summary>
public readonly record struct EntityId(long Value) : IComparable<EntityId>
{
    public int CompareTo(EntityId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
