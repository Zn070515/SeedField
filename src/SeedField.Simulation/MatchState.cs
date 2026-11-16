namespace SeedField.Simulation;

public sealed record MatchRules(int CapturedPointsToWin)
{
    public void Validate()
    {
        if (CapturedPointsToWin <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CapturedPointsToWin),
                "At least one captured point is required to win.");
        }
    }
}

/// <summary>
/// Optional scenario match state. Points keep simulating after a winner exists so the
/// world can continue as a sandbox; the match result itself is frozen.
/// </summary>
public sealed class MatchState
{
    internal MatchState(MatchRules rules)
    {
        rules.Validate();
        Rules = rules;
    }

    public MatchRules Rules { get; }

    public string? WinnerCivilizationId { get; internal set; }

    public bool IsFinished => WinnerCivilizationId is not null;
}
