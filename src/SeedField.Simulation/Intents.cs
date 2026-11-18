namespace SeedField.Simulation;

public enum IntentKind
{
    Move = 0,
    Attack = 1,
    SendMessage = 2,
    Wait = 3,
    TransferMaterial = 4,
    SendEnergy = 5,
    DrawEnergy = 6,
    Mine = 7,
    Production = 8,
}

/// <summary>
/// A request from a controller. Intents never mutate state directly; the simulation
/// core validates each one against the current world.
/// </summary>
public abstract record Intent(EntityId EntityId)
{
    public abstract IntentKind Kind { get; }

    /// <summary>
    /// Issuer stamped by the controller runtime. Untrusted controllers always set it;
    /// the engine refuses intents that target another civilization's entity.
    /// Trusted host/test callers may leave it null.
    /// </summary>
    public string? IssuerCivilizationId { get; init; }
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

/// <summary>
/// Ships material from the sender's region stock to an adjacent region. The shipment
/// spends one direct link's latency in transit before it is usable at the destination.
/// </summary>
public sealed record TransferMaterialIntent(
    EntityId EntityId,
    string TargetRegionId,
    long Amount) : Intent(EntityId)
{
    public override IntentKind Kind => IntentKind.TransferMaterial;
}

/// <summary>
/// Moves energy from the entity's local storage into a regional grid. Omitting the
/// target stores into the entity's own region immediately; a remote target uses links.
/// </summary>
public sealed record SendEnergyIntent(
    EntityId EntityId,
    long Amount,
    string? TargetRegionId = null) : Intent(EntityId)
{
    public override IntentKind Kind => IntentKind.SendEnergy;
}

/// <summary>Draws energy from the entity's own regional grid into local storage.</summary>
public sealed record DrawEnergyIntent(
    EntityId EntityId,
    long Amount) : Intent(EntityId)
{
    public override IntentKind Kind => IntentKind.DrawEnergy;
}

/// <summary>
/// Mines the nearest non-depleted deposit inside the drill's range. The target is
/// resolved deterministically by squared distance, then deposit id.
/// </summary>
public sealed record MineIntent(EntityId EntityId) : Intent(EntityId)
{
    public override IntentKind Kind => IntentKind.Mine;
}

/// <summary>Enables or pauses passive smelting/fabrication on this device.</summary>
public sealed record SetProductionIntent(EntityId EntityId, bool Enabled) : Intent(EntityId)
{
    public override IntentKind Kind => IntentKind.Production;
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
    NoPower,
    NoEnergy,
    InsufficientMaterial,
    OutOfRange,
    OutOfBounds,
    WeaponCoolingDown,
    NoRoute,
    NoBandwidth,
    MessageDenied,
    RegionInactive,
    CoreProtected,
    ComputeBudgetExceeded,
    CoreNotAvailable,
    DeploymentRejected,
    InvalidAction,
    TreatyDenied,
    TreatyExists,
    TreatyNotFound,
    ProposalNotFound,
    TradeNotFound,
    NegotiationExhausted,
    GridFull,
    BreakerMaintenanceRequired,
    RecoveryQuotaExhausted,
    WreckNotFound,
    WreckUnavailable,
    RuinImportRejected,
    NoDrill,
    DepositOutOfRange,
    DepositDepleted,
    ModuleDisabled,
    InsufficientComponents,
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
