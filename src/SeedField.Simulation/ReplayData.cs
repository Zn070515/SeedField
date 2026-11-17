using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SeedField.Simulation;

/// <summary>Stable, data-only intent record used by replay files.</summary>
public sealed record ReplayIntentData(
    string Kind,
    long EntityId,
    string? IssuerCivilizationId = null,
    int? TargetX = null,
    int? TargetY = null,
    long? TargetId = null,
    string? ToCivilizationId = null,
    string? Topic = null,
    string? Payload = null,
    string? TargetRegionId = null,
    long? Amount = null);

public sealed record ReplayTickData(long Tick, ReplayIntentData[] Intents);

/// <summary>
/// Data-only replay: the initial world save plus the intents submitted each tick,
/// with per-tick state hashes for divergence detection.
/// </summary>
public sealed record ReplayData(
    int SchemaVersion,
    string InitialWorld,
    ReplayTickData[] Ticks,
    string[] TickHashes);

public sealed class ReplayFormatException : Exception
{
    public ReplayFormatException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

public static class ReplaySerializer
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxBytes = 16 * 1024 * 1024;
    public const int MaxTicks = 100_000;
    public const int MaxIntentsPerTick = 10_000;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Save(ReplayData data)
    {
        Validate(data);
        var json = JsonSerializer.Serialize(data, Options);
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes)
        {
            throw new ReplayFormatException(
                "REPLAY_TOO_LARGE",
                $"Replay exceeds {MaxBytes} bytes.");
        }

        return json;
    }

    public static ReplayData Load(string json)
    {
        if (string.IsNullOrWhiteSpace(json) ||
            Encoding.UTF8.GetByteCount(json) > MaxBytes)
        {
            throw new ReplayFormatException(
                "REPLAY_TOO_LARGE",
                $"Replay is empty or exceeds {MaxBytes} bytes.");
        }

        ReplayData? data;
        try
        {
            data = JsonSerializer.Deserialize<ReplayData>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new ReplayFormatException(
                "REPLAY_INVALID_JSON",
                "Replay is not valid JSON.",
                exception);
        }

        if (data is null)
        {
            throw new ReplayFormatException("REPLAY_INVALID_JSON", "Replay is null.");
        }

        Validate(data);
        return data;
    }

    public static void Validate(ReplayData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.SchemaVersion != CurrentSchemaVersion)
        {
            throw new ReplayFormatException(
                "UNSUPPORTED_REPLAY_VERSION",
                $"Replay schema {data.SchemaVersion} is not supported; " +
                $"expected {CurrentSchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(data.InitialWorld))
        {
            throw new ReplayFormatException(
                "REPLAY_MISSING_WORLD",
                "Replay needs an initial world save.");
        }

        if (data.Ticks is null || data.TickHashes is null ||
            data.Ticks.Length != data.TickHashes.Length)
        {
            throw new ReplayFormatException(
                "REPLAY_TICK_HASH_MISMATCH",
                "Replay ticks and tick hashes must have the same length.");
        }

        if (data.Ticks.Length > MaxTicks)
        {
            throw new ReplayFormatException(
                "REPLAY_TOO_MANY_TICKS",
                $"Replay exceeds {MaxTicks} ticks.");
        }

        long previousTick = -1;
        foreach (var tick in data.Ticks)
        {
            if (tick is null ||
                tick.Tick <= previousTick ||
                tick.Intents is null ||
                tick.Intents.Length > MaxIntentsPerTick)
            {
                throw new ReplayFormatException(
                    "REPLAY_INVALID_TICK",
                    "Replay ticks must be strictly increasing with bounded intents.");
            }

            previousTick = tick.Tick;
        }

        foreach (var hash in data.TickHashes)
        {
            _ = ParseHash(hash);
        }
    }

    public static ulong ParseHash(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ReplayFormatException(
                "REPLAY_INVALID_HASH",
                "Replay hash is empty.");
        }

        var value = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? text[2..]
            : text;
        if (!ulong.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hash))
        {
            throw new ReplayFormatException(
                "REPLAY_INVALID_HASH",
                $"Replay hash '{text}' is not a 64-bit hexadecimal value.");
        }

        return hash;
    }

    internal static string FormatHash(ulong hash) => $"0x{hash:x16}";
}

