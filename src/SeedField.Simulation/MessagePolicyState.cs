namespace SeedField.Simulation;

/// <summary>
/// Inbound message policy for one civilization. Defaults to accepting all cross-civ
/// messages; scenarios can deny senders/topics or close inbound entirely.
/// </summary>
public sealed class MessagePolicyState
{
    internal MessagePolicyState(string civilizationId, bool allowAllInbound)
    {
        CivilizationId = civilizationId;
        AllowAllInbound = allowAllInbound;
    }

    public string CivilizationId { get; }

    public bool AllowAllInbound { get; internal set; }

    public SortedSet<string> BlockedCivilizations { get; } = new(StringComparer.Ordinal);

    public SortedSet<string> BlockedTopics { get; } = new(StringComparer.Ordinal);
}

public static class MessagePolicyRules
{
    public static bool IsAllowed(
        WorldState world,
        string targetCivilizationId,
        string senderCivilizationId,
        string topic)
    {
        if (!world.MessagePolicies.TryGetValue(targetCivilizationId, out var policy))
        {
            return true;
        }

        if (!policy.AllowAllInbound ||
            policy.BlockedCivilizations.Contains(senderCivilizationId) ||
            policy.BlockedTopics.Contains(topic))
        {
            return false;
        }

        return true;
    }
}
