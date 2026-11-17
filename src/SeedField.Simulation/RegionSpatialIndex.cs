namespace SeedField.Simulation;

/// <summary>
/// Uniform-grid index over one region. Built once per observation cache rebuild;
/// entity ids inside buckets stay sorted so query results remain deterministic.
/// </summary>
internal sealed class RegionSpatialIndex
{
    private const int CellSizeMm = 2_000;

    private readonly Dictionary<long, List<EntityState>> _buckets = new();
    private readonly int _columns;
    private readonly int _rows;

    private RegionSpatialIndex(RegionState region)
    {
        _columns = Math.Max(1, (region.WidthMm + CellSizeMm - 1) / CellSizeMm);
        _rows = Math.Max(1, (region.HeightMm + CellSizeMm - 1) / CellSizeMm);

        foreach (var entity in region.Entities.Values)
        {
            var key = Key(
                Math.Clamp(entity.Position.X / CellSizeMm, 0, _columns - 1),
                Math.Clamp(entity.Position.Y / CellSizeMm, 0, _rows - 1));
            if (!_buckets.TryGetValue(key, out var bucket))
            {
                bucket = new List<EntityState>();
                _buckets.Add(key, bucket);
            }

            bucket.Add(entity);
        }

        foreach (var bucket in _buckets.Values)
        {
            bucket.Sort((left, right) => left.Id.Value.CompareTo(right.Id.Value));
        }
    }

    public static RegionSpatialIndex Build(RegionState region) => new(region);

    public void Query(Vec2 center, int radiusMm, List<EntityState> results)
    {
        results.Clear();
        if (radiusMm <= 0)
        {
            return;
        }

        var minX = Math.Clamp((center.X - radiusMm) / CellSizeMm, 0, _columns - 1);
        var maxX = Math.Clamp((center.X + radiusMm) / CellSizeMm, 0, _columns - 1);
        var minY = Math.Clamp((center.Y - radiusMm) / CellSizeMm, 0, _rows - 1);
        var maxY = Math.Clamp((center.Y + radiusMm) / CellSizeMm, 0, _rows - 1);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (_buckets.TryGetValue(Key(x, y), out var bucket))
                {
                    results.AddRange(bucket);
                }
            }
        }
    }

    private static long Key(int x, int y) => ((long)y << 32) | (uint)x;
}
