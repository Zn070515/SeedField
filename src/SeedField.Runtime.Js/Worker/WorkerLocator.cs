namespace SeedField.Runtime.Js.Worker;

/// <summary>
/// Finds the worker executable. Production builds ship it next to the host; development
/// and test runs fall back to the solution's worker output directory.
/// </summary>
public static class WorkerLocator
{
    public static string Find(string? explicitPath = null)
    {
        var executableName = OperatingSystem.IsWindows()
            ? "SeedField.Worker.exe"
            : "SeedField.Worker";

        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            if (File.Exists(explicitPath) && HasWorkerAssembly(explicitPath))
            {
                return Path.GetFullPath(explicitPath);
            }

            throw new WorkerIsolationException(
                "WORKER_NOT_FOUND",
                $"Worker executable '{explicitPath}' does not exist or lacks SeedField.Worker.dll.");
        }

        var local = Path.Combine(AppContext.BaseDirectory, executableName);
        if (File.Exists(local) && HasWorkerAssembly(local))
        {
            return local;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SeedField.slnx")))
            {
                foreach (var configuration in new[] { "Debug", "Release" })
                {
                    var candidate = Path.Combine(
                        directory.FullName,
                        "src",
                        "SeedField.Worker",
                        "bin",
                        configuration,
                        "net10.0",
                        executableName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                break;
            }

            directory = directory.Parent;
        }

        throw new WorkerIsolationException(
            "WORKER_NOT_FOUND",
            $"Could not locate '{executableName}'. Build SeedField.Worker first.");
    }

    private static bool HasWorkerAssembly(string executable)
    {
        var directory = Path.GetDirectoryName(executable);
        return directory is not null &&
               File.Exists(Path.Combine(directory, "SeedField.Worker.dll"));
    }
}
