using Godot;

namespace SeedField.Game;

public partial class MindustryStyle
{
    private readonly Dictionary<string, Texture2D?> _textures = new();

    private Texture2D? Tex(string asset)
    {
        if (!_textures.TryGetValue(asset, out var texture))
        {
            texture = GD.Load<Texture2D>($"res://Art/style_topdown/{asset}_topdown_000.png");
            _textures[asset] = texture;
        }

        return texture;
    }

    private static string OreAsset(Ore ore) =>
        ore switch
        {
            Ore.Copper => "ore_copper",
            Ore.Lead => "ore_lead",
            Ore.Titanium => "ore_titanium",
            Ore.Coal => "ore_coal",
            _ => "ore_copper",
        };

    private static string ZoneAsset(Zone zone) =>
        zone switch
        {
            Zone.Sand => "terrain_sand",
            Zone.Ash => "terrain_ash",
            Zone.Rust => "terrain_rust",
            _ => "terrain_dirt",
        };
}
