namespace SeedField.Simulation;

public static partial class WorldStateHasher
{
    private static void AppendModule(ref ulong hash, SensorModule? module)
    {
        Append(ref hash, module is not null);
        if (module is not null)
        {
            Append(ref hash, module.RangeMm);
        }
    }

    private static void AppendModule(ref ulong hash, WeaponModule? module)
    {
        Append(ref hash, module is not null);
        if (module is not null)
        {
            Append(ref hash, module.RangeMm);
            Append(ref hash, module.Damage);
            Append(ref hash, module.EnergyCost);
            Append(ref hash, module.CooldownTicks);
            Append(ref hash, (int)module.Kind);
        }
    }

    private static void AppendModule(ref ulong hash, DriveModule? module)
    {
        Append(ref hash, module is not null);
        if (module is not null)
        {
            Append(ref hash, module.SpeedMmPerTick);
            Append(ref hash, module.EnergyCostPerMove);
        }
    }

    private static void AppendModule(ref ulong hash, DrillModule? module)
    {
        Append(ref hash, module is not null);
        if (module is not null)
        {
            Append(ref hash, module.RatePerTick);
            Append(ref hash, module.RangeMm);
            Append(ref hash, module.EnergyCostPerTick);
        }
    }

    private static void AppendModule(ref ulong hash, CommModule? module)
    {
        Append(ref hash, module is not null);
        if (module is not null)
        {
            Append(ref hash, module.LatencyTicks);
            Append(ref hash, module.EnergyCostPerMessage);
            Append(ref hash, module.BandwidthPerTick);
        }
    }
}
