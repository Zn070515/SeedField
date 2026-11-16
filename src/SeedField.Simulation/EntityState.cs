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
    CommModule? Comm = null);

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
    }

    public EntityId Id { get; }

    public string CivilizationId { get; }

    public string RegionId { get; }

    public Vec2 Position { get; internal set; }

    public int MaxHealth { get; }

    public int Health { get; internal set; }

    public long Energy { get; internal set; }

    public long EnergyCapacity { get; }

    public long Material { get; internal set; }

    public SensorModule? Sensor { get; }

    public WeaponModule? Weapon { get; }

    public DriveModule? Drive { get; }

    public CommModule? Comm { get; }

    /// <summary>First tick on which the weapon may fire again.</summary>
    public long NextWeaponReadyTick { get; internal set; }

    public bool IsAlive => Health > 0;
}
