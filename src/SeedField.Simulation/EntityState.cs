namespace SeedField.Simulation;

/// <summary>
/// Immutable creation data for an entity. Used by the world builder and by core-region
/// recovery; runtime mutable state lives in <see cref="EntityState"/>.
/// </summary>
public sealed record EntityDefinition(
    EntityId Id,
    string CivilizationId,
    string RegionId,
    Vec2 Position,
    int MaxHealth,
    long Energy,
    long EnergyCapacity,
    long Material,
    SensorModule? Sensor = null,
    WeaponModule? Weapon = null,
    DriveModule? Drive = null,
    CommModule? Comm = null,
    string? BlueprintId = null,
    int BlueprintVersion = 0,
    string? ControllerProgramId = null,
    PowerModule? Power = null,
    StateModule? State = null,
    bool IsCoreAnchor = false);

/// <summary>
/// Authoritative runtime state for one deployed entity. Only the simulation core may
/// mutate it; controllers observe filtered snapshots.
/// </summary>
public sealed class EntityState
{
    internal EntityState(EntityDefinition definition)
    {
        Id = definition.Id;
        CivilizationId = definition.CivilizationId;
        RegionId = definition.RegionId;
        Position = definition.Position;
        MaxHealth = definition.MaxHealth;
        Health = definition.MaxHealth;
        Energy = definition.Energy;
        EnergyCapacity = definition.EnergyCapacity;
        Material = definition.Material;
        Sensor = definition.Sensor;
        Weapon = definition.Weapon;
        Drive = definition.Drive;
        Comm = definition.Comm;
        BlueprintId = definition.BlueprintId;
        BlueprintVersion = definition.BlueprintVersion;
        ControllerProgramId = definition.ControllerProgramId;
        Power = definition.Power;
        State = definition.State;
        IsCoreAnchor = definition.IsCoreAnchor;
    }

    public EntityId Id { get; }

    public string CivilizationId { get; }

    public string RegionId { get; }

    public Vec2 Position { get; internal set; }

    public int MaxHealth { get; internal set; }

    public int Health { get; internal set; }

    public long Energy { get; internal set; }

    public long EnergyCapacity { get; internal set; }

    public long Material { get; internal set; }

    public SensorModule? Sensor { get; internal set; }

    public WeaponModule? Weapon { get; internal set; }

    public DriveModule? Drive { get; internal set; }

    public CommModule? Comm { get; internal set; }

    /// <summary>Design identity this instance was deployed from.</summary>
    public string? BlueprintId { get; }

    /// <summary>Explicitly deployed design version. Upgrades change this field only.</summary>
    public int BlueprintVersion { get; internal set; }

    /// <summary>Program reference recorded by the design; controller runtime is separate.</summary>
    public string? ControllerProgramId { get; internal set; }

    /// <summary>Optional local generation. Adds energy each tick up to capacity.</summary>
    public PowerModule? Power { get; internal set; }

    /// <summary>Optional compute/storage capability. Limits intents per tick.</summary>
    public StateModule? State { get; internal set; }

    /// <summary>
    /// A damageable shell anchor outside the protected core region. Losing anchors
    /// raises recovery costs; the permanent core itself is never an anchor.
    /// </summary>
    public bool IsCoreAnchor { get; internal set; }

    /// <summary>First tick on which the weapon may fire again.</summary>
    public long NextWeaponReadyTick { get; internal set; }

    public bool IsAlive => Health > 0;
}
