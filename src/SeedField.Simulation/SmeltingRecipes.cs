namespace SeedField.Simulation;

/// <summary>Secondary output of a smelting recipe.</summary>
public enum SmeltByproduct
{
    None = 0,
    Components = 1,
    Energy = 2,
}

/// <summary>
/// Scenario content, not frozen balance: one batch consumes OrePerBatch of a kind
/// and yields material plus an optional byproduct.
/// </summary>
public sealed record SmeltingRecipe(
    OreKind Kind,
    long OrePerBatch,
    long MaterialPerBatch,
    SmeltByproduct Byproduct = SmeltByproduct.None,
    long ByproductPerBatch = 0);

/// <summary>Default M2 demo recipes. Values are scenario data, not balance.</summary>
public static class SmeltingRecipes
{
    public static IReadOnlyList<SmeltingRecipe> All { get; } =
        new[]
        {
            new SmeltingRecipe(OreKind.Copper, OrePerBatch: 1, MaterialPerBatch: 1),
            new SmeltingRecipe(
                OreKind.Lead,
                OrePerBatch: 2,
                MaterialPerBatch: 1,
                Byproduct: SmeltByproduct.Components,
                ByproductPerBatch: 1),
            new SmeltingRecipe(OreKind.Titanium, OrePerBatch: 2, MaterialPerBatch: 3),
            new SmeltingRecipe(
                OreKind.Coal,
                OrePerBatch: 2,
                MaterialPerBatch: 1,
                Byproduct: SmeltByproduct.Energy,
                ByproductPerBatch: 4),
        };

    public static SmeltingRecipe For(OreKind kind) =>
        All.First(recipe => recipe.Kind == kind);
}
