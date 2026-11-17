using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeedField.Runtime.Js.Worker;

/// <summary>
/// One protocol message. Fields are optional because hello/observe/intents/fault share
/// a single envelope; unknown fields are ignored by JSON but unknown kinds are rejected.
/// </summary>
public sealed record WorkerEnvelope(
    int ProtocolVersion = WorkerProtocol.CurrentVersion,
    string Kind = "",
    string? CivilizationId = null,
    string? Script = null,
    int? MaxStatements = null,
    long? MemoryLimitBytes = null,
    int? TimeoutMs = null,
    long? Id = null,
    string? ObservationJson = null,
    string? IntentsJson = null,
    string? Code = null,
    string? Message = null);

public static class WorkerProtocol
{
    public const int CurrentVersion = 1;
    public const int MaxScriptBytes = 256 * 1024;
    public const int MaxEnvelopeCharacters = 4 * 1024 * 1024;

    public const string Hello = "hello";
    public const string Ready = "ready";
    public const string Observe = "observe";
    public const string Intents = "intents";
    public const string Fault = "fault";
    public const string Shutdown = "shutdown";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 16,
    };

    public static string Serialize(WorkerEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        if (json.Length > MaxEnvelopeCharacters)
        {
            throw new IpcProtocolException(
                "ENVELOPE_TOO_LARGE",
                $"Envelope exceeds {MaxEnvelopeCharacters} characters.");
        }

        return json;
    }

    public static WorkerEnvelope Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxEnvelopeCharacters)
        {
            throw new IpcProtocolException(
                "INVALID_ENVELOPE",
                "Envelope is empty or too large.");
        }

        WorkerEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<WorkerEnvelope>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new IpcProtocolException("INVALID_ENVELOPE", exception.Message);
        }

        if (envelope is null || string.IsNullOrWhiteSpace(envelope.Kind))
        {
            throw new IpcProtocolException(
                "INVALID_ENVELOPE",
                "Envelope is missing a kind.");
        }

        return envelope;
    }
}
