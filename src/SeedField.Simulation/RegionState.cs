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

    /// <summary>Local material stock per civilization. Materials do not teleport.</summary>
    public SortedDictionary<string, RegionStock> Stocks { get; } = new(StringComparer.Ordinal);

    /// <summary>Optional regional grid capacity per civilization; absent means unlimited.</summary>
    public SortedDictionary<string, long> GridCapacities { get; } = new(StringComparer.Ordinal);

    public long GetGridCapacity(string civilizationId) =>
        GridCapacities.TryGetValue(civilizationId, out var capacity)
            ? capacity
            : long.MaxValue;

    public long GetGridFree(string civilizationId)
    {
        var capacity = GetGridCapacity(civilizationId);
        return capacity == long.MaxValue
            ? long.MaxValue
            : Math.Max(0, capacity - GetEnergy(civilizationId));
    }

    public bool Contains(Vec2 position) =>
        position.X >= 0 && position.Y >= 0 && position.X <= WidthMm && position.Y <= HeightMm;

    public EntityState? FindEntity(EntityId id) =>
        Entities.TryGetValue(id, out var entity) ? entity : null;

    public long GetMaterial(string civilizationId) =>
        Stocks.TryGetValue(civilizationId, out var stock) ? stock.Material : 0;

    public long GetEnergy(string civilizationId) =>
        Stocks.TryGetValue(civilizationId, out var stock) ? stock.Energy : 0;

    public long GetOre(string civilizationId) =>
        Stocks.TryGetValue(civilizationId, out var stock) ? stock.Ore : 0;

    public long GetComponents(string civilizationId) =>
        Stocks.TryGetValue(civilizationId, out var stock) ? stock.Components : 0;

    public void AddOre(string civilizationId, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        GetOrCreateStock(civilizationId).Ore += amount;
    }

    public void AddComponents(string civilizationId, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        GetOrCreateStock(civilizationId).Components += amount;
    }

    public bool TryConsumeOre(string civilizationId, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (!Stocks.TryGetValue(civilizationId, out var stock) || stock.Ore < amount)
        {
            return false;
        }

        stock.Ore -= amount;
        return true;
    }

    public bool TryConsumeComponents(string civilizationId, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (!Stocks.TryGetValue(civilizationId, out var stock) || stock.Components < amount)
        {
            return false;
        }

        stock.Components -= amount;
        return true;
    }

    public void AddMaterial(string civilizationId, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        GetOrCreateStock(civilizationId).Material += amount;
    }

    public void AddEnergy(string civilizationId, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        GetOrCreateStock(civilizationId).Energy += amount;
    }

    public bool TryConsumeEnergy(string civilizationId, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (!Stocks.TryGetValue(civilizationId, out var stock) || stock.Energy < amount)
        {
            return false;
        }

        stock.Energy -= amount;
        return true;
    }

    private RegionStock GetOrCreateStock(string civilizationId)
    {
        if (!Stocks.TryGetValue(civilizationId, out var stock))
        {
            stock = new RegionStock();
            Stocks.Add(civilizationId, stock);
        }

        return stock;
    }

    public bool TryConsumeMaterial(string civilizationId, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (!Stocks.TryGetValue(civilizationId, out var stock) || stock.Material < amount)
        {
            return false;
        }

        stock.Material -= amount;
        return true;
    }
}

public sealed class RegionStock
{
    public long Material { get; internal set; }

    public long Energy { get; internal set; }

    /// <summary>Raw ore extracted from deposits; smelters convert it to material.</summary>
    public long Ore { get; internal set; }

    /// <summary>Manufactured components; advanced designs consume them.</summary>
    public long Components { get; internal set; }
}
