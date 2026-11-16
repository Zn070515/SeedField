namespace SeedField.Simulation;

/// <summary>
/// Minimal sensor module. Values are content data supplied by the scenario, not
/// frozen balance rules.
/// </summary>
public sealed record SensorModule(int RangeMm);

/// <summary>
/// Minimal direct-fire weapon. Damage and costs are scenario data; the engine only
/// enforces range, cooldown, energy and line-of-fire bookkeeping.
/// </summary>
public sealed record WeaponModule(int RangeMm, int Damage, long EnergyCost, int CooldownTicks);

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
