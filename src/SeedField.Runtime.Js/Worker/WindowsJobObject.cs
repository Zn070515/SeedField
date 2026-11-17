using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SeedField.Runtime.Js.Worker;

/// <summary>
/// Windows Job Object limits for one worker process: memory, one active process,
/// kill-on-close. This is the OS-level boundary that Jint budgets cannot provide.
/// </summary>
public sealed class WindowsJobObject : IDisposable
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitProcessTime = 0x00000002;
    private const uint JobObjectLimitActiveProcess = 0x00000008;
    private const uint JobObjectLimitProcessMemory = 0x00000100;
    private const uint JobObjectLimitJobMemory = 0x00000200;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;

    private IntPtr _handle;

    private WindowsJobObject(IntPtr handle)
    {
        _handle = handle;
        Assigned = true;
    }

    public bool Assigned { get; private set; }

    public static WindowsJobObject? TryAssign(
        Process process,
        long memoryLimitBytes,
        int cpuLimitMs,
        bool require,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(process);
        error = null;

        if (!OperatingSystem.IsWindows())
        {
            if (require)
            {
                throw new WorkerIsolationException(
                    "JOB_OBJECT_UNAVAILABLE",
                    "Windows Job Objects are required but this is not Windows.");
            }

            return null;
        }

        var handle = CreateJobObjectW(IntPtr.Zero, null);
        if (handle == IntPtr.Zero)
        {
            return Fail(require, new Win32Exception(Marshal.GetLastWin32Error()), out error);
        }

        var flags =
            JobObjectLimitActiveProcess |
            JobObjectLimitProcessMemory |
            JobObjectLimitJobMemory |
            JobObjectLimitKillOnJobClose;
        if (cpuLimitMs > 0)
        {
            flags |= JobObjectLimitProcessTime;
        }

        var information = new JobObjectExtendedLimitInformationData
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                PerProcessUserTimeLimit = cpuLimitMs > 0
                    ? -(long)cpuLimitMs * 10_000L
                    : 0,
                LimitFlags = flags,
                ActiveProcessLimit = 1,
            },
            ProcessMemoryLimit = (UIntPtr)(ulong)memoryLimitBytes,
            JobMemoryLimit = (UIntPtr)(ulong)memoryLimitBytes,
        };

        var size = Marshal.SizeOf<JobObjectExtendedLimitInformationData>();
        var infoPointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(information, infoPointer, false);
            if (!SetInformationJobObject(
                    handle,
                    JobObjectExtendedLimitInformation,
                    infoPointer,
                    (uint)size))
            {
                var setError = new Win32Exception(Marshal.GetLastWin32Error());
                CloseHandle(handle);
                return Fail(require, setError, out error);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(infoPointer);
        }

        if (!AssignProcessToJobObject(handle, process.Handle))
        {
            var assignError = new Win32Exception(Marshal.GetLastWin32Error());
            CloseHandle(handle);
            return Fail(require, assignError, out error);
        }

        return new WindowsJobObject(handle);
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }

        Assigned = false;
    }

    private static WindowsJobObject? Fail(
        bool require,
        Win32Exception error,
        out string? message)
    {
        message = error.Message;
        if (require)
        {
            throw new WorkerIsolationException(
                "JOB_OBJECT_FAILED",
                $"Could not apply worker Job Object limits: {error.Message}");
        }

        return null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformationData
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObjectW(IntPtr jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        IntPtr job,
        int informationClass,
        IntPtr information,
        uint informationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
