using SeedField.Runtime.Js.Worker;

namespace SeedField.Runtime.Js.Tests;

public sealed class DirectorySizeGuardTests
{
    [Fact]
    public void SumsNestedFilesAndStopsAtLimit()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"seedfield-size-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        try
        {
            File.WriteAllBytes(Path.Combine(root, "a.bin"), new byte[100]);
            File.WriteAllBytes(
                Path.Combine(root, "nested", "b.bin"),
                new byte[200]);

            Assert.Equal(300, DirectorySizeGuard.ComputeBytes(root, 1_000));
            Assert.True(DirectorySizeGuard.ComputeBytes(root, 150) >= 150);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MissingDirectoryCountsAsZero()
    {
        Assert.Equal(
            0,
            DirectorySizeGuard.ComputeBytes(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                1_000));
    }
}
