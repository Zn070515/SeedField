namespace SeedField.Simulation;

/// <summary>How a capstone challenge combines its prerequisite challenges.</summary>
public enum ChallengeGate
{
    None = 0,
    All = 1,
    Any = 2,
}

/// <summary>
/// One M1 construction challenge. Branch challenges map to a tutorial step; capstones
/// combine completed challenges with an explicit AND/OR gate.
/// </summary>
public sealed record ConstructionChallenge(
    string Id,
    string Title,
    string Description,
    TutorialStep? Step = null,
    ChallengeGate Gate = ChallengeGate.None,
    IReadOnlyList<string>? Requires = null);

/// <summary>Read-only evaluation result. Challenges never change world state.</summary>
public sealed record ChallengeStatus(
    ConstructionChallenge Challenge,
    bool Completed,
    bool Available,
    string Detail);

/// <summary>
/// First M1 challenge set. Capstones only describe later construction capabilities;
/// they never gate PvE admission, which stays derived from <see cref="TutorialProgress"/>.
/// </summary>
public static class ConstructionChallenges
{
    public static IReadOnlyList<ConstructionChallenge> All { get; } =
        new[]
        {
            new ConstructionChallenge(
                "branch.energy",
                "Minimum Energy",
                "Generate and store energy on a deployed design.",
                TutorialStep.Energy),
            new ConstructionChallenge(
                "branch.sensing",
                "Minimum Sensing",
                "Sense a foreign entity inside a sensor's range.",
                TutorialStep.Sensing),
            new ConstructionChallenge(
                "branch.communication",
                "Minimum Communication",
                "Deliver a message to your civilization.",
                TutorialStep.Communication),
            new ConstructionChallenge(
                "branch.state",
                "Minimum State",
                "Have a state-capable device act on its program.",
                TutorialStep.State),
            new ConstructionChallenge(
                "branch.execution",
                "Minimum Execution",
                "Move or fire a deployed device.",
                TutorialStep.Execution),
            new ConstructionChallenge(
                "network.minimum",
                "Minimum Network",
                "Verify energy, communication and execution (AND).",
                Gate: ChallengeGate.All,
                Requires: new[]
                {
                    "branch.energy",
                    "branch.communication",
                    "branch.execution",
                }),
            new ConstructionChallenge(
                "field.minimum",
                "Field Awareness",
                "Verify sensing or communication (OR).",
                Gate: ChallengeGate.Any,
                Requires: new[] { "branch.sensing", "branch.communication" }),
        };

    public static IReadOnlyList<ChallengeStatus> Evaluate(TutorialProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var completed = new HashSet<string>(StringComparer.Ordinal);
        var statuses = new List<ChallengeStatus>(All.Count);

        foreach (var challenge in All)
        {
            if (challenge.Step is not { } step)
            {
                continue;
            }

            var done = progress.HasCompleted(step);
            if (done)
            {
                completed.Add(challenge.Id);
            }

            statuses.Add(new ChallengeStatus(
                challenge,
                done,
                Available: true,
                done
                    ? $"verified at tick {progress.CompletionTick(step)}"
                    : "not verified"));
        }

        foreach (var challenge in All)
        {
            if (challenge.Step is not null)
            {
                continue;
            }

            var requires = challenge.Requires ?? Array.Empty<string>();
            if (requires.Count == 0 || challenge.Gate == ChallengeGate.None)
            {
                throw new InvalidOperationException(
                    $"Challenge '{challenge.Id}' needs a gate and prerequisites.");
            }

            foreach (var requiredId in requires)
            {
                if (!All.Any(candidate =>
                        candidate.Step is not null &&
                        string.Equals(candidate.Id, requiredId, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        $"Challenge '{challenge.Id}' requires unknown branch '{requiredId}'.");
                }
            }

            var met = challenge.Gate == ChallengeGate.All
                ? requires.All(completed.Contains)
                : requires.Any(completed.Contains);
            if (met)
            {
                completed.Add(challenge.Id);
            }

            statuses.Add(new ChallengeStatus(
                challenge,
                Completed: met,
                Available: met,
                Detail: challenge.Gate == ChallengeGate.All
                    ? "requires " + string.Join(" + ", requires)
                    : "requires " + string.Join(" or ", requires)));
        }

        return statuses;
    }
}
