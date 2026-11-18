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
        Stocks.TryGetValue(civilizationId, out var stock) ? stock.TotalOre : 0;

    public long GetOre(string civilizationId, OreKind kind) =>
        Stocks.TryGetValue(civilizationId, out var stock) ? stock.GetOre(kind) : 0;

    public long GetComponents(string civilizationId) =>
        Stocks.TryGetValue(civilizationId, out var stock) ? stock.Components : 0;

    public void AddOre(string civilizationId, long amount) =>
        AddOre(civilizationId, OreKind.Copper, amount);

    public void AddOre(string civilizationId, OreKind kind, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        GetOrCreateStock(civilizationId).AddOre(kind, amount);
    }

    public void AddComponents(string civilizationId, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        GetOrCreateStock(civilizationId).Components += amount;
    }

    public bool TryConsumeOre(string civilizationId, long amount) =>
        TryConsumeOre(civilizationId, OreKind.Copper, amount);

    public bool TryConsumeOre(string civilizationId, OreKind kind, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (!Stocks.TryGetValue(civilizationId, out var stock))
        {
            return false;
        }

        return stock.TryConsumeOre(kind, amount);
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

    /// <summary>Raw ore by kind; smelters convert it to material.</summary>
    public long OreCopper { get; internal set; }

    public long OreLead { get; internal set; }

    public long OreTitanium { get; internal set; }

    public long OreCoal { get; internal set; }

    /// <summary>Manufactured components; advanced designs consume them.</summary>
    public long Components { get; internal set; }

    public long TotalOre => OreCopper + OreLead + OreTitanium + OreCoal;

    public long GetOre(OreKind kind) =>
        kind switch
        {
            OreKind.Copper => OreCopper,
            OreKind.Lead => OreLead,
            OreKind.Titanium => OreTitanium,
            OreKind.Coal => OreCoal,
            _ => 0,
        };

    public void AddOre(OreKind kind, long amount)
    {
        switch (kind)
        {
            case OreKind.Copper:
                OreCopper += amount;
                break;
            case OreKind.Lead:
                OreLead += amount;
                break;
            case OreKind.Titanium:
                OreTitanium += amount;
                break;
            case OreKind.Coal:
                OreCoal += amount;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    public bool TryConsumeOre(OreKind kind, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (GetOre(kind) < amount)
        {
            return false;
        }

        AddOre(kind, -amount);
        return true;
    }
}
