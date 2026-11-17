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
                foreach (var configuration in PreferredConfigurations())
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

    public static string FindLauncher(string? explicitPath = null)
    {
        var executableName = OperatingSystem.IsWindows()
            ? "SeedField.SandboxLauncher.exe"
            : "SeedField.SandboxLauncher";

        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            if (File.Exists(explicitPath))
            {
                return Path.GetFullPath(explicitPath);
            }

            throw new WorkerIsolationException(
                "SANDBOX_LAUNCHER_NOT_FOUND",
                $"Sandbox launcher '{explicitPath}' does not exist.");
        }

        var local = Path.Combine(AppContext.BaseDirectory, executableName);
        if (File.Exists(local))
        {
            return local;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SeedField.slnx")))
            {
                foreach (var configuration in PreferredConfigurations())
                {
                    var candidate = Path.Combine(
                        directory.FullName,
                        "src",
                        "SeedField.SandboxLauncher",
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
            "SANDBOX_LAUNCHER_NOT_FOUND",
            $"Could not locate '{executableName}'. Build SeedField.SandboxLauncher first.");
    }

    /// <summary>
    /// Prefer the configuration matching the host. A stale Debug copy must not
    /// shadow a current Release worker when both exist in a development tree.
    /// </summary>
    private static IEnumerable<string> PreferredConfigurations()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var separator = Path.DirectorySeparatorChar;
        if (baseDirectory.Contains($"{separator}Release{separator}", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Release";
            yield return "Debug";
        }
        else if (baseDirectory.Contains($"{separator}Debug{separator}", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Debug";
            yield return "Release";
        }
        else
        {
            yield return "Debug";
            yield return "Release";
        }
    }

    private static bool HasWorkerAssembly(string executable)
    {
        var directory = Path.GetDirectoryName(executable);
        return directory is not null &&
               File.Exists(Path.Combine(directory, "SeedField.Worker.dll"));
    }
}
