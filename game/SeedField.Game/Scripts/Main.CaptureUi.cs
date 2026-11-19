using Godot;
using SeedField.Simulation;

namespace SeedField.Game;

public partial class Main
{
    private bool _captureMainTopdown;

    private void ParseCaptureOptions(string[] arguments)
    {
        _captureEditor = arguments.Contains("--capture-editor");
        _captureWorkshop = arguments.Contains("--capture-workshop");
        _captureDiplomacy = arguments.Contains("--capture-diplomacy");
        _captureDiplomacyAi = arguments.Contains("--capture-diplomacy-ai");
        _capturePower = arguments.Contains("--capture-power");
        _captureRecovery = arguments.Contains("--capture-recovery");
        _captureVictory = arguments.Contains("--capture-victory");
        _captureVisual = arguments.Contains("--capture-visual");
        _captureTactical = arguments.Contains("--capture-tactical");
        _captureDebug = arguments.Contains("--capture-debug");
        _captureOmniscient = arguments.Contains("--capture-omniscient");
        _captureView = arguments.Contains("--capture-view");
        _captureMainTopdown = arguments.Contains("--capture-main-topdown");
        _tacticalVisible = _captureTactical;
        _debugVisible = _captureDebug || _captureOmniscient;
        _omniscientDebug |= _captureOmniscient;
        _paused = _benchmarkMain || arguments.Contains("--capture") ||
                  _captureEditor ||
                  _captureWorkshop ||
                  _captureDiplomacy ||
                  _captureDiplomacyAi ||
                  _capturePower ||
                  _captureRecovery ||
                  _captureVictory ||
                  _captureVisual ||
                  _captureTactical ||
                  _captureDebug ||
                  _captureOmniscient ||
                  _captureView ||
                  _captureMainTopdown;
        _captureRequested = _paused;
        _capturePath = _captureMainTopdown
            ? "res://screenshots/main-topdown.png"
            : _captureEditor
                ? "res://screenshots/m0-program-editor.png"
                : _captureWorkshop
                    ? "res://screenshots/m0-workshop.png"
                    : _captureDiplomacyAi
                        ? "res://screenshots/m0-diplomacy-ai.png"
                        : _captureDiplomacy
                            ? "res://screenshots/m0-diplomacy.png"
                            : _capturePower
                                ? "res://screenshots/m0-power.png"
                                : _captureRecovery
                                    ? "res://screenshots/m0-recovery.png"
                                    : _captureVictory
                                        ? "res://screenshots/m0-victory.png"
                                        : _captureVisual
                                            ? "res://screenshots/art-main-combat.png"
                                            : _captureTactical
                                                ? "res://screenshots/art-main-tactical.png"
                                                : _captureDebug
                                                    ? "res://screenshots/art-main-debug.png"
                                                    : _captureOmniscient
                                                        ? "res://screenshots/art-main-omniscient.png"
                                                        : _captureView
                                                            ? _zoom < 0.9f
                                                                ? "res://screenshots/art-main-far.png"
                                                                : _zoom > 1.1f
                                                                    ? "res://screenshots/art-main-near.png"
                                                                    : "res://screenshots/art-main-mid.png"
                                                            : "res://screenshots/m0-combat.png";
        if (_captureMainTopdown)
        {
            _zoom = 1.25f;
            var focus = new Vec2(3_000, 4_000);
            _pan = new Vector2(560, 360) -
                (BaseMapOrigin + new Vector2(focus.X * MapScale, focus.Y * MapScale));
        }
    }
}
