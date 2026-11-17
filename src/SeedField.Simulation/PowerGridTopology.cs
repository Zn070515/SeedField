namespace SeedField.Simulation;

/// <summary>
/// Regions connected by links with automatic power capacity. Isolated regions are
/// single-region components. Ordering is deterministic: regions by id, links by id.
/// </summary>
public static class PowerGridTopology
{
    public static IReadOnlyList<IReadOnlyList<string>> BuildComponents(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<IReadOnlyList<string>>();

        foreach (var region in world.Regions.Values)
        {
            if (!visited.Add(region.Id))
            {
                continue;
            }

            var component = new List<string>();
            var pending = new Queue<string>();
            pending.Enqueue(region.Id);

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                component.Add(current);

                foreach (var link in world.Links.Values
                             .Where(link =>
                                 link.PowerPerTick > 0 &&
                                 link.Connects(current))
                             .OrderBy(link => link.Id, StringComparer.Ordinal))
                {
                    var next = link.Other(current);
                    if (visited.Add(next))
                    {
                        pending.Enqueue(next);
                    }
                }
            }

            component.Sort(StringComparer.Ordinal);
            components.Add(component);
        }

        return components;
    }
}
