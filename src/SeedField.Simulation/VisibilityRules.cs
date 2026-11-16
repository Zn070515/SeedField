namespace SeedField.Simulation;

/// <summary>
/// Shared sensor-visibility rule. Intents must not reveal or target entities the
/// issuing civilization has not sensed, even if the caller can guess their id.
/// </summary>
public static class VisibilityRules
{
    public static bool IsSensedBy(EntityState observer, EntityState target)
    {
        var range = observer.Sensor?.RangeMm ?? 0;
        return range > 0 &&
               string.Equals(observer.RegionId, target.RegionId, StringComparison.Ordinal) &&
               observer.Position.IsWithinRange(target.Position, range);
    }

    public static bool IsVisibleTo(WorldState world, string civilizationId, EntityState target)
    {
        if (string.Equals(target.CivilizationId, civilizationId, StringComparison.Ordinal))
        {
            return true;
        }

        if (!world.Regions.TryGetValue(target.RegionId, out var region))
        {
            return false;
        }

        foreach (var own in region.Entities.Values)
        {
            if (string.Equals(own.CivilizationId, civilizationId, StringComparison.Ordinal) &&
                IsSensedBy(own, target))
            {
                return true;
            }
        }

        return false;
    }
}
