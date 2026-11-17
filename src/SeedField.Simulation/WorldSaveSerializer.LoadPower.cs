namespace SeedField.Simulation;

public static partial class WorldSaveSerializer
{
    private static void LoadPower(WorldState world, WorldSaveData data)
    {
        var maxPowerShipmentId = 0L;
        foreach (var shipmentData in data.PowerShipments ?? Array.Empty<PowerShipmentSaveData>())
        {
            if (shipmentData.Id <= 0 ||
                string.IsNullOrWhiteSpace(shipmentData.CivilizationId) ||
                string.IsNullOrWhiteSpace(shipmentData.Route) ||
                !world.Regions.ContainsKey(shipmentData.FromRegionId) ||
                !world.Regions.ContainsKey(shipmentData.ToRegionId) ||
                shipmentData.Amount <= 0 ||
                shipmentData.DeliveredAmount < 0 ||
                shipmentData.DeliveredAmount > shipmentData.Amount ||
                shipmentData.ArriveTick <= shipmentData.SentTick ||
                world.InTransitPower.Any(shipment => shipment.Id == shipmentData.Id))
            {
                throw Invalid(
                    "INVALID_POWER_SHIPMENT",
                    $"Power shipment '{shipmentData.Id}' is invalid.");
            }

            world.InTransitPower.Add(new PowerShipment(
                shipmentData.Id,
                shipmentData.CivilizationId,
                shipmentData.FromRegionId,
                shipmentData.ToRegionId,
                shipmentData.Route,
                shipmentData.Amount,
                shipmentData.DeliveredAmount,
                shipmentData.SentTick,
                shipmentData.ArriveTick));
            maxPowerShipmentId = Math.Max(maxPowerShipmentId, shipmentData.Id);
        }

        world.NextPowerShipmentId = ResolveNextId(
            data.NextPowerShipmentId,
            maxPowerShipmentId,
            "INVALID_NEXT_POWER_SHIPMENT_ID");

        foreach (var policyData in data.PowerPolicies ?? Array.Empty<PowerPolicySaveData>())
        {
            if (string.IsNullOrWhiteSpace(policyData.CivilizationId) ||
                policyData.ReserveEnergy < 0 ||
                policyData.HighPriorityThreshold < 0 ||
                policyData.PriorityReserveEnergy < 0 ||
                policyData.BreakerTripLimit < 0 ||
                policyData.BreakerMaintenanceMaterialCost < 0 ||
                world.PowerPolicies.ContainsKey(policyData.CivilizationId))
            {
                throw Invalid(
                    "INVALID_POWER_POLICY",
                    "Power policy ids must be unique and non-empty; policy values must be non-negative.");
            }

            world.PowerPolicies.Add(
                policyData.CivilizationId,
                new PowerPolicyState(
                    policyData.CivilizationId,
                    policyData.ReserveEnergy,
                    policyData.HighPriorityThreshold,
                    policyData.PriorityReserveEnergy,
                    policyData.AutoReconnect,
                    policyData.BreakerTripLimit,
                    policyData.BreakerMaintenanceMaterialCost));
        }

        foreach (var capacityData in data.GridCapacities ?? Array.Empty<GridCapacitySaveData>())
        {
            if (!world.Regions.TryGetValue(capacityData.RegionId, out var capacityRegion) ||
                string.IsNullOrWhiteSpace(capacityData.CivilizationId) ||
                capacityData.Capacity < 0 ||
                capacityRegion.GridCapacities.ContainsKey(capacityData.CivilizationId))
            {
                throw Invalid(
                    "INVALID_GRID_CAPACITY",
                    "Grid capacities must reference existing regions with unique civilizations and non-negative values.");
            }

            capacityRegion.GridCapacities.Add(
                capacityData.CivilizationId,
                capacityData.Capacity);
        }

        foreach (var stateData in data.PowerGridStates ?? Array.Empty<PowerGridStateSaveData>())
        {
            if (!world.Regions.ContainsKey(stateData.RegionId) ||
                string.IsNullOrWhiteSpace(stateData.CivilizationId) ||
                stateData.TripCount < 0 ||
                world.PowerGridStates.ContainsKey((
                    stateData.RegionId,
                    stateData.CivilizationId)))
            {
                throw Invalid(
                    "INVALID_POWER_GRID_STATE",
                    "Power grid states must reference existing regions, be unique, and have a non-negative trip count.");
            }

            world.PowerGridStates.Add(
                (stateData.RegionId, stateData.CivilizationId),
                new PowerGridState(
                    stateData.RegionId,
                    stateData.CivilizationId)
                {
                    BreakerOpen = stateData.BreakerOpen,
                    ManualHold = stateData.ManualHold,
                    TripCount = stateData.TripCount,
                    MaintenanceRequired = stateData.MaintenanceRequired,
                });
        }
    }
}
