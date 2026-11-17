namespace SeedField.Simulation;

/// <summary>
/// Host-authorised diplomacy actions. Device scripts can observe treaty state but
/// cannot sign, break or spend stock on behalf of a civilization.
/// </summary>
public abstract record DiplomacyCommand(string CivilizationId);

public sealed record ProposeTreatyCommand(
    string CivilizationId,
    string TargetCivilizationId,
    TreatyKind Kind,
    int ExpiresInTicks) : DiplomacyCommand(CivilizationId);

public sealed record RespondTreatyCommand(
    string CivilizationId,
    long ProposalId,
    bool Accept) : DiplomacyCommand(CivilizationId);

public sealed record BreakTreatyCommand(
    string CivilizationId,
    long TreatyId) : DiplomacyCommand(CivilizationId);

public sealed record ProposeTradeCommand(
    string CivilizationId,
    string TargetCivilizationId,
    string RegionId,
    long GiveMaterial,
    long GiveEnergy,
    long WantMaterial,
    long WantEnergy,
    int ExpiresInTicks) : DiplomacyCommand(CivilizationId);

public sealed record RespondTradeCommand(
    string CivilizationId,
    long TradeOfferId,
    bool Accept) : DiplomacyCommand(CivilizationId);

public sealed record CancelTradeCommand(
    string CivilizationId,
    long TradeOfferId) : DiplomacyCommand(CivilizationId);

public sealed record DiplomacyCommandResult(
    bool Success,
    long? TreatyId,
    long? ProposalId,
    long? TradeOfferId,
    SimulationErrorCode ErrorCode,
    string Message,
    IReadOnlyList<SimulationEvent> Events);
