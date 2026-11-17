using SeedField.Runtime.Js.Worker;

namespace SeedField.Runtime.Js.Tests;

public sealed class WorkerControllerOptionsTests
{
    [Fact]
    public void DefaultLimitsArePositive()
    {
        var options = new WorkerControllerOptions();
        options.Validate();

        Assert.True(options.JobCpuLimitMs > 0);
        Assert.True(options.DiskLimitBytes > 0);
        Assert.True(options.JobMemoryLimitBytes > 1024 * 1024);
    }

    [Fact]
    public void InvalidCpuOrDiskLimitsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkerControllerOptions { JobCpuLimitMs = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkerControllerOptions { DiskLimitBytes = 0 }.Validate());
    }
}
