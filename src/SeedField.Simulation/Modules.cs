namespace SeedField.Simulation;

/// <summary>
/// Minimal sensor module. Values are content data supplied by the scenario, not
/// frozen balance rules.
/// </summary>
public sealed record SensorModule(int RangeMm);

public enum WeaponKind
{
    Direct = 0,
    Beam = 1,
}

/// <summary>
/// Weapon. Direct fire is reduced by armor; beams ignore armor entirely. Damage and
/// costs are scenario data; the engine only enforces range, cooldown and energy.
/// </summary>
public sealed record WeaponModule(
    int RangeMm,
    int Damage,
    long EnergyCost,
    int CooldownTicks,
    WeaponKind Kind = WeaponKind.Direct);

/// <summary>Flat damage reduction applied before shields and health.</summary>
public sealed record ArmorModule(int FlatReduction);

/// <summary>
/// Regenerating shield. Regen starts after RegenDelayTicks without damage, costs
/// local energy per regeneration tick, and stops while the device is damaged.
/// </summary>
public sealed record ShieldModule(
    long Capacity,
    long RegenPerTick,
    int RegenDelayTicks,
    long EnergyCostPerRegen);

/// <summary>
/// Converts regional ore into material using <see cref="SmeltingRecipes"/>.
/// PreferredOre pins one kind; otherwise the most abundant eligible kind is used.
/// </summary>
public sealed record SmelterModule(
    long OrePerTick,
    long EnergyCostPerTick,
    OreKind? PreferredOre = null);

/// <summary>
/// Converts material into components, spending local energy. One component costs
/// <paramref name="MaterialPerComponent"/> material.
/// </summary>
public sealed record FabricatorModule(
    long ComponentsPerTick,
    long MaterialPerComponent,
    long EnergyCostPerTick,
    long MaxComponents = 100);

/// <summary>
/// Passive repair projector. Repairs the nearest damaged friendly entity in the same
/// region once per tick, spending local energy and regional material. Range, amount
/// and costs are scenario data, not frozen balance rules.
/// </summary>
public sealed record RepairModule(
    int RangeMm,
    int HealthPerTick,
    long EnergyCostPerTick,
    long MaterialPerTick = 0);

/// <summary>
/// Projects shield recharge to friendly shielded entities in range. Targets must
/// already have a <see cref="ShieldModule"/>; the projector spends local energy per
/// target and bypasses the target's own regen delay. Values are scenario data.
/// </summary>
public sealed record ShieldProjectorModule(
    int RangeMm,
    long RechargePerTick,
    long EnergyCostPerTarget);

/// <summary>Minimal drive. A move action costs a flat amount of local energy.</summary>
public sealed record DriveModule(int SpeedMmPerTick, long EnergyCostPerMove);

/// <summary>
/// Minimal communications module. Messages are queued with a latency of at least one
/// tick and are only readable by the receiving civilization once delivered.
/// </summary>
/// <summary>
/// Minimal communications module. Bandwidth 0 means unlimited in this prototype;
/// a positive value caps messages sent by the entity per tick.
/// </summary>
public sealed record CommModule(
    int LatencyTicks,
    long EnergyCostPerMessage,
    int BandwidthPerTick = 0);
