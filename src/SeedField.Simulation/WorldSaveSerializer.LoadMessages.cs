namespace SeedField.Simulation;

public static partial class WorldSaveSerializer
{
    private static void LoadMessages(WorldState world, WorldSaveData data)
    {
        var maxMessageId = 0L;
        var seenMessageIds = new HashSet<long>();
        foreach (var message in data.InFlightMessages)
        {
            if (message.Id <= 0 || message.DeliverTick <= message.SentTick)
            {
                throw Invalid("INVALID_MESSAGE", $"Message '{message.Id}' has invalid timing.");
            }

            if (!seenMessageIds.Add(message.Id))
            {
                throw Invalid(
                    "DUPLICATE_MESSAGE_ID",
                    $"Message id '{message.Id}' appears more than once.");
            }

            var targetRegionId = ResolveTargetRegionId(
                world,
                message.TargetRegionId,
                message.FromEntityId);
            var envelope = new MessageEnvelope(
                message.Id,
                new EntityId(message.FromEntityId),
                message.FromCivilizationId,
                message.ToCivilizationId,
                targetRegionId,
                message.Topic,
                message.Payload,
                message.SentTick,
                message.DeliverTick);
            world.InFlightMessages.Add(envelope);
            maxMessageId = Math.Max(maxMessageId, message.Id);
        }

        foreach (var inboxData in data.Inboxes)
        {
            if (world.Inboxes.ContainsKey(inboxData.CivilizationId))
            {
                throw Invalid(
                    "DUPLICATE_INBOX",
                    $"Inbox '{inboxData.CivilizationId}' appears twice.");
            }

            var inbox = new List<DeliveredMessage>();
            foreach (var message in inboxData.Messages)
            {
                if (message.Id <= 0 || message.DeliveredTick < message.SentTick)
                {
                    throw Invalid(
                        "INVALID_DELIVERED_MESSAGE",
                        $"Delivered message '{message.Id}' has invalid timing.");
                }

                if (!seenMessageIds.Add(message.Id))
                {
                    throw Invalid(
                        "DUPLICATE_MESSAGE_ID",
                        $"Message id '{message.Id}' appears more than once.");
                }

                var targetRegionId = ResolveTargetRegionId(
                    world,
                    message.TargetRegionId,
                    message.FromEntityId);
                inbox.Add(new DeliveredMessage(
                    message.Id,
                    new EntityId(message.FromEntityId),
                    message.FromCivilizationId,
                    targetRegionId,
                    message.Topic,
                    message.Payload,
                    message.SentTick,
                    message.DeliveredTick));
                maxMessageId = Math.Max(maxMessageId, message.Id);
            }

            world.Inboxes.Add(inboxData.CivilizationId, inbox);
        }

        if (world.NextMessageId <= maxMessageId)
        {
            throw Invalid("INVALID_NEXT_MESSAGE_ID", "NextMessageId must exceed every stored message id.");
        }
    }

    private static void LoadMessagePolicies(WorldState world, WorldSaveData data)
    {
        foreach (var policyData in data.MessagePolicies ?? Array.Empty<MessagePolicySaveData>())
        {
            if (string.IsNullOrWhiteSpace(policyData.CivilizationId) ||
                world.MessagePolicies.ContainsKey(policyData.CivilizationId))
            {
                throw Invalid("INVALID_MESSAGE_POLICY", "Message policy ids must be unique and non-empty.");
            }

            var policy = new MessagePolicyState(
                policyData.CivilizationId,
                policyData.AllowAllInbound)
            {
                AllowAllTopics = policyData.AllowAllTopics,
            };

            foreach (var blocked in policyData.BlockedCivilizations)
            {
                policy.BlockedCivilizations.Add(blocked);
            }

            foreach (var allowed in policyData.AllowedCivilizations ?? Array.Empty<string>())
            {
                policy.AllowedCivilizations.Add(allowed);
            }

            foreach (var blocked in policyData.BlockedTopics)
            {
                policy.BlockedTopics.Add(blocked);
            }

            foreach (var allowed in policyData.AllowedTopics ?? Array.Empty<string>())
            {
                policy.AllowedTopics.Add(allowed);
            }

            world.MessagePolicies.Add(policyData.CivilizationId, policy);
        }
    }
}
