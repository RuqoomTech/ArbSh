using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ArbSh.Core.Processes;

public sealed partial class SystemExternalProcessRunner
{
    /// <summary>
    /// عملية Linux متصلة بطرفية POSIX زائفة. يبدأها posix_spawn مباشرة في جلسة جديدة
    /// تكون الطرفية طرفيتها المتحكمة، فتمتلك مجموعة العمليات من أول تعليمة دون setsid وسيط.
    /// </summary>
    private sealed class LinuxPseudoTerminalProcess : IPseudoTerminalProcess
    {
        // shortcut: حجم ثابت بلا تغيير حجم؛ يُربط بأعمدة الواجهة مع محاكي الشبكة الكامل.
        private const ushort Columns = 120;
        private const ushort Rows = 30;

        private const int OpenReadWrite = 0x2;
        private const int OpenNoControllingTerminal = 0x100;
        private const int OpenCloseOnExec = 0x80000;
        private const int DuplicateCloseOnExec = 1030;
        private const nuint SetWindowSize = 0x5414;
        private const nuint BytesAvailable = 0x541B;
        private const short SpawnSetSignalDefault = 0x04;
        private const short SpawnSetSignalMask = 0x08;
        private const short SpawnSetSession = 0x80;
        private const int Interrupted = 4;
        private const int NoSuchProcess = 3;
        private const int SigKill = 9;

        // أكبر من posix_spawn_file_actions_t وposix_spawnattr_t وsigset_t في glibc على x64 وarm64.
        private const int SpawnStructureSize = 1024;

        private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(2);

        private readonly object _inputSync = new();
        private readonly FileStream _output;
        private readonly FileStream _input;
        private readonly SafeFileHandle _masterQuery;
        private readonly SafeFileHandle _slave;
        private readonly int _processId;
        private readonly Task<int> _exit;

        private LinuxPseudoTerminalProcess(
            SafeFileHandle master,
            SafeFileHandle masterWrite,
            SafeFileHandle slave,
            int processId)
        {
            _masterQuery = master;
            _output = new FileStream(master, FileAccess.Read, bufferSize: 1);
            _input = new FileStream(masterWrite, FileAccess.Write, bufferSize: 1);
            _slave = slave;
            _processId = processId;
            _exit = Task.Factory.StartNew(
                () => WaitForExitCode(processId),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public Stream Output => _output;

        public ExternalProcessTreeOwnershipMode OwnershipMode => ExternalProcessTreeOwnershipMode.PosixProcessGroup;

        public static LinuxPseudoTerminalProcess Start(ExternalProcessRequest request)
        {
            string executable = ResolveLinuxTargetExecutable(request);

            SafeFileHandle master = OpenHandle("/dev/ptmx", OpenReadWrite | OpenNoControllingTerminal | OpenCloseOnExec);
            SafeFileHandle? masterWrite = null;
            SafeFileHandle? slave = null;
            try
            {
                int masterFd = (int)master.DangerousGetHandle();
                if (GrantPseudoTerminal(masterFd) != 0 || UnlockPseudoTerminal(masterFd) != 0)
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "تعذر تجهيز طرفية POSIX الزائفة.");
                }

                byte[] name = new byte[256];
                int nameError = PseudoTerminalName(masterFd, name, (nuint)name.Length);
                if (nameError != 0)
                {
                    throw new Win32Exception(nameError, "تعذر معرفة مسار الطرف التابع للطرفية الزائفة.");
                }

                string slavePath = Encoding.UTF8.GetString(name, 0, Array.IndexOf(name, (byte)0));
                var size = new WindowSize { Rows = Rows, Columns = Columns };
                SetSize(masterFd, SetWindowSize, ref size);

                // يبقي ArbSh طرفًا تابعًا مفتوحًا حتى لا يرد القارئ EIO قبل أن يفتحه البرنامج.
                slave = OpenHandle(slavePath, OpenReadWrite | OpenNoControllingTerminal | OpenCloseOnExec);
                masterWrite = ToHandle(Duplicate(masterFd, DuplicateCloseOnExec, 0));

                int processId = Spawn(executable, request, slavePath);
                return new LinuxPseudoTerminalProcess(master, masterWrite, slave, processId);
            }
            catch
            {
                master.Dispose();
                masterWrite?.Dispose();
                slave?.Dispose();
                throw;
            }
        }

        // الجلسة والطرفية المتحكمة تنشآن داخل posix_spawn قبل exec؛ لا خطوة ملكية لاحقة.
        public void Own()
        {
        }

        public void WriteInput(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            try
            {
                lock (_inputSync)
                {
                    _input.Write(bytes, 0, bytes.Length);
                    _input.Flush();
                }
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
                // خرج البرنامج قبل أن يقرأ هذا السطر.
            }
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken) => _exit.WaitAsync(cancellationToken);

