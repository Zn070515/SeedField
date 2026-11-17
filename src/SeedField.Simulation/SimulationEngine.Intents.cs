namespace SeedField.Simulation;

public sealed partial class SimulationEngine
{
    private void ProcessIntent(
        Intent intent,
        long tick,
        List<SimulationEvent> events,
        List<IntentResult> results,
        Dictionary<EntityId, int> computeUsed,
        Dictionary<EntityId, int> bandwidthUsed,
        Dictionary<string, int> linkUsage,
        Dictionary<string, int> materialLinkUsage,
        Dictionary<string, int> energyLinkUsage)
    {
        if (intent is null)
        {
            return;
        }

        var entity = World.FindEntity(intent.EntityId, out var region);
        if (entity is null || region is null)
        {
            Reject(intent, SimulationErrorCode.UnknownEntity, "Entity does not exist.", tick, events, results);
            return;
        }

        if (intent.IssuerCivilizationId is { } issuer &&
            !string.Equals(issuer, entity.CivilizationId, StringComparison.Ordinal))
        {
            // Deliberately identical to "unknown entity" so a controller cannot probe
            // for foreign ids by observing different rejection codes.
            Reject(
                intent,
                SimulationErrorCode.UnknownEntity,
                "Entity is not available to this controller.",
                tick,
                events,
                results);
            return;
        }

        if (CoreProtection.IsProtectedFrom(World, region.Id, entity.CivilizationId))
        {
            Reject(
                intent,
                SimulationErrorCode.CoreProtected,
                $"Region '{region.Id}' is a protected core region for another civilization.",
                tick,
                events,
                results);
            return;
        }

        if (!IsRegionActive(region.Id))
        {
            Reject(
                intent,
                SimulationErrorCode.RegionInactive,
                $"Region '{region.Id}' is not active; controller intents are rejected.",
                tick,
                events,
                results);
            return;
        }

        var computeCost = IntentComputeCost(intent);
        if (computeCost > 0 && entity.State is { } state)
        {
            var used = computeUsed.GetValueOrDefault(entity.Id);
            var budget = Math.Max(0, state.ComputePerTick);
            if (used + computeCost > budget)
            {
                Reject(
                    intent,
                    SimulationErrorCode.ComputeBudgetExceeded,
                    $"Compute budget {budget} per tick is exhausted.",
                    tick,
                    events,
                    results);
                return;
            }

            computeUsed[entity.Id] = used + computeCost;
        }

        switch (intent)
        {
            case MoveIntent move:
                ProcessMove(entity, region, move, tick, events, results);
                break;
            case AttackIntent attack:
                ProcessAttack(entity, region, attack, tick, events, results);
                break;
            case SendMessageIntent send:
                ProcessSendMessage(entity, send, tick, events, results, bandwidthUsed, linkUsage);
                break;
            case TransferMaterialIntent transfer:
                ProcessTransferMaterial(entity, region, transfer, tick, events, results, materialLinkUsage);
                break;
            case SendEnergyIntent sendEnergy:
                ProcessSendEnergy(entity, region, sendEnergy, tick, events, results, energyLinkUsage);
                break;
            case DrawEnergyIntent drawEnergy:
                ProcessDrawEnergy(entity, region, drawEnergy, tick, events, results);
                break;
            case WaitIntent:
                Accept(intent, "Entity waits.", results);
                break;
            default:
                Reject(intent, SimulationErrorCode.InvalidAction, "Unknown intent type.", tick, events, results);
                break;
        }
    }

    private static int IntentComputeCost(Intent intent) => intent is WaitIntent ? 0 : 1;

    private int IntentPriority(EntityId entityId) =>
        World.FindEntity(entityId)?.Power?.Priority ?? 0;

    /// <summary>
    /// Processing order is preemptive by device priority. Results are restored to the
    /// caller's input order so the observation/intent contract does not change.
    /// </summary>
    private static List<IntentResult> RestoreInputOrder(
        IReadOnlyList<Intent> intents,
        List<IntentResult> results)
    {
        if (results.Count <= 1)
        {
            return results;
        }

        var positions = new Dictionary<Intent, Queue<int>>(ReferenceEqualityComparer.Instance);
        for (var index = 0; index < intents.Count; index++)
        {
            var intent = intents[index];
            if (!positions.TryGetValue(intent, out var queue))
            {
                queue = new Queue<int>();
                positions.Add(intent, queue);
            }

            queue.Enqueue(index);
        }

        var ordered = new IntentResult?[intents.Count];
        var unmatched = new List<IntentResult>();
        foreach (var result in results)
        {
            if (positions.TryGetValue(result.Intent, out var queue) && queue.Count > 0)
            {
                ordered[queue.Dequeue()] = result;
            }
            else
            {
                unmatched.Add(result);
            }
        }

        var restored = new List<IntentResult>(results.Count);
        foreach (var result in ordered)
        {
            if (result is not null)
            {
                restored.Add(result);
            }
        }

        restored.AddRange(unmatched);
        return restored;
    }

}
