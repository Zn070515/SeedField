namespace SeedField.Simulation;

/// <summary>
/// Core regions are untouchable by civilizations other than their registered owners.
/// This rule is applied at deployment and intent validation, and re-checked on load.
/// </summary>
public static class CoreProtection
{
    public static bool IsProtectedFrom(WorldState world, string regionId, string civilizationId)
    {
        if (!world.Regions.TryGetValue(regionId, out var region) || !region.IsCoreRegion)
        {
            return false;
        }

        foreach (var core in world.Cores.Values)
        {
            if (string.Equals(core.RegionId, regionId, StringComparison.Ordinal) &&
                string.Equals(core.CivilizationId, civilizationId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
