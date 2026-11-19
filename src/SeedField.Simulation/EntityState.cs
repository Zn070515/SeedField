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
    bool IsCoreAnchor = false,
    DrillModule? Drill = null,
    ArmorModule? Armor = null,
    ShieldModule? Shield = null,
    SmelterModule? Smelter = null,
    FabricatorModule? Fabricator = null,
    RepairModule? Repair = null,
    ShieldProjectorModule? ShieldProjector = null);

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
        Drill = definition.Drill;
        Armor = definition.Armor;
        Shield = definition.Shield;
        ShieldEnergy = definition.Shield?.Capacity ?? 0;
        Smelter = definition.Smelter;
        Fabricator = definition.Fabricator;
        Repair = definition.Repair;
        ShieldProjector = definition.ShieldProjector;
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

    /// <summary>Optional material extraction. Requires a deposit in range.</summary>
    public DrillModule? Drill { get; internal set; }

    /// <summary>Optional flat damage reduction applied before shields and health.</summary>
    public ArmorModule? Armor { get; internal set; }

    /// <summary>Optional regenerating shield module.</summary>
    public ShieldModule? Shield { get; internal set; }

    /// <summary>Current shield points. Only the simulation mutates it.</summary>
    public long ShieldEnergy { get; internal set; }

    /// <summary>First tick on which the shield may regenerate after taking damage.</summary>
    public long ShieldRegenReadyTick { get; internal set; }

    /// <summary>Optional ore→material conversion.</summary>
    public SmelterModule? Smelter { get; internal set; }

    /// <summary>Optional material→components conversion.</summary>
    public FabricatorModule? Fabricator { get; internal set; }

    /// <summary>Optional passive repair projector.</summary>
    public RepairModule? Repair { get; internal set; }

    /// <summary>Optional friendly shield recharge projector.</summary>
    public ShieldProjectorModule? ShieldProjector { get; internal set; }

    /// <summary>Scripts may pause passive smelting/fabrication for this device.</summary>
    public bool ProductionEnabled { get; internal set; } = true;

    /// <summary>Half health or less: shield regeneration stops.</summary>
    public bool IsDamaged => Health * 2 <= MaxHealth;

    /// <summary>Quarter health or less: weapon, sensor and drive are disabled.</summary>
    public bool IsCritical => Health * 4 <= MaxHealth;

    /// <summary>
    /// A damageable shell anchor outside the protected core region. Losing anchors
    /// raises recovery costs; the permanent core itself is never an anchor.
    /// </summary>
    public bool IsCoreAnchor { get; internal set; }

    /// <summary>First tick on which the weapon may fire again.</summary>
    public long NextWeaponReadyTick { get; internal set; }

    public bool IsAlive => Health > 0;
}
