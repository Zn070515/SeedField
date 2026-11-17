namespace SeedField.Simulation;

/// <summary>
/// Shell anchors are ordinary damageable entities outside the protected core region.
/// The core remembers the largest anchor count it has ever fielded, so destroying an
/// anchor creates a coverage gap until an equivalent anchor is deployed again.
/// </summary>
public readonly record struct CorePressure(
    int CostPercent,
    int QuotaPenalty,
    int AnchorGap,
    bool IsBesieged);

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

    public static CorePressure EvaluatePressure(WorldState world, SeedCoreState core)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(core);

        var gap = CountUncoveredAnchors(world, core);
        var costPercent = 0;
        if (core.Config.AnchorRecoveryCostPercent > 0)
        {
            costPercent += gap * core.Config.AnchorRecoveryCostPercent;
        }

        var besieged = IsBesieged(world, core);
        var quotaPenalty = 0;
        if (besieged)
        {
            costPercent += core.Config.SiegeRecoveryCostPercent;
            quotaPenalty = core.Config.SiegeQuotaPenalty;
        }

        return new CorePressure(costPercent, quotaPenalty, gap, besieged);
    }

    /// <summary>
    /// A core is besieged while a non-NAP civilization has an entity in a directly
    /// linked region. The protected core region itself never contains foreign entities.
    /// </summary>
    public static bool IsBesieged(WorldState world, SeedCoreState core)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(core);

        foreach (var link in world.Links.Values)
        {
            if (!link.Connects(core.RegionId))
            {
                continue;
            }

            var otherRegionId = link.Other(core.RegionId);
            if (!world.Regions.TryGetValue(otherRegionId, out var region))
            {
                continue;
            }

            foreach (var entity in region.Entities.Values)
            {
                if (string.Equals(
                        entity.CivilizationId,
                        core.CivilizationId,
                        StringComparison.Ordinal) ||
                    HasNonAggression(
                        world,
                        entity.CivilizationId,
                        core.CivilizationId))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private static bool HasNonAggression(
        WorldState world,
        string civilizationA,
        string civilizationB) =>
        world.Treaties.Values.Any(treaty =>
            treaty.Kind == TreatyKind.NonAggression &&
            treaty.Involves(civilizationA) &&
            treaty.Involves(civilizationB));

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
