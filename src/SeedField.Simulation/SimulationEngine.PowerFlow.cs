namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    private PowerGridState GetOrCreatePowerGridState(
        string regionId,
        string civilizationId)
    {
        var key = (regionId, civilizationId);
        if (!World.PowerGridStates.TryGetValue(key, out var state))
        {
            state = new PowerGridState(regionId, civilizationId);
            World.PowerGridStates.Add(key, state);
        }

        return state;
    }

    private bool AutoReconnect(string civilizationId) =>
        !World.PowerPolicies.TryGetValue(civilizationId, out var policy) ||
        policy.AutoReconnect;

    private bool IsBreakerOpen(string regionId, string civilizationId) =>
        World.PowerGridStates.TryGetValue(
            (regionId, civilizationId),
            out var state) &&
        state.BreakerOpen;

    private void FlowPower(long tick, List<SimulationEvent> events)
    {
        if (World.Links.Values.All(link => link.PowerPerTick <= 0))
        {
            return;
        }

        var components = PowerGridTopology.BuildComponents(World);
        var civilizations = CollectPowerCivilizations();
        var linkUsage = new Dictionary<string, int>(StringComparer.Ordinal);
        var flowAmounts =
            new Dictionary<(string Civ, string From, string To), long>();

        foreach (var civilizationId in civilizations)
        {
            foreach (var component in components)
            {
                if (component.Count < 2)
                {
                    continue;
                }

                FlowPowerComponent(
                    civilizationId,
                    component,
                    tick,
                    linkUsage,
                    flowAmounts,
                    events);
            }
        }

        foreach (var pair in flowAmounts
                     .OrderBy(pair => pair.Key.Civ, StringComparer.Ordinal)
                     .ThenBy(pair => pair.Key.From, StringComparer.Ordinal)
                     .ThenBy(pair => pair.Key.To, StringComparer.Ordinal))
        {
            events.Add(new PowerFlowed(
                tick,
                pair.Key.Civ,
                pair.Key.From,
                pair.Key.To,
                pair.Value));
        }
    }

    private void FlowPowerComponent(
        string civilizationId,
        IReadOnlyList<string> component,
        long tick,
        Dictionary<string, int> linkUsage,
        Dictionary<(string Civ, string From, string To), long> flowAmounts,
        List<SimulationEvent> events)
    {
        var componentSet = new HashSet<string>(component, StringComparer.Ordinal);
        var incoming = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var shipment in World.InTransitPower)
        {
            if (!string.Equals(
                    shipment.CivilizationId,
                    civilizationId,
                    StringComparison.Ordinal) ||
                shipment.ArriveTick <= tick ||
                !componentSet.Contains(shipment.ToRegionId))
            {
                continue;
            }

            incoming[shipment.ToRegionId] =
                incoming.GetValueOrDefault(shipment.ToRegionId) +
                shipment.DeliveredAmount;
        }

        var projected = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var regionId in component)
        {
            projected[regionId] =
                World.Regions[regionId].GetEnergy(civilizationId) +
                incoming.GetValueOrDefault(regionId);
        }

        var total = projected.Values.Sum();
        if (total <= 0)
        {
            return;
        }

        var target = total / component.Count;
        var remainder = (int)(total % component.Count);
        var desired = new Dictionary<string, long>(StringComparer.Ordinal);
        var ordered = component
            .OrderBy(regionId => projected[regionId])
            .ThenBy(regionId => regionId, StringComparer.Ordinal)
            .ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            desired[ordered[index]] = target + (index < remainder ? 1 : 0);
        }

        var distances = ComputePowerDistances(
            civilizationId,
            componentSet,
            desired,
            linkUsage);
        var deficits = component
            .Where(regionId =>
                projected[regionId] <
                Math.Min(
                    desired[regionId],
                    World.Regions[regionId].GetGridCapacity(civilizationId)))
            .OrderBy(regionId =>
                distances.TryGetValue(regionId, out var distance)
                    ? distance
                    : int.MaxValue)
            .ThenBy(regionId => regionId, StringComparer.Ordinal)
            .ToList();

        foreach (var deficitRegionId in deficits)
        {
            while (true)
            {
                var projectedStock =
                    World.Regions[deficitRegionId].GetEnergy(civilizationId) +
                    incoming.GetValueOrDefault(deficitRegionId);
                var targetStock = Math.Min(
                    desired[deficitRegionId],
                    World.Regions[deficitRegionId].GetGridCapacity(civilizationId));
                var deficit = targetStock - projectedStock;
                if (deficit <= 0)
                {
                    break;
                }

                var path = FindPowerPath(
                    civilizationId,
                    deficitRegionId,
                    componentSet,
                    desired,
                    linkUsage);
                if (path is null)
                {
                    break;
                }

                var sourceEnergy =
                    World.Regions[path.SourceRegionId].GetEnergy(civilizationId);
                var sourceSurplus = sourceEnergy - desired[path.SourceRegionId];
                var pathCapacity = path.Links.Min(link =>
                    link.PowerPerTick - linkUsage.GetValueOrDefault(link.Id));
                var grossNeeded = ComputeGrossPowerForNet(deficit, path.Links);
                if (grossNeeded is null)
                {
                    break;
                }

                var amount = Math.Min(
                    Math.Min(sourceSurplus, grossNeeded.Value),
                    pathCapacity);
                if (amount <= 0)
                {
                    break;
                }

                var delivered = ApplyPowerLoss(amount, path.Links);
                if (delivered <= 0)
                {
                    break;
                }

                var lost = amount - delivered;
                if (!World.Regions[path.SourceRegionId]
                        .TryConsumeEnergy(civilizationId, amount))
                {
                    break;
                }

                foreach (var link in path.Links)
                {
                    linkUsage[link.Id] =
                        linkUsage.GetValueOrDefault(link.Id) + (int)amount;
                }

                var latency = path.Links.Sum(link => link.PowerLatencyTicks);
                if (latency <= 0)
                {
                    World.Regions[deficitRegionId].AddEnergy(
                        civilizationId,
                        delivered);
                    World.Statistics.EnergyFlowedTotal += delivered;
                    World.Statistics.EnergyFlowLossTotal += lost;

                    if (delivered > 0)
                    {
                        var key = (
                            civilizationId,
                            path.SourceRegionId,
                            deficitRegionId);
                        flowAmounts[key] =
                            flowAmounts.GetValueOrDefault(key) + delivered;
                    }
                }
                else
                {
                    var shipmentId = World.NextPowerShipmentId++;
                    var arriveTick = tick + latency;
                    var route = string.Join(
                        ">",
                        path.Links.Select(link => link.Id));
                    World.InTransitPower.Add(new PowerShipment(
                        shipmentId,
                        civilizationId,
                        path.SourceRegionId,
                        deficitRegionId,
                        route,
                        amount,
                        delivered,
                        tick,
                        arriveTick));
                    incoming[deficitRegionId] =
                        incoming.GetValueOrDefault(deficitRegionId) + delivered;
                    events.Add(new PowerShipped(
                        tick,
                        shipmentId,
                        civilizationId,
                        path.SourceRegionId,
                        deficitRegionId,
                        amount,
                        delivered,
                        arriveTick,
                        route));
                }
            }
        }
    }

    private static long? ComputeGrossPowerForNet(
        long net,
        IReadOnlyList<RegionLink> links)
    {
        if (net <= 0)
        {
            return 0;
        }

        var gross = (Int128)net;
        for (var index = links.Count - 1; index >= 0; index--)
        {
            var loss = links[index].PowerLossPercent;
            if (loss >= 100)
            {
                return null;
            }

            if (loss <= 0)
            {
                continue;
            }

            var denominator = 100 - loss;
            gross = (gross * 100 + denominator - 1) / denominator;
            if (gross > long.MaxValue)
            {
                return null;
            }
        }

        return (long)gross;
    }

    private static long ApplyPowerLoss(
        long amount,
        IReadOnlyList<RegionLink> links)
    {
        var delivered = (Int128)amount;
        foreach (var link in links)
        {
            delivered = delivered * (100 - link.PowerLossPercent) / 100;
        }

        return delivered > long.MaxValue ? long.MaxValue : (long)delivered;
    }

    private Dictionary<string, int> ComputePowerDistances(
        string civilizationId,
        HashSet<string> componentSet,
        IReadOnlyDictionary<string, long> desired,
        Dictionary<string, int> linkUsage)
    {
        var distances = new Dictionary<string, int>(StringComparer.Ordinal);
        var pending = new Queue<string>();

        foreach (var regionId in componentSet.OrderBy(regionId => regionId, StringComparer.Ordinal))
        {
            if (World.Regions[regionId].GetEnergy(civilizationId) > desired[regionId])
            {
                distances[regionId] = 0;
                pending.Enqueue(regionId);
            }
        }

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            var nextDistance = distances[current] + 1;

            foreach (var link in PowerLinks(current, componentSet, linkUsage))
            {
                var next = link.Other(current);
                if (distances.ContainsKey(next))
                {
                    continue;
                }

                distances[next] = nextDistance;
                pending.Enqueue(next);
            }
        }

        return distances;
    }

    private PowerPath? FindPowerPath(
        string civilizationId,
        string deficitRegionId,
        HashSet<string> componentSet,
        IReadOnlyDictionary<string, long> desired,
        Dictionary<string, int> linkUsage)
    {
        var parents = new Dictionary<string, (string Previous, RegionLink Link)>(
            StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal) { deficitRegionId };
        var pending = new Queue<string>();
        pending.Enqueue(deficitRegionId);

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();

            if (!string.Equals(current, deficitRegionId, StringComparison.Ordinal) &&
                World.Regions[current].GetEnergy(civilizationId) > desired[current])
            {
                var links = new List<RegionLink>();
                var node = current;
                while (parents.TryGetValue(node, out var parent))
                {
                    links.Add(parent.Link);
                    node = parent.Previous;
                }

                links.Reverse();
                return new PowerPath(current, links);
            }

            foreach (var link in PowerLinks(current, componentSet, linkUsage))
            {
                var next = link.Other(current);
                if (!visited.Add(next))
                {
                    continue;
                }

                parents[next] = (current, link);
                pending.Enqueue(next);
            }
        }

        return null;
    }

    private IEnumerable<RegionLink> PowerLinks(
        string regionId,
        HashSet<string> componentSet,
        Dictionary<string, int> linkUsage) =>
        World.Links.Values
            .Where(link =>
                link.PowerPerTick > 0 &&
                link.PowerLossPercent < 100 &&
                link.Connects(regionId) &&
                componentSet.Contains(link.Other(regionId)) &&
                link.PowerPerTick - linkUsage.GetValueOrDefault(link.Id) > 0)
            .OrderBy(link => link.Id, StringComparer.Ordinal)
            .ThenBy(link => link.Other(regionId), StringComparer.Ordinal);

    private IReadOnlyList<string> CollectPowerCivilizations()
    {
        var civilizations = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var region in World.Regions.Values)
        {
            foreach (var civilizationId in region.Stocks.Keys)
            {
                civilizations.Add(civilizationId);
            }

            foreach (var entity in region.Entities.Values)
            {
                civilizations.Add(entity.CivilizationId);
            }
        }

        return civilizations.ToList();
    }

    private sealed record PowerPath(
        string SourceRegionId,
        IReadOnlyList<RegionLink> Links);

}
