using SeedField.Simulation;

namespace SeedField.Runtime.Js;

/// <summary>
/// One controller instance for a civilization or device. Implementations may run
/// in-process (Jint) or in an isolated worker process; the host validates every intent.
/// </summary>
public interface IController : IDisposable
{
    string CivilizationId { get; }

    ControllerDecision Decide(Observation observation);
}

public delegate IController ControllerFactory(
    string civilizationId,
    string script,
    JsControllerOptions? options);
