namespace SeedField.Simulation;

public enum IntentKind
{
    Move = 0,
    Attack = 1,
    SendMessage = 2,
    Wait = 3,
}

/// <summary>
/// A request from a controller. Intents never mutate state directly; the simulation
/// core validates each one against the current world.
/// </summary>
public abstract record Intent(EntityId EntityId)
{
    public abstract IntentKind Kind { get; }
}

public sealed record MoveIntent(EntityId EntityId, Vec2 Target) : Intent(EntityId)
{
    public override IntentKind Kind => IntentKind.Move;
}

public sealed record AttackIntent(EntityId EntityId, EntityId TargetId) : Intent(EntityId)
{
    public override IntentKind Kind => IntentKind.Attack;
}

public sealed record SendMessageIntent(
    EntityId EntityId,
    string ToCivilizationId,
    string Topic,
    string Payload,
    string? TargetRegionId = null) : Intent(EntityId)
{
    public override IntentKind Kind => IntentKind.SendMessage;
}

public sealed record WaitIntent(EntityId EntityId) : Intent(EntityId)
{
    public override IntentKind Kind => IntentKind.Wait;
}

/// <summary>Stable error codes surfaced both to diagnostics and to the UI.</summary>
public enum SimulationErrorCode
{
    None = 0,
    UnknownEntity,
    TargetUnknown,
    NoDrive,
    NoWeapon,
    NoComm,
    NoEnergy,
    InsufficientMaterial,
    OutOfRange,
    OutOfBounds,
    WeaponCoolingDown,
    NoRoute,
    NoBandwidth,
    ComputeBudgetExceeded,
    CoreNotAvailable,
    DeploymentRejected,
    InvalidAction,
}

public sealed record IntentResult(
    Intent Intent,
    bool Accepted,
    SimulationErrorCode ErrorCode,
    string Message);

public sealed record TickResult(
    long Tick,
    IReadOnlyList<SimulationEvent> Events,
    IReadOnlyList<IntentResult> IntentResults);

public sealed record BootstrapResult(
    bool Success,
    EntityId? EntityId,
    SimulationErrorCode ErrorCode,
    string Message,
    SimulationEvent? Event);
