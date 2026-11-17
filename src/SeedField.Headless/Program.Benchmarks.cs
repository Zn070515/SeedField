namespace SeedField.Headless;

internal static partial class Program
{
    private static bool TryRunBenchmark(string[] args, out int exitCode)
    {
        exitCode = 0;

        if (TryReadBenchmarkDevices(
                args,
                "--benchmark-startup",
                out var startupDevices,
                out var startupInvalid))
        {
            exitCode = startupInvalid ? 2 : RunStartupBenchmark(startupDevices);
            return true;
        }

        if (TryReadBenchmarkDevices(
                args,
                "--benchmark-observation",
                out var observationDevices,
                out var observationInvalid))
        {
            exitCode = observationInvalid ? 2 : RunObservationBenchmark(observationDevices);
            return true;
        }

        if (TryReadBenchmarkDevices(
                args,
                "--benchmark-serialize",
                out var serializeDevices,
                out var serializeInvalid))
        {
            exitCode = serializeInvalid ? 2 : RunSerializeBenchmark(serializeDevices);
            return true;
        }

        if (TryReadBenchmarkDevices(
                args,
                "--benchmark-jint",
                out var jintDevices,
                out var jintInvalid))
        {
            exitCode = jintInvalid ? 2 : RunJintBenchmark(jintDevices);
            return true;
        }

        if (TryReadBenchmarkDevices(
                args,
                "--benchmark-jint-preserialized",
                out var preserializeDevices,
                out var preserializeInvalid))
        {
            var dof = ReadIntFlag(args, "--parallel-dof", 0);
            var ticks = ReadIntFlag(args, "--ticks", 10);
            exitCode = preserializeInvalid
                ? 2
                : RunPreserializedJintBenchmark(preserializeDevices, dof, ticks);
            return true;
        }

        if (TryReadBenchmarkDevices(
                args,
                "--benchmark-jint-parallel",
                out var parallelDevices,
                out var parallelInvalid))
        {
            var dof = ReadIntFlag(args, "--parallel-dof", 0);
            var budget = ReadDoubleFlag(args, "--budget-ms", 0);
            var ticks = ReadIntFlag(args, "--ticks", 10);
            var chunkSize = ReadIntFlag(args, "--chunk-size", 0);
            exitCode = parallelInvalid
                ? 2
                : RunParallelJintBenchmark(
                    parallelDevices,
                    dof,
                    budget,
                    ticks,
                    chunkSize);
            return true;
        }

        if (TryReadBenchmarkDevices(
                args,
                "--benchmark-jint-full",
                out var jintFullDevices,
                out var jintFullInvalid))
        {
            exitCode = jintFullInvalid ? 2 : RunJintBenchmark(jintFullDevices, compact: false);
            return true;
        }

        return false;
    }
}
