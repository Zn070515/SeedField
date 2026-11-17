namespace SeedField.Simulation;

/// <summary>
/// Shell anchors are ordinary damageable entities outside the protected core region.
/// The core remembers the largest anchor count it has ever fielded, so destroying an
/// anchor creates a coverage gap until an equivalent anchor is deployed again.
/// </summary>
public static class CoreShellRules
{
    public static int CountAliveAnchors(WorldState world, string civilizationId)
    {
        ArgumentNullException.ThrowIfNull(world);

        var count = 0;
        foreach (var entity in world.EnumerateEntities())
        {
            if (entity.IsCoreAnchor &&
                string.Equals(entity.CivilizationId, civilizationId, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    public static int CountUncoveredAnchors(WorldState world, SeedCoreState core)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(core);

        return Math.Max(
            0,
            core.AnchorTarget - CountAliveAnchors(world, core.CivilizationId));
    }

    /// <summary>
    /// Called after an anchor is deployed or upgraded. Target only grows: rebuilding a
    /// lost anchor closes the gap without lowering the historical coverage requirement.
    /// </summary>
    public static void RegisterAnchor(WorldState world, EntityState entity)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entity);

        if (!entity.IsCoreAnchor ||
            !world.Cores.TryGetValue(entity.CivilizationId, out var core))
        {
            return;
        }

        core.AnchorTarget = Math.Max(
            core.AnchorTarget,
            CountAliveAnchors(world, entity.CivilizationId));
    }
}
