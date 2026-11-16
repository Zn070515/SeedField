using System.Text;

namespace SeedField.Simulation;

/// <summary>
/// Stable FNV-1a 64-bit digest of every field that can affect future simulation
/// results. Used by tests and headless runs to prove reproducibility across runs.
/// </summary>
public static class WorldStateHasher
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Compute(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var hash = OffsetBasis;

        Append(ref hash, world.Tick);
        Append(ref hash, world.NextEntityId);
        Append(ref hash, world.NextMessageId);
        Append(ref hash, world.NextShipmentId);

        Append(ref hash, world.Statistics.EnergySpentTotal);
        Append(ref hash, world.Statistics.EnergyRegeneratedTotal);
        Append(ref hash, world.Statistics.EnergyGeneratedTotal);
        Append(ref hash, world.Statistics.MaterialSpentTotal);
        Append(ref hash, world.Statistics.MaterialRegeneratedTotal);
        Append(ref hash, world.Statistics.ShotsFired);
        Append(ref hash, world.Statistics.DamageDealt);
        Append(ref hash, world.Statistics.EntitiesDestroyed);
        Append(ref hash, world.Statistics.MessagesDelivered);
        Append(ref hash, world.Statistics.MessagesExpired);
        Append(ref hash, world.Statistics.MaterialShippedTotal);
        Append(ref hash, world.Statistics.MaterialDeliveredTotal);

        Append(ref hash, world.Regions.Count);
        foreach (var region in world.Regions.Values)
        {
            Append(ref hash, region.Id);
            Append(ref hash, region.WidthMm);
            Append(ref hash, region.HeightMm);
            Append(ref hash, region.IsCoreRegion);
            Append(ref hash, region.Stocks.Count);
            foreach (var stock in region.Stocks)
            {
                Append(ref hash, stock.Key);
                Append(ref hash, stock.Value.Material);
            }

            Append(ref hash, region.Entities.Count);

            foreach (var entity in region.Entities.Values)
            {
                Append(ref hash, entity.Id.Value);
                Append(ref hash, entity.CivilizationId);
                Append(ref hash, entity.RegionId);
                Append(ref hash, entity.Position.X);
                Append(ref hash, entity.Position.Y);
                Append(ref hash, entity.MaxHealth);
                Append(ref hash, entity.Health);
                Append(ref hash, entity.Energy);
                Append(ref hash, entity.EnergyCapacity);
                Append(ref hash, entity.Material);
                Append(ref hash, entity.NextWeaponReadyTick);
                Append(ref hash, entity.BlueprintId ?? string.Empty);
                Append(ref hash, entity.BlueprintVersion);
                Append(ref hash, entity.ControllerProgramId ?? string.Empty);

                AppendModule(ref hash, entity.Sensor);
                AppendModule(ref hash, entity.Weapon);
                AppendModule(ref hash, entity.Drive);
                AppendModule(ref hash, entity.Comm);

                Append(ref hash, entity.Power is not null);
                if (entity.Power is not null)
                {
                    Append(ref hash, entity.Power.EnergyPerTick);
                    Append(ref hash, entity.Power.StorageCapacity);
                }

                Append(ref hash, entity.State is not null);
                if (entity.State is not null)
                {
                    Append(ref hash, entity.State.ComputePerTick);
                    Append(ref hash, entity.State.StorageCapacity);
                }
            }
        }

        var shipments = world.InTransitMaterial.OrderBy(shipment => shipment.Id).ToList();
        Append(ref hash, shipments.Count);
        foreach (var shipment in shipments)
        {
            Append(ref hash, shipment.Id);
            Append(ref hash, shipment.FromEntityId.Value);
            Append(ref hash, shipment.CivilizationId);
            Append(ref hash, shipment.FromRegionId);
            Append(ref hash, shipment.ToRegionId);
            Append(ref hash, shipment.Route);
            Append(ref hash, shipment.Amount);
            Append(ref hash, shipment.SentTick);
            Append(ref hash, shipment.ArriveTick);
        }

        Append(ref hash, world.Links.Count);
        foreach (var link in world.Links.Values)
        {
            Append(ref hash, link.Id);
            Append(ref hash, link.RegionA);
            Append(ref hash, link.RegionB);
            Append(ref hash, link.LatencyTicks);
            Append(ref hash, link.MessagesPerTick);
            Append(ref hash, link.MaterialPerTick);
        }

        var blueprints = world.Workshop.GetAllVersions()
            .OrderBy(blueprint => blueprint.BlueprintId, StringComparer.Ordinal)
            .ThenBy(blueprint => blueprint.Version)
            .ToList();
        Append(ref hash, blueprints.Count);
        foreach (var blueprint in blueprints)
        {
            Append(ref hash, blueprint.BlueprintId);
            Append(ref hash, blueprint.Version);
            Append(ref hash, blueprint.Name);
            Append(ref hash, blueprint.MaxHealth);
            Append(ref hash, blueprint.EnergyCapacity);
            Append(ref hash, blueprint.ControllerProgramId ?? string.Empty);
            Append(ref hash, blueprint.MaterialCost);
            AppendModule(ref hash, blueprint.Sensor);
            AppendModule(ref hash, blueprint.Weapon);
            AppendModule(ref hash, blueprint.Drive);
            AppendModule(ref hash, blueprint.Comm);

            Append(ref hash, blueprint.Power is not null);
            if (blueprint.Power is not null)
            {
                Append(ref hash, blueprint.Power.EnergyPerTick);
                Append(ref hash, blueprint.Power.StorageCapacity);
            }

            Append(ref hash, blueprint.State is not null);
            if (blueprint.State is not null)
            {
                Append(ref hash, blueprint.State.ComputePerTick);
                Append(ref hash, blueprint.State.StorageCapacity);
            }
        }

        Append(ref hash, world.Cores.Count);
        foreach (var core in world.Cores.Values)
        {
            Append(ref hash, core.CivilizationId);
            Append(ref hash, core.RegionId);
            Append(ref hash, core.EnergyReserve);
            Append(ref hash, core.MaterialReserve);
            Append(ref hash, core.Config.EnergyPerTick);
            Append(ref hash, core.Config.MaterialPerTick);
            Append(ref hash, core.Config.MaxEnergyReserve);
            Append(ref hash, core.Config.MaxMaterialReserve);

            Append(ref hash, core.LatestProducts.Count);
            foreach (var product in core.LatestProducts)
            {
                Append(ref hash, (int)product.Key);
                Append(ref hash, product.Value.BlueprintId);
                Append(ref hash, product.Value.Version);
            }
        }

        var inFlight = world.InFlightMessages.OrderBy(message => message.Id).ToList();
        Append(ref hash, inFlight.Count);
        foreach (var message in inFlight)
        {
            Append(ref hash, message.Id);
            Append(ref hash, message.FromEntityId.Value);
            Append(ref hash, message.FromCivilizationId);
            Append(ref hash, message.ToCivilizationId);
            Append(ref hash, message.TargetRegionId);
            Append(ref hash, message.Topic);
            Append(ref hash, message.Payload);
            Append(ref hash, message.SentTick);
            Append(ref hash, message.DeliverTick);
        }

        Append(ref hash, world.Inboxes.Count);
        foreach (var inbox in world.Inboxes)
        {
            Append(ref hash, inbox.Key);
            var messages = inbox.Value.OrderBy(message => message.Id).ToList();
            Append(ref hash, messages.Count);
            foreach (var message in messages)
            {
                Append(ref hash, message.Id);
                Append(ref hash, message.FromEntityId.Value);
                Append(ref hash, message.FromCivilizationId);
                Append(ref hash, message.TargetRegionId);
                Append(ref hash, message.Topic);
                Append(ref hash, message.Payload);
                Append(ref hash, message.SentTick);
                Append(ref hash, message.DeliveredTick);
            }
        }

        return hash;
    }

    public static string ComputeHex(WorldState world) => Compute(world).ToString("x16");

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

    private static void Append(ref ulong hash, long value)
    {
        for (var shift = 0; shift < 64; shift += 8)
        {
            hash ^= (byte)(value >> shift);
            hash *= Prime;
        }
    }

    private static void Append(ref ulong hash, int value) => Append(ref hash, (long)value);

    private static void Append(ref ulong hash, bool value) => Append(ref hash, value ? 1L : 0L);

    private static void Append(ref ulong hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Append(ref hash, bytes.Length);
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= Prime;
        }
    }
}