        public void Terminate() => KillProcessGroup(throwOnFailure: true);

        public void Kill() => KillProcessGroup(throwOnFailure: false);

        public int GetExitCode() => _exit.Result;

        public void Close()
        {
            // مرة واحدة: بعد حصاد البرنامج قد يُعاد استخدام معرّفه لمجموعة غريبة.
            if (_slave.IsClosed)
            {
                return;
            }

            // أبناء الخلفية في المجموعة لا يبقون بعد خروج البرنامج، كما في المسار غير التفاعلي.
            KillProcessGroup(throwOnFailure: false);

            // إغلاق الطرف التابع والبيانات ما زالت في الطرفية قد يفقد آخر الخرج.
            int masterFd = (int)_masterQuery.DangerousGetHandle();
            DateTime deadline = DateTime.UtcNow + DrainTimeout;
            while (DateTime.UtcNow < deadline
                   && QueryAvailable(masterFd, BytesAvailable, out int available) == 0
                   && available > 0)
            {
                Thread.Sleep(10);
            }

            _slave.Dispose();
        }

        public void Dispose()
        {
            Close();
            _input.Dispose();
            _output.Dispose();
        }

        private static int Spawn(string executable, ExternalProcessRequest request, string slavePath)
        {
            IntPtr actions = Marshal.AllocHGlobal(SpawnStructureSize);
            IntPtr attributes = Marshal.AllocHGlobal(SpawnStructureSize);
            IntPtr signals = Marshal.AllocHGlobal(SpawnStructureSize);
            var strings = new List<IntPtr>();
            bool actionsReady = false;
            bool attributesReady = false;
            try
            {
                Check(FileActionsInit(actions));
                actionsReady = true;
                Check(AttributesInit(attributes));
                attributesReady = true;

                if (request.WorkingDirectory is not null)
                {
                    Check(FileActionsAddChangeDirectory(actions, request.WorkingDirectory));
                }

                // بعد setsid يصبح أول طرفية تُفتح بلا O_NOCTTY طرفية الجلسة المتحكمة.
                Check(FileActionsAddOpen(actions, 0, slavePath, OpenReadWrite, 0));
                Check(FileActionsAddDuplicate(actions, 0, 1));
                Check(FileActionsAddDuplicate(actions, 0, 2));

                // كما يفعل Process.Start: إشارات افتراضية وقناع فارغ بدل ما ورثه وقت تشغيل .NET.
                Check(SignalSetEmpty(signals));
                Check(AttributesSetSignalMask(attributes, signals));
                Check(SignalSetFill(signals));
                Check(AttributesSetSignalDefault(attributes, signals));
                Check(AttributesSetFlags(attributes, SpawnSetSession | SpawnSetSignalDefault | SpawnSetSignalMask));

                IntPtr[] argv = [.. ToNative(strings, [request.Executable, .. request.Arguments]), IntPtr.Zero];
                IntPtr[] environment = [.. ToNative(strings, CreateEnvironment(request)), IntPtr.Zero];
                Check(PosixSpawn(out int processId, executable, actions, attributes, argv, environment));
                return processId;
            }
            finally
            {
                if (actionsReady)
                {
                    FileActionsDestroy(actions);
                }

                if (attributesReady)
                {
                    AttributesDestroy(attributes);
                }

                Marshal.FreeHGlobal(actions);
                Marshal.FreeHGlobal(attributes);
                Marshal.FreeHGlobal(signals);
                foreach (IntPtr text in strings)
                {
                    Marshal.FreeCoTaskMem(text);
                }
            }
        }

        private static IEnumerable<string> CreateEnvironment(ExternalProcessRequest request)
        {
            var variables = new Dictionary<string, string>(StringComparer.Ordinal);
            if (request.InheritEnvironment)
            {
                foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                {
                    variables[(string)entry.Key] = (string?)entry.Value ?? string.Empty;
                }
            }

            foreach (KeyValuePair<string, string?> change in request.EnvironmentChanges)
            {
                if (change.Value is null)
                {
                    variables.Remove(change.Key);
                }
                else
                {
                    variables[change.Key] = change.Value;
                }
            }

            // shortcut: TERM=dumb حتى توجد شبكة VT؛ البرامج تكتب نصًا سطريًا بلا ألوان ولا رسم شاشة.
            if (!request.EnvironmentChanges.ContainsKey("TERM"))
            {
                variables["TERM"] = "dumb";
            }

            return variables.Select(variable => $"{variable.Key}={variable.Value}");
        }

        private static IEnumerable<IntPtr> ToNative(List<IntPtr> owned, IEnumerable<string> values)
        {
            foreach (string value in values)
            {
                IntPtr native = Marshal.StringToCoTaskMemUTF8(value);
                owned.Add(native);
                yield return native;
            }
        }

