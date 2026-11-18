namespace SeedField.Simulation;

public static partial class WorldSaveSerializer
{
    private static void LoadDeposits(WorldState world, WorldSaveData data)
    {
        var maxId = 0L;
        foreach (var depositData in data.Deposits ?? Array.Empty<DepositSaveData>())
        {
            if (depositData.Id <= 0 ||
                !Enum.IsDefined(depositData.Kind) ||
                depositData.RadiusMm <= 0 ||
                depositData.Remaining < 0 ||
                depositData.YieldPerTick < 0)
            {
                throw Invalid(
                    "INVALID_DEPOSIT",
                    $"Deposit '{depositData.Id}' has invalid fields.");
            }

            if (!world.Regions.TryGetValue(depositData.RegionId, out var region))
            {
                throw Invalid(
                    "UNKNOWN_DEPOSIT_REGION",
                    $"Deposit '{depositData.Id}' references unknown region '{depositData.RegionId}'.");
            }

            var position = new Vec2(depositData.X, depositData.Y);
            if (!region.Contains(position))
            {
                throw Invalid(
                    "DEPOSIT_OUT_OF_BOUNDS",
                    $"Deposit '{depositData.Id}' lies outside region '{region.Id}'.");
            }

            if (!world.Deposits.TryAdd(
                    depositData.Id,
                    new ResourceDepositState(
                        depositData.Id,
                        depositData.RegionId,
                        depositData.Kind,
                        position,
                        depositData.RadiusMm,
                        depositData.Remaining,
                        depositData.YieldPerTick)))
            {
                throw Invalid(
                    "DUPLICATE_DEPOSIT",
                    $"Deposit '{depositData.Id}' appears twice.");
            }

            maxId = Math.Max(maxId, depositData.Id);
        }

        if (data.NextDepositId != 0 && data.NextDepositId <= maxId)
        {
            throw Invalid(
                "INVALID_NEXT_DEPOSIT_ID",
                $"NextDepositId {data.NextDepositId} is not greater than deposit {maxId}.");
        }

        world.NextDepositId = Math.Max(data.NextDepositId, maxId + 1);
    }
}
