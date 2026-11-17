using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ArbSh.Core.Processes;

internal interface IExternalProcessTreeOwner : IDisposable
{
    ExternalProcessTreeOwnershipMode Mode { get; }

    void Terminate(uint exitCode);
}

internal static class ExternalProcessTreeOwner
{
    internal static IExternalProcessTreeOwner Attach(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        return OperatingSystem.IsWindows()
            ? WindowsJobProcessTreeOwner.Attach(process)
            : OperatingSystem.IsLinux()
                ? PosixProcessGroupOwner.Attach(process)
                : new DotNetProcessTreeOwner(process);
    }

    private sealed class PosixProcessGroupOwner : IExternalProcessTreeOwner
    {
        private const int NoSuchProcess = 3;
        private const int SigKill = 9;
        private static readonly TimeSpan SessionReadyTimeout = TimeSpan.FromSeconds(2);
        private readonly int _processGroupId;
        private bool _disposed;

        private PosixProcessGroupOwner(int processGroupId)
        {
            _processGroupId = processGroupId;
        }

        public ExternalProcessTreeOwnershipMode Mode =>
            ExternalProcessTreeOwnershipMode.PosixProcessGroup;

        internal static PosixProcessGroupOwner Attach(Process process)
        {
            int processId = process.Id;
            var stopwatch = Stopwatch.StartNew();

            while (!process.HasExited && stopwatch.Elapsed < SessionReadyTimeout)
            {
                int sessionId = GetSessionId(processId);
                if (sessionId == processId)
                {
                    return new PosixProcessGroupOwner(processId);
                }

                if (sessionId < 0 && Marshal.GetLastPInvokeError() is not NoSuchProcess)
                {
                    throw new Win32Exception(
                        Marshal.GetLastPInvokeError(),
                        "تعذر التحقق من جلسة العملية على POSIX.");
                }

                Thread.Sleep(2);
            }

            if (process.HasExited)
            {
                return new PosixProcessGroupOwner(processId);
            }

            throw new InvalidOperationException(
                "بدأت العملية على Linux لكنها لم تدخل مجموعة POSIX مستقلة في المهلة المحددة.");
        }

        public void Terminate(uint exitCode)
        {
            KillProcessGroup(throwOnFailure: true);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            KillProcessGroup(throwOnFailure: false);
        }

        private void KillProcessGroup(bool throwOnFailure)
        {
            if (Kill(-_processGroupId, SigKill) == 0)
            {
                return;
            }

            int error = Marshal.GetLastPInvokeError();
            if (error == NoSuchProcess || !throwOnFailure)
            {
                return;
            }

            throw new Win32Exception(error, "تعذر إنهاء مجموعة عمليات POSIX.");
        }

        [DllImport("libc", EntryPoint = "getsid", SetLastError = true)]
        private static extern int GetSessionId(int processId);

        [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
        private static extern int Kill(int processId, int signal);
    }

    private sealed class DotNetProcessTreeOwner : IExternalProcessTreeOwner
    {
        private readonly Process _process;

        internal DotNetProcessTreeOwner(Process process)
        {
            _process = process;
        }

        public ExternalProcessTreeOwnershipMode Mode =>
            ExternalProcessTreeOwnershipMode.DotNetProcessTree;

        public void Terminate(uint exitCode)
        {
            if (_process.HasExited)
            {
                return;
            }

            _process.Kill(entireProcessTree: true);
        }

        public void Dispose()
        {
        }
    }

    private sealed class WindowsJobProcessTreeOwner : IExternalProcessTreeOwner
    {
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;
        private const int JobObjectExtendedLimitInformationClass = 9;
        private readonly SafeFileHandle _jobHandle;

        private WindowsJobProcessTreeOwner(SafeFileHandle jobHandle)
        {
            _jobHandle = jobHandle;
        }

        public ExternalProcessTreeOwnershipMode Mode =>
            ExternalProcessTreeOwnershipMode.WindowsJobObject;

        internal static WindowsJobProcessTreeOwner Attach(Process process)
        {
            SafeFileHandle jobHandle = CreateJobObjectW(IntPtr.Zero, null);
            if (jobHandle.IsInvalid)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "تعذر إنشاء Windows Job Object لامتلاك شجرة العملية.");
            }

            try
            {
                ConfigureKillOnClose(jobHandle);
                // A program that exits before it is assigned leaves an empty job, as on POSIX above.
                if (!AssignProcessToJobObject(jobHandle, process.SafeHandle) && !process.HasExited)
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "تعذر إسناد العملية إلى Windows Job Object.");
                }

                return new WindowsJobProcessTreeOwner(jobHandle);
            }
            catch
            {
                jobHandle.Dispose();
                throw;
            }
        }

        public void Terminate(uint exitCode)
        {
            if (!TerminateJobObject(_jobHandle, exitCode))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "تعذر إنهاء Windows Job Object.");
            }
        }

        public void Dispose()
        {
            _jobHandle.Dispose();
        }

        private static void ConfigureKillOnClose(SafeFileHandle jobHandle)
        {
            var information = new JobObjectExtendedLimitInformation
            {
                BasicLimitInformation = new JobObjectBasicLimitInformation
                {
                    LimitFlags = JobObjectLimitKillOnJobClose
                }
            };
            int length = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
            IntPtr buffer = Marshal.AllocHGlobal(length);

            try
            {
                Marshal.StructureToPtr(information, buffer, fDeleteOld: false);
                if (!SetInformationJobObject(
                    jobHandle,
                    JobObjectExtendedLimitInformationClass,
                    buffer,
                    (uint)length))
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "تعذر ضبط سياسة إغلاق Windows Job Object.");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateJobObjectW(
            IntPtr jobAttributes,
            string? name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(
            SafeFileHandle job,
            int informationClass,
            IntPtr information,
            uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(
            SafeFileHandle job,
            SafeProcessHandle process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateJobObject(
            SafeFileHandle job,
            uint exitCode);

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
        {
            internal long PerProcessUserTimeLimit;
            internal long PerJobUserTimeLimit;
            internal uint LimitFlags;
            internal UIntPtr MinimumWorkingSetSize;
            internal UIntPtr MaximumWorkingSetSize;
            internal uint ActiveProcessLimit;
            internal UIntPtr Affinity;
            internal uint PriorityClass;
            internal uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            internal ulong ReadOperationCount;
            internal ulong WriteOperationCount;
            internal ulong OtherOperationCount;
            internal ulong ReadTransferCount;
            internal ulong WriteTransferCount;
            internal ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInformation
        {
            internal JobObjectBasicLimitInformation BasicLimitInformation;
            internal IoCounters IoInfo;
            internal UIntPtr ProcessMemoryLimit;
            internal UIntPtr JobMemoryLimit;
            internal UIntPtr PeakProcessMemoryUsed;
            internal UIntPtr PeakJobMemoryUsed;
        }
    }
}