        private static int WaitForExitCode(int processId)
        {
            while (true)
            {
                int result = WaitProcess(processId, out int status, 0);
                if (result == processId)
                {
                    int signal = status & 0x7f;
                    return signal == 0 ? (status >> 8) & 0xff : 128 + signal;
                }

                int error = Marshal.GetLastPInvokeError();
                if (result < 0 && error == Interrupted)
                {
                    continue;
                }

                throw new Win32Exception(error, "تعذر انتظار خروج العملية على Linux.");
            }
        }

        private void KillProcessGroup(bool throwOnFailure)
        {
            if (SendSignal(-_processId, SigKill) == 0)
            {
                return;
            }

            int error = Marshal.GetLastPInvokeError();
            if (error != NoSuchProcess && throwOnFailure)
            {
                throw new Win32Exception(error, "تعذر إنهاء مجموعة عمليات POSIX.");
            }
        }

        private static SafeFileHandle OpenHandle(string path, int flags) => ToHandle(Open(path, flags));

        private static SafeFileHandle ToHandle(int fd)
        {
            if (fd < 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "تعذر فتح طرفية POSIX الزائفة.");
            }

            return new SafeFileHandle(fd, ownsHandle: true);
        }

        private static void Check(int error)
        {
            if (error != 0)
            {
                throw new Win32Exception(error);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowSize
        {
            public ushort Rows;
            public ushort Columns;
            public ushort PixelWidth;
            public ushort PixelHeight;
        }

        [DllImport("libc", EntryPoint = "open", SetLastError = true)]
        private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

        [DllImport("libc", EntryPoint = "grantpt", SetLastError = true)]
        private static extern int GrantPseudoTerminal(int fd);

        [DllImport("libc", EntryPoint = "unlockpt", SetLastError = true)]
        private static extern int UnlockPseudoTerminal(int fd);

        [DllImport("libc", EntryPoint = "ptsname_r")]
        private static extern int PseudoTerminalName(int fd, byte[] buffer, nuint length);

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        private static extern int SetSize(int fd, nuint request, ref WindowSize size);

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        private static extern int QueryAvailable(int fd, nuint request, out int available);

        [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
        private static extern int Duplicate(int fd, int command, int minimum);

        [DllImport("libc", EntryPoint = "posix_spawn")]
        private static extern int PosixSpawn(
            out int processId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
            IntPtr fileActions,
            IntPtr attributes,
            IntPtr[] arguments,
            IntPtr[] environment);

        [DllImport("libc", EntryPoint = "posix_spawn_file_actions_init")]
        private static extern int FileActionsInit(IntPtr actions);

        [DllImport("libc", EntryPoint = "posix_spawn_file_actions_destroy")]
        private static extern int FileActionsDestroy(IntPtr actions);

        [DllImport("libc", EntryPoint = "posix_spawn_file_actions_addchdir_np")]
        private static extern int FileActionsAddChangeDirectory(
            IntPtr actions,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

        [DllImport("libc", EntryPoint = "posix_spawn_file_actions_addopen")]
        private static extern int FileActionsAddOpen(
            IntPtr actions,
            int fd,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
            int flags,
            uint mode);

        [DllImport("libc", EntryPoint = "posix_spawn_file_actions_adddup2")]
        private static extern int FileActionsAddDuplicate(IntPtr actions, int fd, int newFd);

        [DllImport("libc", EntryPoint = "posix_spawnattr_init")]
        private static extern int AttributesInit(IntPtr attributes);

        [DllImport("libc", EntryPoint = "posix_spawnattr_destroy")]
        private static extern int AttributesDestroy(IntPtr attributes);

        [DllImport("libc", EntryPoint = "posix_spawnattr_setflags")]
        private static extern int AttributesSetFlags(IntPtr attributes, short flags);

        [DllImport("libc", EntryPoint = "posix_spawnattr_setsigmask")]
        private static extern int AttributesSetSignalMask(IntPtr attributes, IntPtr signals);

        [DllImport("libc", EntryPoint = "posix_spawnattr_setsigdefault")]
        private static extern int AttributesSetSignalDefault(IntPtr attributes, IntPtr signals);

        [DllImport("libc", EntryPoint = "sigemptyset")]
        private static extern int SignalSetEmpty(IntPtr signals);

        [DllImport("libc", EntryPoint = "sigfillset")]
        private static extern int SignalSetFill(IntPtr signals);

        [DllImport("libc", EntryPoint = "waitpid", SetLastError = true)]
        private static extern int WaitProcess(int processId, out int status, int options);

        [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
        private static extern int SendSignal(int processId, int signal);
    }
}
