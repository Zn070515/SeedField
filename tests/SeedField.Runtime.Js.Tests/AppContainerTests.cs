using System.Diagnostics;
using SeedField.Runtime.Js.Worker;
using SeedField.Simulation;

namespace SeedField.Runtime.Js.Tests;

/// <summary>
/// Windows AppContainer integration. These tests are skipped (pass without
/// assertions) when the launcher or the OS AppContainer profile API is unavailable,
/// so non-Windows/dev environments stay usable; the security gate is exercised on
/// Windows runners and the development workstation.
/// </summary>
public sealed class AppContainerTests
{
    private const string Profile = "SeedField.Test";

    [Fact]
    public void WorkerRunsUnderAppContainer()
    {
        if (!LauncherAvailable())
        {
            return;
        }

        var world = new WorldBuilder()
            .AddRegion("r0", 10_000, 10_000)
            .AddEntity(new EntityDefinition(
                new EntityId(1),
                "A",
                "r0",
                new Vec2(100, 100),
                MaxHealth: 10,
                Energy: 10,
                EnergyCapacity: 10,
                Material: 0))
            .Build();
        var engine = new SimulationEngine(world);
        using var controller = new WorkerController(
            "A",
            "function decide(obs) { return [{ type: \"wait\", entityId: obs.viewerEntityId }]; }",
            new WorkerControllerOptions
            {
                UseAppContainer = true,
                AppContainerProfileName = Profile,
            });

        var decision = controller.Decide(
            engine.BuildControllerObservation(new EntityId(1)));

        Assert.Single(decision.Intents);
        Assert.Equal(1, decision.Intents[0].EntityId.Value);
    }

    [Fact]
    public void AppContainerDeniesFileOutsideSandbox()
    {
        if (!LauncherAvailable())
        {
            return;
        }

        var secret = Path.Combine(Path.GetTempPath(), $"seedfield-secret-{Guid.NewGuid():N}.txt");
        File.WriteAllText(secret, "SECRET");
        try
        {
            var result = RunLauncher(
                @"C:\Windows\System32\cmd.exe",
                $"/c type \"{secret}\"",
                grant: false);

            Assert.True(
                result.ExitCode != 0 ||
                result.Output.Contains("Access is denied", StringComparison.OrdinalIgnoreCase),
                $"AppContainer read the file outside its grant: {result.Output}");
        }
        finally
        {
            File.Delete(secret);
        }
    }

    [Fact]
    public void AppContainerCannotWriteToWorkerDirectory()
    {
        if (!LauncherAvailable())
        {
            return;
        }

        var worker = WorkerLocator.Find();
        var directory = Path.GetDirectoryName(worker)!;
        var target = Path.Combine(directory, $"sandbox-write-{Guid.NewGuid():N}.txt");
        try
        {
            var result = RunLauncher(
                @"C:\Windows\System32\cmd.exe",
                $"/c echo x > \"{target}\"",
                grant: false);

            Assert.False(
                File.Exists(target),
                $"AppContainer wrote into the worker directory (exit {result.ExitCode}).");
        }
        finally
        {
            if (File.Exists(target))
            {
                File.Delete(target);
            }
        }
    }

    [Fact]
    public void AppContainerDeniesNetwork()
    {
        if (!LauncherAvailable())
        {
            return;
        }

        var result = RunLauncher(
            @"C:\Windows\System32\curl.exe",
            "--max-time 3 -s -o NUL http://example.com",
            grant: false);

        Assert.NotEqual(0, result.ExitCode);
    }

    private static bool LauncherAvailable()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(@"C:\Windows\System32\cmd.exe"))
        {
            return false;
        }

        try
        {
            var result = RunLauncher(
                @"C:\Windows\System32\cmd.exe",
                "/c echo SANDBOX_READY",
                grant: false);
            return result.ExitCode == 0 &&
                   result.Output.Contains("SANDBOX_READY", StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static (int ExitCode, string Output) RunLauncher(
        string worker,
        string arguments,
        bool grant)
    {
        var launcher = WorkerLocator.FindLauncher();
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
        startInfo.ArgumentList.Add(Profile);
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
