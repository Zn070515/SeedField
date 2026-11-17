using System.Diagnostics;
using SeedField.Runtime.Js.Worker;

namespace SeedField.Headless;

internal static partial class Program
{
    private static int RunSandboxCheck()
    {
        string launcher;
        try
        {
            launcher = WorkerLocator.FindLauncher();
        }
        catch (WorkerIsolationException exception)
        {
            Console.Error.WriteLine($"SANDBOX_UNAVAILABLE {exception.Code}: {exception.Message}");
            return 2;
        }

        Console.WriteLine($"SANDBOX_LAUNCHER {launcher}");
        var execute = RunSandboxCommand(
            launcher,
            @"C:\Windows\System32\cmd.exe",
            "/c echo SANDBOX_EXEC_OK",
            grant: false);
        Console.WriteLine(
            $"SANDBOX_EXEC exit={execute.ExitCode} " +
            $"ok={execute.Output.Contains("SANDBOX_EXEC_OK", StringComparison.Ordinal)}");

        var secret = Path.Combine(
            Path.GetTempPath(),
            $"seedfield-sandbox-{Guid.NewGuid():N}.txt");
        File.WriteAllText(secret, "SECRET");
        try
        {
            var file = RunSandboxCommand(
                launcher,
                @"C:\Windows\System32\cmd.exe",
                $"/c type \"{secret}\"",
                grant: false);
            Console.WriteLine(
                $"SANDBOX_FILE exit={file.ExitCode} denied={file.ExitCode != 0}");
        }
        finally
        {
            File.Delete(secret);
        }

        var curl = RunSandboxCommand(
            launcher,
            @"C:\Windows\System32\curl.exe",
            "--max-time 3 -s -o NUL http://example.com",
            grant: false);
        Console.WriteLine(
            $"SANDBOX_NETWORK exit={curl.ExitCode} denied={curl.ExitCode != 0}");
        return 0;
    }

    private static (int ExitCode, string Output) RunSandboxCommand(
        string launcher,
        string worker,
        string arguments,
        bool grant)
    {
        var startInfo = new ProcessStartInfo(launcher)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--worker");
        startInfo.ArgumentList.Add(worker);
        startInfo.ArgumentList.Add("--args");
        startInfo.ArgumentList.Add(arguments);
        startInfo.ArgumentList.Add("--profile");
        startInfo.ArgumentList.Add("SeedField.Check");
        if (!grant)
        {
            startInfo.ArgumentList.Add("--no-grant");
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Sandbox launcher did not start.");
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}
