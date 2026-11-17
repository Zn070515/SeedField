using System.Diagnostics;
using SeedField.Simulation;

namespace SeedField.Runtime.Js.Worker;

/// <summary>
/// Parent-side controller that runs one Jint controller in a separate process. The
/// parent still owns the world and validates every intent; this class only transports
/// observation JSON and parses the returned intents.
/// </summary>
public sealed class WorkerController : IController
{
    private readonly WorkerControllerOptions _options;
    private readonly object _gate = new();
    private readonly object _stderrGate = new();
    private readonly string _script;
    private Process? _process;
    private Stream? _input;
    private Stream? _output;
    private WindowsJobObject? _job;
    private string? _temporaryDirectory;
    private string? _lastWorkerError;
    private CancellationTokenSource? _diskWatchdog;
    private long _nextRequestId = 1;
    private bool _disposed;

    public WorkerController(
        string civilizationId,
        string script,
        WorkerControllerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(civilizationId);
        ArgumentNullException.ThrowIfNull(script);

        CivilizationId = civilizationId;
        _script = script;
        _options = options ?? new WorkerControllerOptions();
        _options.Validate();

        Start();
    }

    public string CivilizationId { get; }

    public bool JobObjectAssigned => _job?.Assigned ?? false;

    public bool IsolationDegraded { get; private set; }

    public string? LastWorkerError
    {
        get
        {
            lock (_stderrGate)
            {
                return _lastWorkerError;
            }
        }
    }

    public int? ProcessId => _process?.Id;

    public bool HasExited => _process?.HasExited ?? true;

    public ControllerDecision Decide(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (!string.Equals(observation.CivilizationId, CivilizationId, StringComparison.Ordinal))
        {
            return ControllerDecision.Fault(
                ScriptFaultCodes.Fault,
                "Observation belongs to a different civilization.");
        }

        // Cross-process payloads stay object-shaped: an older worker (or a worker
        // built from a different revision) can still parse them, and the current
        // harness passes them through unchanged.
        var observationJson = JsController.SerializeFullObservation(observation);
        lock (_gate)
        {
            if (_disposed)
            {
                return ControllerDecision.Fault(
                    ScriptFaultCodes.Fault,
                    "Worker controller has been disposed.");
            }

            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    EnsureStarted();
                    var requestId = _nextRequestId++;
                    Send(new WorkerEnvelope(
                        Kind: WorkerProtocol.Observe,
                        Id: requestId,
                        ObservationJson: observationJson));
                    var response = Read(
                        _options.DecisionTimeoutMs,
                        "WORKER_DECISION_TIMEOUT");

                    if (response.Kind == WorkerProtocol.Intents)
                    {
                        return JsController.ParseDecisionJson(
                            response.IntentsJson ?? string.Empty,
                            CivilizationId);
                    }

                    if (response.Kind == WorkerProtocol.Fault)
                    {
                        return ControllerDecision.Fault(
                            response.Code ?? ScriptFaultCodes.Fault,
                            response.Message ?? "Worker reported a fault.");
                    }

                    return ControllerDecision.Fault(
                        ScriptFaultCodes.Fault,
                        $"Worker returned unexpected kind '{response.Kind}'.");
                }
                catch (WorkerIsolationException exception)
                {
                    KillNoLock();
                    if (attempt == 0)
                    {
                        continue;
                    }

                    return ControllerDecision.Fault(
                        ScriptFaultCodes.Fault,
                        $"{exception.Code}: {exception.Message}");
                }
            }

