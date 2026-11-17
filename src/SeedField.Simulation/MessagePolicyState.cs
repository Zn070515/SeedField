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

    public bool AllowAllTopics { get; internal set; } = true;

    public SortedSet<string> BlockedCivilizations { get; } = new(StringComparer.Ordinal);

    public SortedSet<string> AllowedCivilizations { get; } = new(StringComparer.Ordinal);

    public SortedSet<string> BlockedTopics { get; } = new(StringComparer.Ordinal);

    public SortedSet<string> AllowedTopics { get; } = new(StringComparer.Ordinal);
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

        if (policy.BlockedCivilizations.Contains(senderCivilizationId))
        {
            return false;
        }

        if (!policy.AllowAllInbound &&
            !policy.AllowedCivilizations.Contains(senderCivilizationId))
        {
            return false;
        }

        if (policy.BlockedTopics.Contains(topic))
        {
            return false;
        }

        if (!policy.AllowAllTopics &&
            !policy.AllowedTopics.Contains(topic))
        {
            return false;
        }

        return true;
    }
}
