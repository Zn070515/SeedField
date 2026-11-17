namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    private DiplomacyCommandResult ProposeTreaty(
        ProposeTreatyCommand command,
        List<SimulationEvent> events)
    {
        if (string.IsNullOrWhiteSpace(command.CivilizationId) ||
            string.IsNullOrWhiteSpace(command.TargetCivilizationId) ||
            string.Equals(
                command.CivilizationId,
                command.TargetCivilizationId,
                StringComparison.Ordinal) ||
            command.ExpiresInTicks <= 0)
        {
            return DiplomacyFailure(
                SimulationErrorCode.InvalidAction,
                "Treaty proposal needs two different civilizations and a positive expiry.",
                events);
        }

        if (!HasCommunicationPath(command.CivilizationId, command.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoRoute,
                "No communication path between the civilizations.",
                events);
        }

        if (HasActiveTreaty(
                command.Kind,
                command.CivilizationId,
                command.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TreatyExists,
                "A treaty of this kind is already active.",
                events);
        }

        if (HasPendingProposal(
                command.Kind,
                command.CivilizationId,
                command.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TreatyExists,
                "A proposal of this kind is already pending.",
                events);
        }

        var tick = World.Tick;
        var proposal = new TreatyProposalState(
            World.NextTreatyProposalId++,
            command.Kind,
            command.CivilizationId,
            command.TargetCivilizationId,
            tick,
            tick + command.ExpiresInTicks);
        World.TreatyProposals.Add(proposal.Id, proposal);
        events.Add(new TreatyProposed(
            tick,
            proposal.Id,
            proposal.Kind,
            proposal.ProposerCivilizationId,
            proposal.TargetCivilizationId,
            proposal.ExpiresTick));

        return new DiplomacyCommandResult(
            true,
            null,
            proposal.Id,
            null,
            SimulationErrorCode.None,
            $"Treaty proposal {proposal.Id} created.",
            events);
    }

    private DiplomacyCommandResult RespondTreaty(
        RespondTreatyCommand command,
        List<SimulationEvent> events)
    {
        if (!World.TreatyProposals.TryGetValue(command.ProposalId, out var proposal))
        {
            return DiplomacyFailure(
                SimulationErrorCode.ProposalNotFound,
                "Treaty proposal does not exist.",
                events);
        }

        if (!string.Equals(
                proposal.TargetCivilizationId,
                command.CivilizationId,
                StringComparison.Ordinal))
        {
            return DiplomacyFailure(
                SimulationErrorCode.ProposalNotFound,
                "Treaty proposal does not exist.",
                events);
        }

        if (!HasCommunicationPath(
                proposal.ProposerCivilizationId,
                proposal.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.NoRoute,
                "No communication path between the civilizations.",
                events);
        }

        var tick = World.Tick;

        if (!command.Accept)
        {
            World.TreatyProposals.Remove(proposal.Id);
            events.Add(new TreatyRejected(
                tick,
                proposal.Id,
                proposal.Kind,
                proposal.ProposerCivilizationId,
                proposal.TargetCivilizationId));
            return new DiplomacyCommandResult(
                true,
                null,
                proposal.Id,
                null,
                SimulationErrorCode.None,
                $"Treaty proposal {proposal.Id} rejected.",
                events);
        }

        if (HasActiveTreaty(
                proposal.Kind,
                proposal.ProposerCivilizationId,
                proposal.TargetCivilizationId))
        {
            return DiplomacyFailure(
                SimulationErrorCode.TreatyExists,
                "A treaty of this kind is already active.",
                events);
        }

        World.TreatyProposals.Remove(proposal.Id);
        var (civilizationA, civilizationB) = OrderParties(
            proposal.ProposerCivilizationId,
            proposal.TargetCivilizationId);
        var treaty = new TreatyState(
            World.NextTreatyId++,
            proposal.Kind,
            civilizationA,
            civilizationB,
            proposal.ProposerCivilizationId,
            tick);
        World.Treaties.Add(treaty.Id, treaty);
        events.Add(new TreatyAccepted(
            tick,
            treaty.Id,
            treaty.Kind,
            treaty.CivilizationA,
            treaty.CivilizationB));

        return new DiplomacyCommandResult(
            true,
            treaty.Id,
            proposal.Id,
            null,
            SimulationErrorCode.None,
            $"Treaty {treaty.Id} accepted.",
            events);
    }

    private bool HasActiveTreaty(TreatyKind kind, string civilizationA, string civilizationB) =>
        World.Treaties.Values.Any(treaty =>
            treaty.Kind == kind &&
            treaty.Involves(civilizationA) &&
            treaty.Involves(civilizationB));

    private bool HasPendingProposal(TreatyKind kind, string civilizationA, string civilizationB) =>
        World.TreatyProposals.Values.Any(proposal =>
            proposal.Kind == kind &&
            proposal.Involves(civilizationA) &&
            proposal.Involves(civilizationB));

    private static (string A, string B) OrderParties(string first, string second) =>
        string.CompareOrdinal(first, second) <= 0
            ? (first, second)
            : (second, first);

    /// <summary>
    /// Read-only query used by the host UI: both civilizations need a Comm endpoint and
    /// either a shared region or a link path between their endpoints.
    /// </summary>
    public bool HasCommunicationPath(string civilizationA, string civilizationB)
    {
        foreach (var regionA in World.Regions.Values)
        {
            if (!HasCommEndpoint(regionA.Id, civilizationA))
            {
                continue;
            }

            foreach (var regionB in World.Regions.Values)
            {
                if (!HasCommEndpoint(regionB.Id, civilizationB))
                {
                    continue;
                }

                if (string.Equals(regionA.Id, regionB.Id, StringComparison.Ordinal) ||
                    FindMessagePath(regionA.Id, regionB.Id) is not null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static DiplomacyCommandResult DiplomacyFailure(
        SimulationErrorCode code,
        string message,
        IReadOnlyList<SimulationEvent>? events = null) =>
        new(
            false,
            null,
            null,
            null,
            code,
            message,
            events ?? Array.Empty<SimulationEvent>());

}