            return ControllerDecision.Fault(
                ScriptFaultCodes.Fault,
                "Worker did not produce a decision.");
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                Send(new WorkerEnvelope(Kind: WorkerProtocol.Shutdown));
                _input?.Flush();
            }
            catch
            {
                // Best-effort shutdown; the job/process kill below is authoritative.
            }

            try
            {
                _process?.WaitForExit(300);
            }
            catch
            {
                // Ignore races with a crashed worker.
            }

            KillNoLock();
        }
    }

    private void EnsureStarted()
    {
        if (_process is not null && !_process.HasExited)
        {
            return;
        }

        Start();
    }

    private void Start()
    {
        KillNoLock();

        var workerExecutable = WorkerLocator.Find(_options.ExecutablePath);
        var executable = workerExecutable;
        var arguments = string.Empty;
        if (_options.UseAppContainer)
        {
            executable = WorkerLocator.FindLauncher(_options.AppContainerLauncherPath);
            var memoryMb = Math.Max(1, _options.JobMemoryLimitBytes / (1024 * 1024));
            arguments =
                $"--worker \"{workerExecutable}\" " +
                $"--profile \"{_options.AppContainerProfileName}\" " +
                $"--memory-mb {memoryMb} --cpu-ms {_options.JobCpuLimitMs} --keep-profile";
        }

        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"seedfield-worker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _temporaryDirectory,
        };
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_EnableDiagnostics"] = "0";
        foreach (var key in startInfo.Environment.Keys
                     .Where(key =>
                         key is "DOTNET_STARTUP_HOOKS" ||
                         key.StartsWith("CORECLR_", StringComparison.OrdinalIgnoreCase) ||
                         key.StartsWith("COMPlus_", StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            startInfo.Environment.Remove(key);
        }

        var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new WorkerIsolationException(
                    "WORKER_START_FAILED",
                    "Worker process did not start.");
            }
        }
        catch (Exception exception)
        {
            process.Dispose();
            throw new WorkerIsolationException(
                "WORKER_START_FAILED",
                exception.Message);
        }

        _process = process;
        _input = process.StandardInput.BaseStream;
        _output = process.StandardOutput.BaseStream;
        process.ErrorDataReceived += OnWorkerError;
        process.BeginErrorReadLine();

        if (_options.UseAppContainer)
        {
            // The launcher owns the worker's Job Object (it creates the worker
            // suspended, assigns it, then resumes). Assigning the launcher to the
            // host job with ActiveProcessLimit=1 would block the worker child.
            _job = null;
        }
        else
        {
            _job = WindowsJobObject.TryAssign(
                process,
                _options.JobMemoryLimitBytes,
                _options.JobCpuLimitMs,
                _options.RequireJobObject,
                out var jobError);
            if (_job is null)
            {
                IsolationDegraded = true;
                lock (_stderrGate)
                {
                    _lastWorkerError = jobError;
                }
            }
        }

        try
        {
            Send(new WorkerEnvelope(
                Kind: WorkerProtocol.Hello,
                CivilizationId: CivilizationId,
                Script: _script,
                MaxStatements: _options.ControllerOptions.MaxStatements,
                MemoryLimitBytes: _options.ControllerOptions.MemoryLimitBytes,
                TimeoutMs: (int)_options.ControllerOptions.Timeout.TotalMilliseconds));
            var ready = Read(_options.StartupTimeoutMs, "WORKER_START_TIMEOUT");
            if (ready.Kind != WorkerProtocol.Ready)
            {
                throw new WorkerIsolationException(
                    "WORKER_LOAD_FAILED",
                    ready.Message ?? $"Worker did not become ready (kind '{ready.Kind}').");
            }

            StartDiskWatchdog();
        }
        catch (Exception exception)
        {
            var message = exception is WorkerIsolationException isolation
                ? isolation.Message
                : exception.Message;
            var stderr = LastWorkerError;
            var exitCode = _process?.HasExited == true ? _process.ExitCode : (int?)null;
            KillNoLock();
            throw new WorkerIsolationException(
                "WORKER_LOAD_FAILED",
                $"{message} exit={exitCode} stderr={stderr}");
        }
    }

    private void Send(WorkerEnvelope envelope)
    {
        if (_input is null)
        {
            throw new WorkerIsolationException(
                "WORKER_NOT_STARTED",
                "Worker input stream is not available.");
        }

        try
        {
            IpcFraming.Write(_input, WorkerProtocol.Serialize(envelope));
        }
        catch (Exception exception) when (exception is not WorkerIsolationException)
        {
            throw new WorkerIsolationException("WORKER_IPC", exception.Message);
        }
    }

    private WorkerEnvelope Read(int timeoutMs, string timeoutCode)
    {
        if (_output is null)
        {
            throw new WorkerIsolationException(
                "WORKER_NOT_STARTED",
                "Worker output stream is not available.");
        }

        var stream = _output;
        var task = Task.Run(() => IpcFraming.Read(stream));
        if (!task.Wait(timeoutMs))
        {
            _ = task.ContinueWith(
                completed => _ = completed.Exception,
                TaskContinuationOptions.OnlyOnFaulted);
            throw new WorkerIsolationException(
                timeoutCode,
                $"Worker did not answer within {timeoutMs} ms.");
        }

        if (task.IsFaulted)
        {
            var exception = task.Exception?.GetBaseException();
            throw new WorkerIsolationException(
                "WORKER_IPC",
                exception?.Message ?? "Worker IPC failed.");
        }

        var json = task.Result;
        if (json is null)
        {
            throw new WorkerIsolationException(
                "WORKER_EXITED",
                "Worker closed its output stream.");
        }

        try
        {
            return WorkerProtocol.Parse(json);
        }
        catch (IpcProtocolException exception)
        {
            throw new WorkerIsolationException(exception.Code, exception.Message);
        }
    }

    private void OnWorkerError(object sender, DataReceivedEventArgs args)
    {
        if (args.Data is null)
        {
            return;
        }

        lock (_stderrGate)
        {
            var combined = string.IsNullOrEmpty(_lastWorkerError)
                ? args.Data
                : _lastWorkerError + Environment.NewLine + args.Data;
            _lastWorkerError = combined.Length <= 4096
                ? combined
                : combined[^4096..];
        }
    }

    private void StartDiskWatchdog()
    {
        StopDiskWatchdog();
        if (_temporaryDirectory is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _diskWatchdog = cancellation;
        _ = Task.Run(async () =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(250, cancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                var directory = _temporaryDirectory;
                if (directory is null)
                {
                    return;
                }

                var bytes = DirectorySizeGuard.ComputeBytes(
                    directory,
                    _options.DiskLimitBytes + 1);
                if (bytes <= _options.DiskLimitBytes)
                {
                    continue;
                }

                lock (_gate)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    lock (_stderrGate)
                    {
                        _lastWorkerError =
                            $"DISK_LIMIT: {bytes} bytes in worker directory.";
                    }

                    KillNoLock();
                }

                return;
            }
        });
    }

    private void StopDiskWatchdog()
    {
        _diskWatchdog?.Cancel();
        _diskWatchdog?.Dispose();
        _diskWatchdog = null;
    }

    private void KillNoLock()
    {
        StopDiskWatchdog();
        try
        {
            _input?.Dispose();
        }
        catch
        {
            // Worker may already be gone.
        }

        try
        {
            _output?.Dispose();
        }
        catch
        {
            // Worker may already be gone.
        }

        if (_process is not null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Race with exit.
            }

            _process.Dispose();
        }

        _job?.Dispose();
        _job = null;
        _input = null;
        _output = null;
        _process = null;

        if (_temporaryDirectory is not null)
        {
            try
            {
                Directory.Delete(_temporaryDirectory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; the directory is unique to this worker.
            }

            _temporaryDirectory = null;
        }
    }
}
