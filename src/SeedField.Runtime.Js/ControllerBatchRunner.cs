using SeedField.Simulation;

namespace SeedField.Runtime.Js;

public sealed record ControllerBatchItem(
    int Index,
    DeviceControllerSlot Controller,
    Observation Observation);

/// <summary>
/// Executes one tick's independent device controllers. Observations are built by
/// the caller before <see cref="Run"/>; the runner only reads them and never
/// touches WorldState.
/// </summary>
public sealed class ControllerBatchRunner
{
    private readonly ControllerExecutionOptions _options;

    public ControllerBatchRunner(ControllerExecutionOptions? options = null)
    {
        _options = options ?? new ControllerExecutionOptions();
        _options.Validate();
    }

    public int LastUsedDegreeOfParallelism { get; private set; } = 1;

    public ControllerDecision[] Run(IReadOnlyList<ControllerBatchItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var decisions = new ControllerDecision[items.Count];
        if (items.Count == 0)
        {
            LastUsedDegreeOfParallelism = 1;
            return decisions;
        }

        var degree = Math.Min(_options.MaxDegreeOfParallelism, items.Count);
        var parallel = items.Count >= _options.MinBatchSize && degree > 1;
        LastUsedDegreeOfParallelism = parallel ? degree : 1;

        if (parallel)
        {
            Parallel.For(
                0,
                items.Count,
                new ParallelOptions { MaxDegreeOfParallelism = degree },
                index => decisions[items[index].Index] = Decide(items[index]));
        }
        else
        {
            foreach (var item in items)
            {
                decisions[item.Index] = Decide(item);
            }
        }

        return decisions;
    }

    private static ControllerDecision Decide(ControllerBatchItem item)
    {
        try
        {
            return item.Controller.Decide(item.Observation);
        }
        catch (Exception exception)
        {
            return ControllerDecision.Fault(ScriptFaultCodes.Fault, exception.Message);
        }
    }
}
