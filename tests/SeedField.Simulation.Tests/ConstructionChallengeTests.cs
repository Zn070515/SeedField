using SeedField.Simulation;

namespace SeedField.Simulation.Tests;

/// <summary>
/// M1 challenge catalog: branch challenges come from tutorial facts; capstones use
/// explicit AND/OR prerequisites. None of them gate PvE admission.
/// </summary>
public sealed class ConstructionChallengeTests
{
    [Fact]
    public void EnergyAloneDoesNotCompleteNetworkOrFieldChallenges()
    {
        var progress = Progress(TutorialStep.Energy);

        var statuses = ConstructionChallenges.Evaluate(progress);

        Assert.True(Completed(statuses, "branch.energy"));
        Assert.False(Completed(statuses, "network.minimum"));
        Assert.False(Completed(statuses, "field.minimum"));
    }

    [Fact]
    public void NetworkChallengeRequiresAllThreeBranches()
    {
        var two = ConstructionChallenges.Evaluate(
            Progress(TutorialStep.Energy, TutorialStep.Communication));
        var three = ConstructionChallenges.Evaluate(
            Progress(
                TutorialStep.Energy,
                TutorialStep.Communication,
                TutorialStep.Execution));

        Assert.False(Completed(two, "network.minimum"));
        Assert.True(Completed(three, "network.minimum"));
    }

    [Fact]
    public void FieldChallengeAcceptsEitherBranch()
    {
        var sensing = ConstructionChallenges.Evaluate(Progress(TutorialStep.Sensing));
        var communication = ConstructionChallenges.Evaluate(
            Progress(TutorialStep.Communication));

        Assert.True(Completed(sensing, "field.minimum"));
        Assert.True(Completed(communication, "field.minimum"));
    }

    [Fact]
    public void AllFiveBranchesCompleteEveryChallenge()
    {
        var progress = Progress(
            TutorialStep.Energy,
            TutorialStep.Sensing,
            TutorialStep.Communication,
            TutorialStep.State,
            TutorialStep.Execution);

        var statuses = ConstructionChallenges.Evaluate(progress);

        Assert.All(statuses, status => Assert.True(status.Completed, status.Challenge.Id));
        Assert.True(progress.PveUnlocked);
    }

    [Fact]
    public void StatusesAreStableAndUnique()
    {
        var progress = Progress(TutorialStep.Energy);
        var first = ConstructionChallenges.Evaluate(progress);
        var second = ConstructionChallenges.Evaluate(progress);

        Assert.Equal(
            first.Select(status => status.Challenge.Id),
            second.Select(status => status.Challenge.Id));
        Assert.Equal(
            first.Select(status => status.Challenge.Id).Distinct().Count(),
            first.Count);
    }

    [Fact]
    public void ChallengeCatalogDoesNotChangeTutorialAdmission()
    {
        var progress = Progress(TutorialStep.Energy);

        ConstructionChallenges.Evaluate(progress);

        Assert.False(progress.PveUnlocked);
        Assert.Equal(1, progress.CompletedCount);
    }

    private static bool Completed(
        IReadOnlyList<ChallengeStatus> statuses,
        string challengeId) =>
        statuses.Single(status => status.Challenge.Id == challengeId).Completed;

    private static TutorialProgress Progress(params TutorialStep[] steps)
    {
        var completed = steps.ToDictionary(step => step, _ => 0L);
        return TutorialProgress.Restore("A", completed);
    }
}
