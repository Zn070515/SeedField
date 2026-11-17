using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SeedField.SandboxLauncher;

/// <summary>
/// Launches one child in a Windows AppContainer. It is a separate process so the
/// host can keep using ordinary redirected stdio: this launcher inherits those
/// handles and passes them to the sandboxed child.
///
/// Diagnostics go to stderr only; stdout belongs to the worker protocol.
/// </summary>
internal static class Program
{
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint CreateSuspended = 0x00000004;
    private const uint StartfUseStdHandles = 0x00000100;
    private const uint DuplicateSameAccess = 0x00000002;
    private const int ProcThreadAttributeSecurityCapabilities = 0x00020009;
    private const int StdInputHandle = -10;
    private const int StdOutputHandle = -11;
    private const int StdErrorHandle = -12;
    private const int AlreadyExists = unchecked((int)0x800700B7);

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var worker = ReadFlag(args, "--worker");
        if (worker is null)
        {
            Console.Error.WriteLine("SANDBOX_USAGE --worker <path> [--args <command line>] [--profile <name>] [--no-grant]");
            return 2;
        }

        var workerArgs = ReadFlag(args, "--args") ?? string.Empty;
        var profile = ReadFlag(args, "--profile") ?? "SeedField.Worker";
        var grant = !args.Contains("--no-grant");
        var keepProfile = args.Contains("--keep-profile");
        var memoryMb = ReadInt(args, "--memory-mb", 128);
        var cpuMs = ReadInt(args, "--cpu-ms", 30_000);

        var sid = CreateOrDeriveSid(profile, out var createHr);
        if (sid == IntPtr.Zero)
        {
            Console.Error.WriteLine($"SANDBOX_PROFILE_FAILED hr=0x{createHr:X8}");
            return 2;
        }

