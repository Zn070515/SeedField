namespace SeedField.Runtime.Js;

/// <summary>
/// Host-safety limits for one controller. These are degradation guards, not an
/// operating-system sandbox: third-party packages still require process isolation
/// before they may execute arbitrary code.
/// </summary>
public sealed record JsControllerOptions
{
    public int MaxStatements { get; init; } = 50_000;

    public long MemoryLimitBytes { get; init; } = 8_000_000;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMilliseconds(50);

    public void Validate()
    {
        if (MaxStatements <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxStatements));
        }

        if (MemoryLimitBytes <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MemoryLimitBytes));
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout));
        }
    }
}
