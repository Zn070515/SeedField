namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    private void ProcessSendMessage(
        EntityState entity,
        SendMessageIntent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results,
        Dictionary<EntityId, int> bandwidthUsed,
        Dictionary<string, int> linkUsage)
    {
        if (entity.Comm is null)
        {
            Reject(intent, SimulationErrorCode.NoComm, "Entity has no communications module.", tick, events, results);
            return;
        }

        if (string.IsNullOrWhiteSpace(intent.ToCivilizationId))
        {
            Reject(
                intent,
                SimulationErrorCode.InvalidAction,
                "Message recipient civilization is required.",
                tick,
                events,
                results);
            return;
        }

        if (string.IsNullOrWhiteSpace(intent.Topic))
        {
            Reject(intent, SimulationErrorCode.InvalidAction, "Message topic is required.", tick, events, results);
            return;
        }

        var targetRegionId = string.IsNullOrWhiteSpace(intent.TargetRegionId)
            ? entity.RegionId
            : intent.TargetRegionId;

        if (!World.Regions.ContainsKey(targetRegionId))
        {
            Reject(
                intent,
                SimulationErrorCode.NoRoute,
                $"Target region '{targetRegionId}' does not exist.",
                tick,
                events,
                results);
            return;
        }

        var sameCivilization = string.Equals(
            intent.ToCivilizationId,
            entity.CivilizationId,
            StringComparison.Ordinal);

        if (!sameCivilization &&
            !HasCommEndpoint(targetRegionId, intent.ToCivilizationId))
        {
            Reject(
                intent,
                SimulationErrorCode.NoRoute,
                $"Target civilization has no comm endpoint in region '{targetRegionId}'.",
                tick,
                events,
                results);
            return;
        }

        if (!sameCivilization &&
            !MessagePolicyRules.IsAllowed(
                World,
                intent.ToCivilizationId,
                entity.CivilizationId,
                intent.Topic))
        {
            Reject(
                intent,
                SimulationErrorCode.MessageDenied,
                "Target civilization refuses this sender or topic.",
                tick,
                events,
                results);
            return;
        }

        IReadOnlyList<RegionLink> route = Array.Empty<RegionLink>();
        if (!string.Equals(targetRegionId, entity.RegionId, StringComparison.Ordinal))
        {
            if (sameCivilization && !HasCommEndpoint(targetRegionId, entity.CivilizationId))
            {
                Reject(
                    intent,
                    SimulationErrorCode.NoRoute,
                    $"No own comm endpoint in target region '{targetRegionId}'.",
                    tick,
                    events,
                    results);
                return;
            }

            var path = FindMessagePath(entity.RegionId, targetRegionId);
            if (path is null)
            {
                Reject(
                    intent,
                    SimulationErrorCode.NoRoute,
                    $"No link path from '{entity.RegionId}' to '{targetRegionId}'.",
                    tick,
                    events,
                    results);
                return;
            }

            foreach (var link in path)
            {
                if (link.MessagesPerTick > 0 &&
                    linkUsage.GetValueOrDefault(link.Id) >= link.MessagesPerTick)
                {
                    Reject(
                        intent,
                        SimulationErrorCode.NoBandwidth,
                        $"Link '{link.Id}' bandwidth is exhausted for this tick.",
                        tick,
                        events,
                        results);
                    return;
                }
            }

            route = path;
        }

        var cost = entity.Comm.EnergyCostPerMessage;
        var powerRegion = World.Regions[entity.RegionId];
        if (entity.Energy < cost &&
            !TryAutoDrawForAction(entity, powerRegion, cost, tick, events))
        {
            AddPowerShortfall(
                entity,
                powerRegion.Id,
                cost,
                ActionAvailableEnergy(entity, powerRegion),
                ActionShortfallSource(entity, powerRegion, cost),
                tick,
                events);
            Reject(intent, SimulationErrorCode.NoEnergy, "Not enough energy to send.", tick, events, results);
            return;
        }

        if (entity.Comm.BandwidthPerTick > 0)
        {
            var used = bandwidthUsed.GetValueOrDefault(entity.Id);
            if (used >= entity.Comm.BandwidthPerTick)
            {
                Reject(
                    intent,
                    SimulationErrorCode.NoBandwidth,
                    $"Bandwidth {entity.Comm.BandwidthPerTick} messages per tick is exhausted.",
                    tick,
                    events,
                    results);
                return;
            }

            bandwidthUsed[entity.Id] = used + 1;
        }

        foreach (var link in route)
        {
            if (link.MessagesPerTick > 0)
            {
                linkUsage[link.Id] = linkUsage.GetValueOrDefault(link.Id) + 1;
            }
        }

        entity.Energy -= cost;
        World.Statistics.EnergySpentTotal += cost;

        var messageId = World.NextMessageId++;
        var latency = Math.Max(1, entity.Comm.LatencyTicks + route.Sum(link => link.LatencyTicks));
        var envelope = new MessageEnvelope(
            messageId,
            entity.Id,
            entity.CivilizationId,
            intent.ToCivilizationId,
            targetRegionId,
            intent.Topic,
            intent.Payload ?? string.Empty,
            tick,
            tick + latency);

        World.InFlightMessages.Add(envelope);

        events.Add(new MessageQueued(
            tick,
            messageId,
            entity.Id,
            intent.ToCivilizationId,
            targetRegionId,
            intent.Topic,
            envelope.DeliverTick,
            cost,
            route.Count == 0 ? "local" : string.Join(">", route.Select(link => link.Id))));

        Accept(intent, $"Message {messageId} queued for tick {envelope.DeliverTick}.", results);
    }

    private bool HasCommEndpoint(string regionId, string civilizationId) =>
        World.Regions[regionId].Entities.Values.Any(entity =>
            string.Equals(entity.CivilizationId, civilizationId, StringComparison.Ordinal) &&
            entity.Comm is not null);

    /// <summary>
    /// Bidirectional BFS. Each level is expanded from the smaller frontier so dense
    /// graphs do not blow up one side. Neighbour order is deterministic by link id.
    /// On meeting, the forward half is reversed and the backward half is appended.
    /// </summary>
    private IReadOnlyList<RegionLink>? FindMessagePath(string sourceRegionId, string targetRegionId)
    {
        if (string.Equals(sourceRegionId, targetRegionId, StringComparison.Ordinal))
        {
            return Array.Empty<RegionLink>();
        }

        var forwardVisited = new HashSet<string>(StringComparer.Ordinal) { sourceRegionId };
        var backwardVisited = new HashSet<string>(StringComparer.Ordinal) { targetRegionId };
        var forwardFrontier = new List<string> { sourceRegionId };
        var backwardFrontier = new List<string> { targetRegionId };
        var forwardParents =
            new Dictionary<string, (string Parent, RegionLink Link)>(StringComparer.Ordinal);
        var backwardParents =
            new Dictionary<string, (string Next, RegionLink Link)>(StringComparer.Ordinal);

        while (forwardFrontier.Count > 0 && backwardFrontier.Count > 0)
        {
            var expandForward = forwardFrontier.Count <= backwardFrontier.Count;
            var meeting = expandForward
                ? ExpandFrontier(
                    forwardFrontier,
                    forwardVisited,
                    backwardVisited,
                    forwardParents)
                : ExpandFrontier(
                    backwardFrontier,
                    backwardVisited,
                    forwardVisited,
                    backwardParents);

            if (meeting is not null)
            {
                return BuildPath(
                    meeting,
                    sourceRegionId,
                    targetRegionId,
                    forwardParents,
                    backwardParents);
            }
        }

        return null;
    }

    private string? ExpandFrontier(
        List<string> frontier,
        HashSet<string> ownVisited,
        HashSet<string> otherVisited,
        Dictionary<string, (string Parent, RegionLink Link)> parents)
    {
        var nextFrontier = new List<string>();

        foreach (var regionId in frontier)
        {
            foreach (var link in World.Links.Values
                         .Where(link => link.Connects(regionId))
                         .OrderBy(link => link.Id, StringComparer.Ordinal))
            {
                var next = link.Other(regionId);
                if (!ownVisited.Add(next))
                {
                    continue;
                }

                parents[next] = (regionId, link);

                if (otherVisited.Contains(next))
                {
                    return next;
                }

                nextFrontier.Add(next);
            }
        }

        frontier.Clear();
        frontier.AddRange(nextFrontier);
        return null;
    }

    private static IReadOnlyList<RegionLink> BuildPath(
        string meetingRegionId,
        string sourceRegionId,
        string targetRegionId,
        Dictionary<string, (string Parent, RegionLink Link)> forwardParents,
        Dictionary<string, (string Next, RegionLink Link)> backwardParents)
    {
        var path = new List<RegionLink>();

        var current = meetingRegionId;
        while (!string.Equals(current, sourceRegionId, StringComparison.Ordinal))
        {
            var parent = forwardParents[current];
            path.Add(parent.Link);
            current = parent.Parent;
        }

        path.Reverse();

        current = meetingRegionId;
        while (!string.Equals(current, targetRegionId, StringComparison.Ordinal))
        {
            var next = backwardParents[current];
            path.Add(next.Link);
            current = next.Next;
        }

        return path;
    }

    private void DeliverDueMessages(long tick, List<SimulationEvent> events)
    {
        if (World.InFlightMessages.Count == 0)
        {
            return;
        }

        var due = World.InFlightMessages
            .Where(message => message.DeliverTick <= tick)
            .OrderBy(message => message.DeliverTick)
            .ThenBy(message => message.Id)
            .ToList();

        // Storage is a property of the civilization's entities, not of each
        // message. Cache it per delivery batch; scanning the whole world per
        // message made 1000 local messages O(messages × entities).
        var storageByCivilization =
            new Dictionary<string, (bool HasStorage, int Capacity)>(StringComparer.Ordinal);
        foreach (var message in due)
        {
            World.InFlightMessages.Remove(message);

            if (!World.Inboxes.TryGetValue(message.ToCivilizationId, out var inbox))
            {
                inbox = new List<DeliveredMessage>();
                World.Inboxes.Add(message.ToCivilizationId, inbox);
            }

            if (!storageByCivilization.TryGetValue(
                    message.ToCivilizationId,
                    out var storage))
            {
                storage = GetMessageStorage(message.ToCivilizationId);
                storageByCivilization[message.ToCivilizationId] = storage;
            }

            var (hasStorage, capacity) = storage;
            if (hasStorage)
            {
                if (capacity <= 0)
                {
                    World.Statistics.MessagesExpired += 1;
                    events.Add(new MessageExpired(
                        tick,
                        message.Id,
                        message.ToCivilizationId,
                        "storage capacity is zero"));
                    continue;
                }

                while (inbox.Count >= capacity)
                {
                    var expired = inbox[0];
                    inbox.RemoveAt(0);
                    World.Statistics.MessagesExpired += 1;
                    events.Add(new MessageExpired(
                        tick,
                        expired.Id,
                        message.ToCivilizationId,
                        "inbox storage full"));
                }
            }

            inbox.Add(new DeliveredMessage(
                message.Id,
                message.FromEntityId,
                message.FromCivilizationId,
                message.TargetRegionId,
                message.Topic,
                message.Payload,
                message.SentTick,
                tick));

            World.Statistics.MessagesDelivered += 1;
            events.Add(new MessageDelivered(
                tick,
                message.Id,
                message.FromEntityId,
                message.ToCivilizationId,
                message.TargetRegionId,
                message.Topic));
        }
    }

    /// <summary>
    /// Storage S is the sum of own State module capacities. No State module means the
    /// legacy unlimited inbox; a zero-capacity module retains no history.
    /// </summary>
    private (bool HasStorage, int Capacity) GetMessageStorage(string civilizationId)
    {
        var hasStorage = false;
        var capacity = 0;

        foreach (var entity in World.EnumerateEntities())
        {
            if (!string.Equals(entity.CivilizationId, civilizationId, StringComparison.Ordinal) ||
                entity.State is null)
            {
                continue;
            }

            hasStorage = true;
            capacity += Math.Max(0, entity.State.StorageCapacity);
        }

        return (hasStorage, capacity);
    }

}