        try
        {
            var sidString = SidToString(sid);
            if (grant)
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(worker));
                if (!string.IsNullOrEmpty(directory))
                {
                    GrantReadExecute(directory, sidString);
                }
            }

            var exitCode = Launch(worker, workerArgs, sid, memoryMb, cpuMs);
            return exitCode;
        }
        finally
        {
            FreeSid(sid);
            if (!keepProfile)
            {
                _ = DeleteAppContainerProfile(profile);
            }
        }
    }

    private static IntPtr CreateOrDeriveSid(string profile, out int hr)
    {
        hr = CreateAppContainerProfile(profile, profile, "SeedField worker sandbox", IntPtr.Zero, 0, out var sid);
        if (hr == AlreadyExists)
        {
            hr = DeriveAppContainerSidFromAppContainerName(profile, out sid);
        }

        return hr == 0 ? sid : IntPtr.Zero;
    }

    private static string SidToString(IntPtr sid)
    {
        if (!ConvertSidToStringSidW(sid, out var text))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            return Marshal.PtrToStringUni(text) ?? string.Empty;
        }
        finally
        {
            _ = LocalFree(text);
        }
    }

    private static void GrantReadExecute(string directory, string sid)
    {
        // Spike-level ACL grant. The AppContainer SID only gets read/execute on the
        // worker directory; no write, no broad user-profile access.
        var startInfo = new ProcessStartInfo("icacls")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(directory);
        startInfo.ArgumentList.Add("/grant");
        startInfo.ArgumentList.Add($"*{sid}:(OI)(CI)(RX)");
        startInfo.ArgumentList.Add("/T");
        startInfo.ArgumentList.Add("/C");
        startInfo.ArgumentList.Add("/Q");

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            Console.Error.WriteLine("SANDBOX_ACL_FAILED icacls did not start");
            return;
        }

        process.WaitForExit();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        if (process.ExitCode != 0)
        {
            Console.Error.WriteLine($"SANDBOX_ACL_FAILED exit={process.ExitCode} {output} {error}");
        }
    }

    private static int Launch(
        string worker,
        string workerArgs,
        IntPtr sid,
        int memoryMb,
        int cpuMs)
    {
        var job = TryCreateJob(memoryMb, cpuMs);
        var commandLine = "\"" + worker + "\"" +
                          (string.IsNullOrWhiteSpace(workerArgs) ? string.Empty : " " + workerArgs);

        var size = UIntPtr.Zero;
        _ = InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
        var attributeList = Marshal.AllocHGlobal((int)size);
        var capabilitiesPointer = Marshal.AllocHGlobal(
            Marshal.SizeOf<SecurityCapabilities>());
        IntPtr input = IntPtr.Zero, output = IntPtr.Zero, error = IntPtr.Zero;
        try
        {
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var capabilities = new SecurityCapabilities
            {
                AppContainerSid = sid,
                Capabilities = IntPtr.Zero,
                CapabilityCount = 0,
                Reserved = 0,
            };
            Marshal.StructureToPtr(capabilities, capabilitiesPointer, false);
            if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    (IntPtr)ProcThreadAttributeSecurityCapabilities,
                    capabilitiesPointer,
                    (UIntPtr)Marshal.SizeOf<SecurityCapabilities>(),
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            input = DuplicateInheritable(GetStdHandle(StdInputHandle));
            output = DuplicateInheritable(GetStdHandle(StdOutputHandle));
            error = DuplicateInheritable(GetStdHandle(StdErrorHandle));

            var startup = new StartupInfoEx
            {
                StartupInfo = new StartupInfo
                {
                    cb = Marshal.SizeOf<StartupInfoEx>(),
                    dwFlags = (int)StartfUseStdHandles,
                    hStdInput = input,
                    hStdOutput = output,
                    hStdError = error,
                },
                lpAttributeList = attributeList,
            };

            if (!CreateProcessW(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    true,
                    ExtendedStartupInfoPresent | CreateSuspended,
                    IntPtr.Zero,
                    Path.GetDirectoryName(Path.GetFullPath(worker)),
                    ref startup,
                    out var processInfo))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                if (!AssignProcessToJobObject(job, processInfo.hProcess))
                {
                    _ = TerminateProcess(processInfo.hProcess, 2);
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (ResumeThread(processInfo.hThread) == uint.MaxValue)
                {
                    _ = TerminateProcess(processInfo.hProcess, 2);
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                _ = WaitForSingleObject(processInfo.hProcess, 0xFFFFFFFF);
                _ = GetExitCodeProcess(processInfo.hProcess, out var exitCode);
                return unchecked((int)exitCode);
            }
            finally
            {
                _ = CloseHandle(processInfo.hThread);
                _ = CloseHandle(processInfo.hProcess);
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"SANDBOX_LAUNCH_FAILED {exception.Message}");
            return 2;
        }
        finally
        {
            DeleteProcThreadAttributeList(attributeList);
            Marshal.FreeHGlobal(capabilitiesPointer);
            Marshal.FreeHGlobal(attributeList);
            if (input != IntPtr.Zero) _ = CloseHandle(input);
            if (output != IntPtr.Zero) _ = CloseHandle(output);
            if (error != IntPtr.Zero) _ = CloseHandle(error);
            if (job != IntPtr.Zero) _ = CloseHandle(job);
        }
    }

    private static IntPtr TryCreateJob(int memoryMb, int cpuMs)
    {
        var job = CreateJobObjectW(IntPtr.Zero, null);
        if (job == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        const uint limitProcessTime = 0x00000002;
        const uint limitActiveProcess = 0x00000008;
        const uint limitProcessMemory = 0x00000100;
        const uint limitJobMemory = 0x00000200;
        const uint limitKillOnJobClose = 0x00002000;
        var memory = (UIntPtr)((ulong)Math.Max(1, memoryMb) * 1024 * 1024);
        var information = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                PerProcessUserTimeLimit = cpuMs > 0 ? -(long)cpuMs * 10_000L : 0,
                LimitFlags = limitProcessTime | limitActiveProcess |
                             limitProcessMemory | limitJobMemory | limitKillOnJobClose,
                ActiveProcessLimit = 1,
                MinimumWorkingSetSize = UIntPtr.Zero,
                MaximumWorkingSetSize = UIntPtr.Zero,
            },
            ProcessMemoryLimit = memory,
            JobMemoryLimit = memory,
        };

        var size = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
        var pointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(information, pointer, false);
            if (!SetInformationJobObject(job, 9, pointer, (uint)size))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        catch
        {
            _ = CloseHandle(job);
            throw;
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }

        return job;
    }

    private static IntPtr DuplicateInheritable(IntPtr handle)
    {
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            return handle;
        }

        if (!DuplicateHandle(
                GetCurrentProcess(),
                handle,
                GetCurrentProcess(),
                out var duplicate,
                0,
                true,
                DuplicateSameAccess))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return duplicate;
    }

    private static string? ReadFlag(string[] args, string flag)
    {
        var index = Array.IndexOf(args, flag);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int ReadInt(string[] args, string flag, int fallback)
    {
        var value = ReadFlag(args, flag);
        return value is not null && int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : fallback;
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
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityCapabilities
    {
        public IntPtr AppContainerSid;
        public IntPtr Capabilities;
        public uint CapabilityCount;
        public uint Reserved;
    }

    [DllImport("userenv.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int CreateAppContainerProfile(
        string appContainerName,
        string displayName,
        string description,
        IntPtr capabilities,
        uint capabilityCount,
        out IntPtr sid);

    [DllImport("userenv.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int DeriveAppContainerSidFromAppContainerName(
        string appContainerName,
        out IntPtr sid);

    [DllImport("userenv.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int DeleteAppContainerProfile(string appContainerName);

    [DllImport("advapi32.dll")]
    private static extern IntPtr FreeSid(IntPtr sid);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertSidToStringSidW(IntPtr sid, out IntPtr text);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int handle);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateHandle(
        IntPtr sourceProcess,
        IntPtr sourceHandle,
        IntPtr targetProcess,
        out IntPtr targetHandle,
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint options);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(
        IntPtr attributeList,
        int attributeCount,
        int flags,
        ref UIntPtr size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr attributeList,
        uint flags,
        IntPtr attribute,
        IntPtr value,
        UIntPtr size,
        IntPtr previousValue,
        IntPtr returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(
        string? applicationName,
        string commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObjectW(IntPtr attributes, string? name);

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
    private static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);
}
