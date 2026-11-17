namespace SeedField.Simulation;

/// <summary>Host-authorised breaker operations for one region/civilization.</summary>
public abstract record PowerCommand(string CivilizationId);

public sealed record OpenBreakerCommand(
    string CivilizationId,
    string RegionId) : PowerCommand(CivilizationId);

public sealed record CloseBreakerCommand(
    string CivilizationId,
    string RegionId) : PowerCommand(CivilizationId);

public sealed record PowerCommandResult(
    bool Success,
    SimulationErrorCode ErrorCode,
    string Message,
    IReadOnlyList<SimulationEvent> Events);
