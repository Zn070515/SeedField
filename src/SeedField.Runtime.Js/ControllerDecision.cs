using SeedField.Simulation;

namespace SeedField.Runtime.Js;

public static class ScriptFaultCodes
{
    public const string LoadFailed = "SCRIPT_LOAD_FAILED";

    /// <summary>
    /// Wall-clock host safety valve. It is intentionally not a deterministic in-game
    /// compute result: host load, scheduling and GC pauses can change when it fires.
    /// </summary>
    public const string Timeout = "SCRIPT_TIMEOUT";

    /// <summary>Deterministic Jint heap budget for the same script/input.</summary>
    public const string MemoryLimit = "SCRIPT_MEMORY_LIMIT";

    /// <summary>Deterministic Jint statement budget for the same script/input.</summary>
    public const string StatementLimit = "SCRIPT_STATEMENT_LIMIT";
    public const string Fault = "SCRIPT_FAULT";
    public const string InvalidIntentSchema = "INVALID_INTENT_SCHEMA";
    public const string DeviceScope = "DEVICE_SCOPE";
}

public sealed record ControllerFault(string Code, string Message);

/// <summary>Raw script result before intent parsing; used by the worker process.</summary>
public sealed record ControllerRunResult(
    bool Success,
    string? IntentsJson,
    string? FaultCode,
    string? FaultMessage)
{
    public static ControllerRunResult Ok(string intentsJson) =>
        new(true, intentsJson, null, null);

    public static ControllerRunResult Fault(string code, string message) =>
        new(false, null, code, message);
}

/// <summary>
/// Result of one controller invocation. A fault never mutates the world; the engine
/// receives only the valid intents, if any.
/// </summary>
public sealed record ControllerDecision(
    IReadOnlyList<Intent> Intents,
    IReadOnlyList<ControllerFault> Faults)
{
    public bool HasFaults => Faults.Count > 0;

    public static ControllerDecision Fault(string code, string message) =>
        new([], [new ControllerFault(code, message)]);
}

public sealed class JsControllerLoadException : Exception
{
    public JsControllerLoadException(string message)
        : base(message)
    {
    }

    public JsControllerLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
