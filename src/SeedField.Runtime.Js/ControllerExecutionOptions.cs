namespace SeedField.Runtime.Js;

/// <summary>
/// Execution policy for one tick's controller batch. The runner stays sequential
/// below <see cref="MinBatchSize"/> so small worlds do not pay thread-pool costs.
/// </summary>
public sealed record ControllerExecutionOptions
{
    public int MinBatchSize { get; init; } = 8;

    public int MaxDegreeOfParallelism { get; init; } =
        Math.Max(1, Environment.ProcessorCount);

    public void Validate()
    {
        if (MinBatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinBatchSize));
        }

        if (MaxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxDegreeOfParallelism));
        }
    }
}
