namespace SeedField.Simulation;

/// <summary>
/// Material has already been paid when this order exists. It materialises at
/// CompleteTick; cancellation/loss rules are intentionally not modelled yet.
/// </summary>
public sealed record ConstructionOrder(
    long Id,
    string CivilizationId,
    string BlueprintId,
    int BlueprintVersion,
    string RegionId,
    Vec2 Position,
    long StartTick,
    long CompleteTick,
    long MaterialPaid,
    long ComponentsPaid = 0);
