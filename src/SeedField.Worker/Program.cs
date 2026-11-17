using SeedField.Runtime.Js;
using SeedField.Runtime.Js.Worker;

namespace SeedField.Worker;

/// <summary>
/// Untrusted controller host. It only speaks framed JSON on stdin/stdout, owns one
/// Jint controller, and never sees the world state or a CLR observation object.
/// </summary>
internal static class Program
{
    private static int Main()
    {
        var input = Console.OpenStandardInput();
        var output = Console.OpenStandardOutput();
        JsController? controller = null;

        try
        {
            while (true)
            {
                var json = IpcFraming.Read(input);
                if (json is null)
                {
                    return 0;
                }

                WorkerEnvelope envelope;
                try
                {
                    envelope = WorkerProtocol.Parse(json);
                }
                catch (IpcProtocolException exception)
                {
                    WriteFault(output, 0, exception.Code, exception.Message);
                    return 2;
                }

                switch (envelope.Kind)
                {
                    case WorkerProtocol.Hello:
                        controller?.Dispose();
                        controller = null;
                        if (!TryLoad(envelope, out controller, out var loadFault))
                        {
                            WriteFault(
                                output,
                                0,
                                ScriptFaultCodes.LoadFailed,
                                loadFault);
                            break;
                        }

                        Write(output, new WorkerEnvelope(Kind: WorkerProtocol.Ready));
                        break;

                    case WorkerProtocol.Observe:
                        if (controller is null)
                        {
                            WriteFault(
                                output,
                                envelope.Id ?? 0,
                                ScriptFaultCodes.LoadFailed,
                                "No controller loaded; send hello first.");
                            break;
                        }

                        var run = controller.RunJson(envelope.ObservationJson ?? "");
                        Write(
                            output,
                            run.Success
                                ? new WorkerEnvelope(
                                    Kind: WorkerProtocol.Intents,
                                    Id: envelope.Id,
                                    IntentsJson: run.IntentsJson)
                                : new WorkerEnvelope(
                                    Kind: WorkerProtocol.Fault,
                                    Id: envelope.Id,
                                    Code: run.FaultCode,
                                    Message: run.FaultMessage));
                        break;

                    case WorkerProtocol.Shutdown:
                        controller?.Dispose();
                        return 0;

                    default:
                        WriteFault(
                            output,
                            envelope.Id ?? 0,
                            "IPC_UNKNOWN_KIND",
                            $"Unknown message kind '{envelope.Kind}'.");
                        return 2;
                }
            }
        }
        catch (IpcProtocolException exception)
        {
            WriteFault(output, 0, exception.Code, exception.Message);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 3;
        }
        finally
        {
            controller?.Dispose();
        }
    }

    private static bool TryLoad(
        WorkerEnvelope envelope,
        out JsController? controller,
        out string fault)
    {
        controller = null;
        fault = string.Empty;

        if (string.IsNullOrWhiteSpace(envelope.CivilizationId) ||
            envelope.Script is null)
        {
            fault = "Hello needs civilizationId and script.";
            return false;
        }

        if (System.Text.Encoding.UTF8.GetByteCount(envelope.Script) >
            WorkerProtocol.MaxScriptBytes)
        {
            fault = $"Script exceeds {WorkerProtocol.MaxScriptBytes} bytes.";
            return false;
        }

        var options = new JsControllerOptions
        {
            MaxStatements = envelope.MaxStatements ?? 50_000,
            MemoryLimitBytes = envelope.MemoryLimitBytes ?? 8_000_000,
            Timeout = TimeSpan.FromMilliseconds(envelope.TimeoutMs ?? 50),
        };

        try
        {
            options.Validate();
            controller = new JsController(
                envelope.CivilizationId,
                envelope.Script,
                options);
            return true;
        }
        catch (Exception exception)
        {
            fault = exception.Message;
            return false;
        }
    }

    private static void WriteFault(
        Stream output,
        long id,
        string? code,
        string? message) =>
        Write(
            output,
            new WorkerEnvelope(
                Kind: WorkerProtocol.Fault,
                Id: id,
                Code: code,
                Message: message));

    private static void Write(Stream output, WorkerEnvelope envelope) =>
        IpcFraming.Write(output, WorkerProtocol.Serialize(envelope));
}
