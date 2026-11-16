using System.Collections.Generic;

namespace SeedField.Simulation;

/// <summary>
/// One connected 2D region. A region owns real positions; it is not an abstract node.
/// The entity dictionary is kept sorted by id so every iteration order is reproducible.
/// </summary>
public sealed class RegionState
{
    internal RegionState(string id, int widthMm, int heightMm, bool isCoreRegion)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Region id is required.", nameof(id));
        }

        if (widthMm <= 0 || heightMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(widthMm), "Region dimensions must be positive.");
        }

        Id = id;
        WidthMm = widthMm;
        HeightMm = heightMm;
        IsCoreRegion = isCoreRegion;
    }

    public string Id { get; }

    public int WidthMm { get; }

    public int HeightMm { get; }

    public bool IsCoreRegion { get; }

    public SortedDictionary<EntityId, EntityState> Entities { get; } = new();

    public bool Contains(Vec2 position) =>
        position.X >= 0 && position.Y >= 0 && position.X <= WidthMm && position.Y <= HeightMm;

    public EntityState? FindEntity(EntityId id) =>
        Entities.TryGetValue(id, out var entity) ? entity : null;
}
