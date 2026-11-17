namespace SeedField.Simulation;

/// <summary>Host-authorised offline ruin import. Snapshot content is data-only.</summary>
public abstract record RuinCommand(string CivilizationId);

public sealed record ImportRuinCommand(
    string CivilizationId,
    string RegionId,
    string SnapshotJson,
    int CenterX,
    int CenterY,
    int RadiusMm,
    int AgingPercent,
    int LootPercent) : RuinCommand(CivilizationId);

public sealed record RuinCommandResult(
    bool Success,
    SimulationErrorCode ErrorCode,
    string Message,
    IReadOnlyList<SimulationEvent> Events);
