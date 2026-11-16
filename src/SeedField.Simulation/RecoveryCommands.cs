namespace SeedField.Simulation;

public abstract record RecoveryCommand(string CivilizationId);

public sealed record BootstrapCoreCommand(
    string CivilizationId,
    RecoveryBlueprint Blueprint,
    Vec2 Position) : RecoveryCommand(CivilizationId);

public sealed record RestoreCoreProductCommand(
    string CivilizationId,
    DeviceBranch Branch,
    Vec2 Position,
    long EnergyCost,
    long MaterialCost) : RecoveryCommand(CivilizationId);

public sealed record RecoveryCommandResult(
    bool Success,
    EntityId? EntityId,
    SimulationErrorCode ErrorCode,
    string Message,
    SimulationEvent? Event);
