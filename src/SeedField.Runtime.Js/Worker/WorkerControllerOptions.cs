namespace SeedField.Runtime.Js.Worker;

public sealed class WorkerIsolationException : Exception
{
    public WorkerIsolationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed record WorkerControllerOptions
{
    public string? ExecutablePath { get; init; }

    public int StartupTimeoutMs { get; init; } = 5_000;

    public int DecisionTimeoutMs { get; init; } = 500;

    public long JobMemoryLimitBytes { get; init; } = 128L * 1024 * 1024;

    public int JobCpuLimitMs { get; init; } = 30_000;

    public long DiskLimitBytes { get; init; } = 8L * 1024 * 1024;

    /// <summary>
    /// When true, a failed Job Object assignment aborts startup. Default false because
    /// nested-job support varies in development environments; Phase 1 is allowlist-only.
    /// </summary>
    public bool RequireJobObject { get; init; }

    /// <summary>
    /// Launch the worker inside a Windows AppContainer with no capabilities. The
    /// launcher also owns a Job Object for the worker. When true, a missing
    /// launcher is a hard failure; there is no silent unsandboxed fallback.
    /// </summary>
    public bool UseAppContainer { get; init; }

    public string AppContainerProfileName { get; init; } = "SeedField.Worker";

    public string? AppContainerLauncherPath { get; init; }

    public JsControllerOptions ControllerOptions { get; init; } = new();

    public void Validate()
    {
        if (StartupTimeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(StartupTimeoutMs));
        }

        if (DecisionTimeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(DecisionTimeoutMs));
        }

        if (JobMemoryLimitBytes <= 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(JobMemoryLimitBytes),
                "Worker memory limit must be greater than 1 MiB.");
        }

        if (JobCpuLimitMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(JobCpuLimitMs));
        }

        if (DiskLimitBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(DiskLimitBytes));
        }

        if (UseAppContainer && string.IsNullOrWhiteSpace(AppContainerProfileName))
        {
            throw new ArgumentException(
                "AppContainer profile name is required when UseAppContainer is true.",
                nameof(AppContainerProfileName));
        }

        ControllerOptions.Validate();
    }
}
