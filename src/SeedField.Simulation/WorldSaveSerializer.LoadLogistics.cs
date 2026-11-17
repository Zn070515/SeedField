namespace SeedField.Simulation;

public static partial class WorldSaveSerializer
{
    private static void LoadMaterialShipments(WorldState world, WorldSaveData data)
    {
        var maxShipmentId = 0L;
        foreach (var shipmentData in data.MaterialShipments ?? Array.Empty<MaterialShipmentSaveData>())
        {
            if (shipmentData.Id <= 0 ||
                shipmentData.Amount <= 0 ||
                shipmentData.ArriveTick <= shipmentData.SentTick)
            {
                throw Invalid(
                    "INVALID_SHIPMENT",
                    $"Shipment '{shipmentData.Id}' has invalid id, amount or timing.");
            }

            if (!world.Regions.ContainsKey(shipmentData.FromRegionId) ||
                !world.Regions.ContainsKey(shipmentData.ToRegionId) ||
                !IsValidShipmentRoute(world, shipmentData))
            {
                throw Invalid(
                    "INVALID_SHIPMENT_ROUTE",
                    $"Shipment '{shipmentData.Id}' references a missing region or invalid route.");
            }

            world.InTransitMaterial.Add(new MaterialShipment(
                shipmentData.Id,
                new EntityId(shipmentData.FromEntityId),
                shipmentData.CivilizationId,
                shipmentData.FromRegionId,
                shipmentData.ToRegionId,
                shipmentData.Route,
                shipmentData.Amount,
                shipmentData.SentTick,
                shipmentData.ArriveTick));
            maxShipmentId = Math.Max(maxShipmentId, shipmentData.Id);
        }

        var nextShipmentId = data.NextShipmentId > 0
            ? data.NextShipmentId
            : maxShipmentId + 1;
        if (nextShipmentId <= maxShipmentId)
        {
            throw Invalid(
                "INVALID_NEXT_SHIPMENT_ID",
                "NextShipmentId must exceed every stored shipment id.");
        }

        world.NextShipmentId = nextShipmentId;
    }

    private static void LoadEnergyShipments(WorldState world, WorldSaveData data)
    {
        var maxEnergyShipmentId = 0L;
        foreach (var shipmentData in data.EnergyShipments ?? Array.Empty<EnergyShipmentSaveData>())
        {
            if (shipmentData.Id <= 0 ||
                shipmentData.Amount <= 0 ||
                shipmentData.ArriveTick <= shipmentData.SentTick ||
                !world.Regions.ContainsKey(shipmentData.FromRegionId) ||
                !world.Regions.ContainsKey(shipmentData.ToRegionId) ||
                !IsValidRoute(
                    world,
                    shipmentData.FromRegionId,
                    shipmentData.ToRegionId,
                    shipmentData.Route))
            {
                throw Invalid(
                    "INVALID_ENERGY_SHIPMENT",
                    $"Energy shipment '{shipmentData.Id}' is invalid.");
            }

            world.InTransitEnergy.Add(new EnergyShipment(
                shipmentData.Id,
                new EntityId(shipmentData.FromEntityId),
                shipmentData.CivilizationId,
                shipmentData.FromRegionId,
                shipmentData.ToRegionId,
                shipmentData.Route,
                shipmentData.Amount,
                shipmentData.SentTick,
                shipmentData.ArriveTick));
            maxEnergyShipmentId = Math.Max(maxEnergyShipmentId, shipmentData.Id);
        }

        var nextEnergyShipmentId = data.NextEnergyShipmentId > 0
            ? data.NextEnergyShipmentId
            : maxEnergyShipmentId + 1;
        if (nextEnergyShipmentId <= maxEnergyShipmentId)
        {
            throw Invalid(
                "INVALID_NEXT_ENERGY_SHIPMENT_ID",
                "NextEnergyShipmentId must exceed every stored energy shipment id.");
        }

        world.NextEnergyShipmentId = nextEnergyShipmentId;
    }
}
