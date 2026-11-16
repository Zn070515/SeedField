namespace SeedField.Simulation;

public abstract record DeploymentCommand(string CivilizationId);

public sealed record DeployCommand(
    string CivilizationId,
    DeviceBlueprint Blueprint,
    string RegionId,
    Vec2 Position) : DeploymentCommand(CivilizationId);

public sealed record UpgradeCommand(
    string CivilizationId,
    EntityId EntityId,
    DeviceBlueprint Blueprint) : DeploymentCommand(CivilizationId);

public sealed record DeploymentCommandResult(
    bool Success,
    EntityId? EntityId,
    SimulationErrorCode ErrorCode,
    string Message,
    IReadOnlyList<SimulationEvent> Events);
