namespace SeedField.Simulation;

/// <summary>Host-authorised salvage operations. Scripts use intents, not this path.</summary>
public abstract record WreckCommand(string CivilizationId);

public sealed record RecoverWreckCommand(
    string CivilizationId,
    long WreckId) : WreckCommand(CivilizationId);

/// <summary>Claims a wreck for a civilization with a living entity in its region.</summary>
public sealed record OccupyWreckCommand(
    string CivilizationId,
    long WreckId) : WreckCommand(CivilizationId);

public sealed record WreckCommandResult(
    bool Success,
    SimulationErrorCode ErrorCode,
    string Message,
    IReadOnlyList<SimulationEvent> Events);