public static class ReplayIntentCodec
{
    public static ReplayIntentData ToData(Intent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return intent switch
        {
            MoveIntent move => new ReplayIntentData(
                "move",
                move.EntityId.Value,
                move.IssuerCivilizationId,
                TargetX: move.Target.X,
                TargetY: move.Target.Y),
            AttackIntent attack => new ReplayIntentData(
                "attack",
                attack.EntityId.Value,
                attack.IssuerCivilizationId,
                TargetId: attack.TargetId.Value),
            SendMessageIntent send => new ReplayIntentData(
                "send",
                send.EntityId.Value,
                send.IssuerCivilizationId,
                ToCivilizationId: send.ToCivilizationId,
                Topic: send.Topic,
                Payload: send.Payload,
                TargetRegionId: send.TargetRegionId),
            WaitIntent wait => new ReplayIntentData(
                "wait",
                wait.EntityId.Value,
                wait.IssuerCivilizationId),
            TransferMaterialIntent transfer => new ReplayIntentData(
                "transfer",
                transfer.EntityId.Value,
                transfer.IssuerCivilizationId,
                TargetRegionId: transfer.TargetRegionId,
                Amount: transfer.Amount),
            SendEnergyIntent sendEnergy => new ReplayIntentData(
                "sendEnergy",
                sendEnergy.EntityId.Value,
                sendEnergy.IssuerCivilizationId,
                TargetRegionId: sendEnergy.TargetRegionId,
                Amount: sendEnergy.Amount),
            DrawEnergyIntent drawEnergy => new ReplayIntentData(
                "drawEnergy",
                drawEnergy.EntityId.Value,
                drawEnergy.IssuerCivilizationId,
                Amount: drawEnergy.Amount),
            _ => throw new ReplayFormatException(
                "REPLAY_UNSUPPORTED_INTENT",
                $"Intent type '{intent.GetType().Name}' is not replayable."),
        };
    }

    public static Intent ToIntent(ReplayIntentData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.EntityId <= 0)
        {
            throw new ReplayFormatException(
                "REPLAY_INVALID_INTENT",
                "Replay intent needs a positive entity id.");
        }

        var entity = new EntityId(data.EntityId);
        Intent intent = data.Kind switch
        {
            "move" => new MoveIntent(
                entity,
                new Vec2(Require(data.TargetX, "targetX"), Require(data.TargetY, "targetY"))),
            "attack" => new AttackIntent(
                entity,
                new EntityId(RequirePositive(data.TargetId, "targetId"))),
            "send" => new SendMessageIntent(
                entity,
                RequireText(data.ToCivilizationId, "toCivilizationId"),
                RequireText(data.Topic, "topic"),
                data.Payload ?? string.Empty,
                data.TargetRegionId),
            "wait" => new WaitIntent(entity),
            "transfer" => new TransferMaterialIntent(
                entity,
                RequireText(data.TargetRegionId, "targetRegionId"),
                RequirePositive(data.Amount, "amount")),
            "sendEnergy" => new SendEnergyIntent(
                entity,
                RequirePositive(data.Amount, "amount"),
                data.TargetRegionId),
            "drawEnergy" => new DrawEnergyIntent(
                entity,
                RequirePositive(data.Amount, "amount")),
            _ => throw new ReplayFormatException(
                "REPLAY_UNSUPPORTED_INTENT",
                $"Unknown replay intent kind '{data.Kind}'."),
        };

        return intent with { IssuerCivilizationId = data.IssuerCivilizationId };
    }

    private static int Require(int? value, string field) =>
        value ?? throw new ReplayFormatException(
            "REPLAY_INVALID_INTENT",
            $"Replay intent is missing '{field}'.");

    private static long RequirePositive(long? value, string field)
    {
        var actual = value ?? throw new ReplayFormatException(
            "REPLAY_INVALID_INTENT",
            $"Replay intent is missing '{field}'.");
        if (actual <= 0)
        {
            throw new ReplayFormatException(
                "REPLAY_INVALID_INTENT",
                $"Replay intent '{field}' must be positive.");
        }

        return actual;
    }

    private static string RequireText(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ReplayFormatException(
                "REPLAY_INVALID_INTENT",
                $"Replay intent is missing '{field}'.")
            : value;
}
