namespace SeedField.Simulation;

/// <summary>Host-authorised breaker operations for one region/civilization.</summary>
public abstract record PowerCommand(string CivilizationId);

public sealed record OpenBreakerCommand(
    string CivilizationId,
    string RegionId) : PowerCommand(CivilizationId);

public sealed record CloseBreakerCommand(
    string CivilizationId,
    string RegionId) : PowerCommand(CivilizationId);

/// <summary>
/// Clears breaker wear. It does not energize the region: the breaker stays open
/// until an explicit close or an automatic reconnect succeeds.
/// </summary>
public sealed record MaintainBreakerCommand(
    string CivilizationId,
    string RegionId) : PowerCommand(CivilizationId);

public sealed record PowerCommandResult(
    bool Success,
    SimulationErrorCode ErrorCode,
    string Message,
    IReadOnlyList<SimulationEvent> Events);
