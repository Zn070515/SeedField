namespace SeedField.Simulation;

[Flags]
public enum DeviceBranch
{
    None = 0,
    Energy = 1 << 0,
    Sensing = 1 << 1,
    Communication = 1 << 2,
    State = 1 << 3,
    Execution = 1 << 4,
    All = Energy | Sensing | Communication | State | Execution,
}
