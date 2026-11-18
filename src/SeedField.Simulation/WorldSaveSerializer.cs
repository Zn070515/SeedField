using System.Text.Json;

namespace SeedField.Simulation;

/// <summary>
/// JSON persistence for the authoritative world. Loading rebuilds a brand-new
/// WorldState and validates every structural invariant before returning it.
/// </summary>
public static partial class WorldSaveSerializer
{
    public const int CurrentSchemaVersion = 24;
    public const int OldestSupportedSchemaVersion = 1;
    public const string GameVersion = "0.1.0-m0";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string Save(WorldState world) =>
        SaveData(ToData(world));

    public static string SaveData(WorldSaveData data) =>
        JsonSerializer.Serialize(data, Options);

    public static WorldState Load(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new SaveFormatException("EMPTY_SAVE", "Save text is empty.");
        }

        WorldSaveData? data;
        try
        {
            data = JsonSerializer.Deserialize<WorldSaveData>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new SaveFormatException("INVALID_SAVE", exception.Message, exception);
        }

        if (data is null)
        {
            throw new SaveFormatException("INVALID_SAVE", "Save payload is null.");
        }

        return FromData(data);
    }

    public static WorldState FromData(WorldSaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.SchemaVersion < OldestSupportedSchemaVersion ||
            data.SchemaVersion > CurrentSchemaVersion)
        {
            throw new SaveFormatException(
                "UNSUPPORTED_SAVE_VERSION",
                $"Save schema {data.SchemaVersion} is not supported; " +
                $"expected {OldestSupportedSchemaVersion}..{CurrentSchemaVersion}.");
        }

        if (data.SchemaVersion < CurrentSchemaVersion)
        {
            data = Migrate(data);
        }

        var world = new WorldState
        {
            Tick = data.Tick,
            NextEntityId = data.NextEntityId,
            NextMessageId = data.NextMessageId,
        };
        world.Random.Restore(data.RandomSeed, data.RandomState);

        LoadRegions(world, data);
        LoadDeposits(world, data);
        LoadDiscoveries(world, data);
        LoadWrecks(world, data);
        LoadCombatRules(world, data);
        LoadLinks(world, data);
        LoadBlueprints(world, data);
        LoadCores(world, data);
        ValidateCoreExclusion(world);
        ValidateNextEntityId(world);
        LoadMessages(world, data);
        LoadMaterialShipments(world, data);
        LoadEnergyShipments(world, data);
        LoadControlPoints(world, data);
        LoadConstructionOrders(world, data);
        LoadMessagePolicies(world, data);
        LoadDiplomacy(world, data);
        LoadPower(world, data);

        ApplyStatistics(world, data.Statistics);
        return world;
    }

    private static long ResolveNextId(long stored, long maxId, string errorCode)
    {
        if (stored == 0)
        {
            return maxId + 1;
        }

        if (stored <= maxId)
        {
            throw Invalid(errorCode, "Next id must exceed every stored id.");
        }

        return stored;
    }

    private static void ApplyStatistics(WorldState world, StatisticsSaveData statistics)
    {
        world.Statistics.EnergySpentTotal = statistics.EnergySpentTotal;
        world.Statistics.EnergyRegeneratedTotal = statistics.EnergyRegeneratedTotal;
        world.Statistics.EnergyGeneratedTotal = statistics.EnergyGeneratedTotal;
        world.Statistics.MaterialSpentTotal = statistics.MaterialSpentTotal;
        world.Statistics.MaterialRegeneratedTotal = statistics.MaterialRegeneratedTotal;
        world.Statistics.ShotsFired = statistics.ShotsFired;
        world.Statistics.DamageDealt = statistics.DamageDealt;
        world.Statistics.EntitiesDestroyed = statistics.EntitiesDestroyed;
        world.Statistics.MessagesDelivered = statistics.MessagesDelivered;
        world.Statistics.MessagesExpired = statistics.MessagesExpired;
        world.Statistics.MaterialShippedTotal = statistics.MaterialShippedTotal;
        world.Statistics.MaterialDeliveredTotal = statistics.MaterialDeliveredTotal;
        world.Statistics.EnergyShippedTotal = statistics.EnergyShippedTotal;
        world.Statistics.EnergyDeliveredTotal = statistics.EnergyDeliveredTotal;
        world.Statistics.EnergyStoredTotal = statistics.EnergyStoredTotal;
        world.Statistics.EnergyDrawnTotal = statistics.EnergyDrawnTotal;
        world.Statistics.EnergyLostTotal = statistics.EnergyLostTotal;
        world.Statistics.MaterialLostTotal = statistics.MaterialLostTotal;
        world.Statistics.ConstructionsStartedTotal = statistics.ConstructionsStartedTotal;
        world.Statistics.ConstructionsCompletedTotal = statistics.ConstructionsCompletedTotal;
        world.Statistics.MaterialRefundedTotal = statistics.MaterialRefundedTotal;
        world.Statistics.TradeMaterialSettledTotal = statistics.TradeMaterialSettledTotal;
        world.Statistics.TradeEnergySettledTotal = statistics.TradeEnergySettledTotal;
        world.Statistics.EnergyFlowedTotal = statistics.EnergyFlowedTotal;
        world.Statistics.EnergyOverflowedTotal = statistics.EnergyOverflowedTotal;
        world.Statistics.EnergyFlowLossTotal = statistics.EnergyFlowLossTotal;
        world.Statistics.EnergyAutoDrawnTotal = statistics.EnergyAutoDrawnTotal;
        world.Statistics.EnergyWastedTotal = statistics.EnergyWastedTotal;
        world.Statistics.MaterialSalvagedTotal = statistics.MaterialSalvagedTotal;
        world.Statistics.ShotsMissed = statistics.ShotsMissed;
        world.Statistics.MaterialLootedTotal = statistics.MaterialLootedTotal;
        world.Statistics.OreMinedTotal = statistics.OreMinedTotal;
        world.Statistics.ShieldAbsorbedTotal = statistics.ShieldAbsorbedTotal;
        world.Statistics.ArmorAbsorbedTotal = statistics.ArmorAbsorbedTotal;
        world.Statistics.ShieldRegeneratedTotal = statistics.ShieldRegeneratedTotal;
        world.Statistics.OreSmeltedTotal = statistics.OreSmeltedTotal;
        world.Statistics.ComponentsFabricatedTotal = statistics.ComponentsFabricatedTotal;
        world.Statistics.ComponentsRefundedTotal = statistics.ComponentsRefundedTotal;
    }

    /// <summary>
    /// v1 predates links, blueprint registry, material/energy shipments and the
    /// associated ids/stats. Missing collections are treated as empty and the
    /// loaders compute next ids from stored data when zero.
    /// </summary>
    private static WorldSaveData Migrate(WorldSaveData data) =>
        data with
        {
            SchemaVersion = CurrentSchemaVersion,
            Blueprints = data.Blueprints ?? Array.Empty<BlueprintSaveData>(),
            Links = data.Links ?? Array.Empty<LinkSaveData>(),
            MaterialShipments = data.MaterialShipments ?? Array.Empty<MaterialShipmentSaveData>(),
            EnergyShipments = data.EnergyShipments ?? Array.Empty<EnergyShipmentSaveData>(),
            InFlightMessages = data.InFlightMessages ?? Array.Empty<MessageSaveData>(),
            Inboxes = data.Inboxes ?? Array.Empty<InboxSaveData>(),
            ControlPoints = data.ControlPoints ??
                            (data.ControlPoint is null
                                ? Array.Empty<ControlPointSaveData>()
                                : new[] { data.ControlPoint }),
            ConstructionOrders = data.ConstructionOrders ??
                                 Array.Empty<ConstructionOrderSaveData>(),
            MessagePolicies = data.MessagePolicies ??
                              Array.Empty<MessagePolicySaveData>(),
            TreatyProposals = data.TreatyProposals ??
                              Array.Empty<TreatyProposalSaveData>(),
            Treaties = data.Treaties ?? Array.Empty<TreatySaveData>(),
            TradeOffers = data.TradeOffers ?? Array.Empty<TradeOfferSaveData>(),
            PowerPolicies = data.PowerPolicies ?? Array.Empty<PowerPolicySaveData>(),
            PowerShipments = data.PowerShipments ?? Array.Empty<PowerShipmentSaveData>(),
            GridCapacities = data.GridCapacities ?? Array.Empty<GridCapacitySaveData>(),
            PowerGridStates = data.PowerGridStates ?? Array.Empty<PowerGridStateSaveData>(),
            Cores = (data.Cores ?? Array.Empty<CoreSaveData>())
                .Select(core => core with
                {
                    AnchorEnergyPerTick = 0,
                    AnchorMaterialPerTick = 0,
                    AnchorStorageBonus = 0,
                })
                .ToArray(),
        };
}
