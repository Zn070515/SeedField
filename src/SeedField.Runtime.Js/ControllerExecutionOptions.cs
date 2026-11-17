namespace SeedField.Runtime.Js;

/// <summary>
/// Execution policy for one tick's controller batch. The runner stays sequential
/// below <see cref="MinBatchSize"/> so small worlds do not pay thread-pool costs.
/// </summary>
public sealed record ControllerExecutionOptions
{
    public int MinBatchSize { get; init; } = 8;

    /// <summary>
    /// Physical-core heuristic: SMT siblings do not speed up allocation-heavy Jint
    /// work and made 1000-device ticks slower on the 32-thread dev machine.
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } =
        Math.Clamp(Environment.ProcessorCount, 1, 16);

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
