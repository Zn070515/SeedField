using System.Text.Json;
using SeedField.Simulation;

namespace SeedField.Runtime.Js;

public sealed record ControllerSaveData(string CivilizationId, int Version, string Script);

public sealed record GameSaveData(
    int SchemaVersion,
    WorldSaveData World,
    ControllerSaveData[] Controllers,
    string? OpponentPackageId = null,
    string? OpponentPackageVersion = null);

public sealed record LoadedGame(
    WorldState World,
    IReadOnlyList<ControllerSlot> Controllers,
    string? OpponentPackageId = null,
    string? OpponentPackageVersion = null);

/// <summary>
/// Composition-level save: authoritative world plus the deployed controller programs
/// and versions. The world schema and the game schema are versioned independently.
/// </summary>
public static class GameSaveSerializer
{
    public const int CurrentSchemaVersion = 3;
    public const int OldestSupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string Save(
        WorldState world,
        IEnumerable<ControllerSlot> controllers,
        string? opponentPackageId = null,
        string? opponentPackageVersion = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(controllers);

        var data = new GameSaveData(
            CurrentSchemaVersion,
            WorldSaveSerializer.ToData(world),
            controllers
                .Select(controller => new ControllerSaveData(
                    controller.CivilizationId,
                    controller.Version,
                    controller.DeployedScript))
                .OrderBy(controller => controller.CivilizationId, StringComparer.Ordinal)
                .ToArray(),
            opponentPackageId,
            opponentPackageVersion);

        return JsonSerializer.Serialize(data, Options);
    }

    public static LoadedGame Load(string json, JsControllerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new SaveFormatException("EMPTY_SAVE", "Save text is empty.");
        }

        GameSaveData? data;
        try
        {
            data = JsonSerializer.Deserialize<GameSaveData>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new SaveFormatException("INVALID_SAVE", exception.Message, exception);
        }

        if (data is null)
        {
            throw new SaveFormatException("INVALID_SAVE", "Save payload is null.");
        }

        if (data.SchemaVersion < OldestSupportedSchemaVersion ||
            data.SchemaVersion > CurrentSchemaVersion)
        {
            throw new SaveFormatException(
                "UNSUPPORTED_GAME_SAVE_VERSION",
                $"Game save schema {data.SchemaVersion} is not supported; " +
                $"expected {OldestSupportedSchemaVersion}..{CurrentSchemaVersion}.");
        }

        if (data.SchemaVersion < CurrentSchemaVersion)
        {
            data = data with { SchemaVersion = CurrentSchemaVersion };
        }

        if (data.World is null)
        {
            throw new SaveFormatException("INVALID_SAVE", "Game save has no world payload.");
        }

        var world = WorldSaveSerializer.FromData(data.World);
        var controllers = new List<ControllerSlot>();

        foreach (var controllerData in data.Controllers)
        {
            if (string.IsNullOrWhiteSpace(controllerData.CivilizationId))
            {
                throw new SaveFormatException(
                    "INVALID_CONTROLLER",
                    "Controller entry has no civilization id.");
            }

            if (string.IsNullOrWhiteSpace(controllerData.Script))
            {
                throw new SaveFormatException(
                    "INVALID_CONTROLLER",
                    $"Controller for '{controllerData.CivilizationId}' has no script.");
            }

            try
            {
                controllers.Add(new ControllerSlot(
                    controllerData.CivilizationId,
                    controllerData.Script,
                    options,
                    controllerData.Version));
            }
            catch (Exception exception)
            {
                throw new SaveFormatException(
                    "INVALID_CONTROLLER_SCRIPT",
                    $"Controller for '{controllerData.CivilizationId}' failed to load: {exception.Message}",
                    exception);
            }
        }

        return new LoadedGame(
            world,
            controllers,
            data.OpponentPackageId,
            data.OpponentPackageVersion);
    }
}
