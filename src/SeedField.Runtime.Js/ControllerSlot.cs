using SeedField.Simulation;

namespace SeedField.Runtime.Js;

public sealed record ControllerDeploymentResult(bool Success, int Version, string? Error)
{
    public static ControllerDeploymentResult Ok(int version) => new(true, version, null);

    public static ControllerDeploymentResult Failed(int version, string error) =>
        new(false, version, error);
}

/// <summary>
/// One civilization's live controller plus its deployment history. A successful
/// deploy atomically swaps in a freshly compiled controller; a failed load keeps the
/// previous one running. This is the unit the in-game editor and future save files
/// reference by version.
/// </summary>
public sealed class ControllerSlot : IDisposable
{
    private readonly JsControllerOptions? _options;
    private JsController _controller;

    public ControllerSlot(
        string civilizationId,
        string initialScript,
        JsControllerOptions? options = null,
        int version = 1)
    {
        ArgumentException.ThrowIfNullOrEmpty(civilizationId);
        ArgumentNullException.ThrowIfNull(initialScript);
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Controller version starts at 1.");
        }

        CivilizationId = civilizationId;
        _options = options;
        _controller = new JsController(civilizationId, initialScript, options);
        DeployedScript = initialScript;
        Version = version;
    }

    public string CivilizationId { get; }

    public int Version { get; private set; }

    public string DeployedScript { get; private set; }

    public ControllerDecision Decide(Observation observation) => _controller.Decide(observation);

    public ControllerDeploymentResult Deploy(string script)
    {
        ArgumentNullException.ThrowIfNull(script);

        JsController next;
        try
        {
            next = new JsController(CivilizationId, script, _options);
        }
        catch (Exception exception)
        {
            return ControllerDeploymentResult.Failed(Version, exception.Message);
        }

        var previous = _controller;
        _controller = next;
        DeployedScript = script;
        Version++;
        previous.Dispose();

        return ControllerDeploymentResult.Ok(Version);
    }

    public void Dispose() => _controller.Dispose();
}
