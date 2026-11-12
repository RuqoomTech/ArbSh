using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ArbSh.Core.Processes;

public sealed partial class SystemExternalProcessRunner : IInteractiveExternalProcessRunner
{
    // ConPTY متاح منذ Windows 10 1809؛ وعلى Linux طرفية POSIX الزائفة. يبقى macOS على الأنابيب.
    /// <inheritdoc />
    public bool SupportsInteractive =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763) || OperatingSystem.IsLinux();

    /// <inheritdoc />
    public async Task<ExternalProcessResult> RunInteractiveAsync(
        ExternalProcessRequest request,
        InteractiveConsole console,
        IExternalProcessOutputSink outputSink,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(outputSink);

        var stopwatch = Stopwatch.StartNew();
        if (!SupportsInteractive)
        {
            return CreateLaunchFailure(stopwatch.Elapsed, "لا تدعم هذه المنصة الطرفية الزائفة بعد.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return CreateCancelledResult(
                stopwatch.Elapsed,
                string.Empty,
                string.Empty,
                ExternalProcessTreeOwnershipMode.None,
                failureMessage: null);
        }

        IPseudoTerminalProcess terminal;
        try
        {
            terminal = OperatingSystem.IsWindows()
                ? WindowsPseudoConsoleProcess.StartSuspended(request, console.Columns, console.Rows)
                : LinuxPseudoTerminalProcess.Start(request, console.Columns, console.Rows);
        }
        catch (Exception exception) when (IsLaunchException(exception))
        {
            return CreateLaunchFailure(stopwatch.Elapsed, exception.Message);
        }

        using (terminal)
        using (InteractiveConsole.Session session = console.Attach(terminal.WriteInput, terminal.Resize))
        {
            // قد يتغير حجم النافذة بين قراءة الحجم وبدء الطرفية.
            if (console.Columns != terminal.Columns || console.Rows != terminal.Rows)
            {
                session.Resize(console.Columns, console.Rows);
            }

            Task<string> outputTask = PumpTerminalOutputAsync(terminal.Output, session, outputSink);
            try
            {
                terminal.Own();
            }
            catch (Exception exception) when (IsOwnershipException(exception))
            {
                terminal.Kill();
                terminal.Close();
                string failedOutput = await outputTask.ConfigureAwait(false);
                return CreateOwnershipFailure(stopwatch.Elapsed, failedOutput, string.Empty, exception.Message);
            }

            bool cancelled = false;
            string? cancellationFailure = null;
            try
            {
                await terminal.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                try
                {
                    terminal.Terminate();
                }
                catch (Exception exception) when (IsOwnershipException(exception))
                {
                    cancellationFailure = exception.Message;
                    terminal.Kill();
                }

                await terminal.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            // لا تنغلق قناة الخرج بخروج البرنامج؛ الإغلاق ينهي ما بقي من الشجرة ويصرف الخرج ثم ينهيه.
            int exitCode = terminal.GetExitCode();
            terminal.Close();
            string output = await outputTask.ConfigureAwait(false);
            stopwatch.Stop();

            return cancelled
                ? CreateCancelledResult(
                    stopwatch.Elapsed,
                    output,
                    string.Empty,
                    terminal.OwnershipMode,
                    cancellationFailure)
                : new ExternalProcessResult(
                    ExternalProcessTerminationReason.Exited,
                    exitCode,
                    output,
                    string.Empty,
                    stopwatch.Elapsed,
                    failureMessage: null,
                    terminal.OwnershipMode);
        }
    }

    /// <summary>
    /// يقرأ خرج الطرفية الزائفة حتى تُغلق ويطبقه على شاشة الجلسة، ويبث كل سطر تسلّمه الشاشة.
    /// الخرج المجمّع نص بلا تسلسلات؛ والأسطر المبثوثة تحفظ ألوان SGR لمن يعرضها.
    /// </summary>
    private static async Task<string> PumpTerminalOutputAsync(
        Stream terminalOutput,
        InteractiveConsole.Session session,
        IExternalProcessOutputSink outputSink)
    {
        var captured = new StringBuilder();
        Decoder decoder = Utf8WithoutBom.GetDecoder();
        byte[] bytes = new byte[4096];
        char[] characters = new char[Utf8WithoutBom.GetMaxCharCount(bytes.Length)];

        async ValueTask EmitAsync(List<string> lines)
        {
            foreach (string line in lines)
            {
                captured.Append(VtScreen.StripStyles(line)).Append('\n');
                await outputSink
                    .WriteAsync(
                        new ExternalProcessOutputChunk(ExternalProcessStream.StandardOutput, line + "\n"),
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }

        while (true)
        {
            int byteCount;
            try
            {
                byteCount = await terminalOutput.ReadAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
                // يرد Linux بالخطأ EIO على قارئ الطرفية بعد إغلاق آخر طرف تابع.
                break;
            }

            if (byteCount == 0)
            {
                break;
            }

            int characterCount = decoder.GetChars(bytes, 0, byteCount, characters, 0);
            await EmitAsync(session.Feed(characters.AsSpan(0, characterCount))).ConfigureAwait(false);
        }

        await EmitAsync(session.Finish()).ConfigureAwait(false);
        return captured.ToString();
    }

    /// <summary>
    /// عملية متصلة بطرفية زائفة. الترتيب: البدء ثم Own ثم الانتظار ثم GetExitCode ثم Close.
    /// </summary>
    private interface IPseudoTerminalProcess : IDisposable
    {
        Stream Output { get; }

        ExternalProcessTreeOwnershipMode OwnershipMode { get; }

        int Columns { get; }

        int Rows { get; }

        /// <summary>يغير حجم الطرفية الزائفة؛ يصل البرنامج إشعار تغيير الحجم من النظام.</summary>
        void Resize(int columns, int rows);

        /// <summary>يمتلك شجرة العملية ثم يسمح لها بالعمل.</summary>
        void Own();

        void WriteInput(string text);

        Task WaitForExitAsync(CancellationToken cancellationToken);

        /// <summary>ينهي الشجرة عبر مالكها؛ قد يرمي استثناء ملكية.</summary>
        void Terminate();

        /// <summary>إنهاء احتياطي لا يرمي.</summary>
        void Kill();

        int GetExitCode();

        /// <summary>ينهي ما بقي من الشجرة ويغلق الطرف التابع حتى ينتهي الخرج.</summary>
        void Close();
    }

    /// <summary>
    /// يبني سطر أوامر CreateProcess من argv بقواعد اقتباس CommandLineToArgvW؛ لا يستقبل نصًا مركبًا من المستخدم.
    /// </summary>
    internal static string BuildWindowsCommandLine(string executable, IEnumerable<string> arguments)
    {
        var commandLine = new StringBuilder();
        AppendWindowsArgument(commandLine, executable, alwaysQuote: true);
        foreach (string argument in arguments)
        {
            commandLine.Append(' ');
            AppendWindowsArgument(commandLine, argument, alwaysQuote: false);
        }

        return commandLine.ToString();
    }

    private static void AppendWindowsArgument(StringBuilder commandLine, string argument, bool alwaysQuote)
    {
        if (!alwaysQuote && argument.Length > 0 && argument.AsSpan().IndexOfAny(' ', '\t', '"') < 0)
        {
            commandLine.Append(argument);
            return;
        }

        commandLine.Append('"');
        int backslashes = 0;
        foreach (char character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            // الشرطات المائلة قبل علامة اقتباس تُضاعف، وتبقى كما هي قبل أي حرف آخر.
            commandLine.Append('\\', character == '"' ? (backslashes * 2) + 1 : backslashes);
            commandLine.Append(character);
            backslashes = 0;
        }

        commandLine.Append('\\', backslashes * 2).Append('"');
    }

    /// <summary>
    /// عملية Windows متصلة بطرفية زائفة ConPTY، تُنشأ معلقة ليمتلك Job Object شجرتها قبل أن تعمل.
    /// </summary>
    private sealed class WindowsPseudoConsoleProcess : IPseudoTerminalProcess
    {
        private const uint ExtendedStartupInfoPresent = 0x00080000;
        private const uint CreateUnicodeEnvironment = 0x00000400;
        private const uint CreateSuspended = 0x00000004;
        private const int StartfUseStdHandles = 0x00000100;
        private static readonly IntPtr PseudoConsoleAttribute = 0x00020016;

        private readonly object _inputSync = new();
        private readonly FileStream _input;
        private readonly FileStream _output;
        private readonly IntPtr _attributeList;
        private readonly SafeProcessHandle _processHandle;
        private IntPtr _threadHandle;
        private IntPtr _pseudoConsole;
        private IExternalProcessTreeOwner? _treeOwner;

        private WindowsPseudoConsoleProcess(
            FileStream input,
            FileStream output,
            IntPtr pseudoConsole,
            IntPtr attributeList,
            ProcessInformation processInformation,
            int columns,
            int rows)
        {
            Columns = columns;
            Rows = rows;
            _input = input;
            _output = output;
            _pseudoConsole = pseudoConsole;
            _attributeList = attributeList;
            _processHandle = new SafeProcessHandle(processInformation.Process, ownsHandle: true);
            _threadHandle = processInformation.Thread;
            Process = Process.GetProcessById(processInformation.ProcessId);
        }

        public Process Process { get; }

        public Stream Output => _output;

        public ExternalProcessTreeOwnershipMode OwnershipMode => ExternalProcessTreeOwnershipMode.WindowsJobObject;

        public int Columns { get; private set; }

        public int Rows { get; private set; }

        public static WindowsPseudoConsoleProcess StartSuspended(ExternalProcessRequest request, int columns, int rows)
        {
            if (!CreatePipe(out SafeFileHandle inputRead, out SafeFileHandle inputWrite, IntPtr.Zero, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (!CreatePipe(out SafeFileHandle outputRead, out SafeFileHandle outputWrite, IntPtr.Zero, 0))
            {
                int error = Marshal.GetLastWin32Error();
                inputRead.Dispose();
                inputWrite.Dispose();
                throw new Win32Exception(error);
            }

            IntPtr pseudoConsole = IntPtr.Zero;
            IntPtr attributeList = IntPtr.Zero;
            IntPtr environment = IntPtr.Zero;
            try
            {
                int result = CreatePseudoConsole(
                    new Coordinate { X = (short)columns, Y = (short)rows },
                    inputRead,
                    outputWrite,
                    0,
                    out pseudoConsole);
                if (result != 0)
                {
                    throw new Win32Exception(result, "تعذر إنشاء الطرفية الزائفة ConPTY.");
                }

                attributeList = CreatePseudoConsoleAttributeList(pseudoConsole);
                environment = CreateEnvironmentBlock(request);

                var startupInfo = new StartupInfoEx();
                startupInfo.StartupInfo.Size = Marshal.SizeOf<StartupInfoEx>();
                // مقابض فارغة صريحة تمنع وراثة stdout معاد التوجيه من المضيف بدل الطرفية الزائفة.
                startupInfo.StartupInfo.Flags = StartfUseStdHandles;
                startupInfo.AttributeList = attributeList;

                var commandLine = new StringBuilder(BuildWindowsCommandLine(request.Executable, request.Arguments));
                if (!CreateProcessW(
                        null,
                        commandLine,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        false,
                        ExtendedStartupInfoPresent | CreateUnicodeEnvironment | CreateSuspended,
                        environment,
                        request.WorkingDirectory,
                        ref startupInfo,
                        out ProcessInformation processInformation))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                WindowsPseudoConsoleProcess session;
                try
                {
                    session = new WindowsPseudoConsoleProcess(
                        new FileStream(inputWrite, FileAccess.Write, bufferSize: 1, isAsync: false),
                        new FileStream(outputRead, FileAccess.Read, bufferSize: 1, isAsync: false),
                        pseudoConsole,
                        attributeList,
                        processInformation,
                        columns,
                        rows);
                }
                catch
                {
                    // لا تُترك عملية معلقة بلا مالك.
                    TerminateProcessNative(processInformation.Process, 1);
                    CloseHandle(processInformation.Thread);
                    CloseHandle(processInformation.Process);
                    throw;
                }

                pseudoConsole = IntPtr.Zero;
                attributeList = IntPtr.Zero;
                session.SelectUtf8CodePage();
                return session;
            }
            catch
            {
                inputWrite.Dispose();
                outputRead.Dispose();
                throw;
            }
            finally
            {
                // الطرفية الزائفة تحتفظ بنسختها من هذين الطرفين.
                inputRead.Dispose();
                outputWrite.Dispose();
                if (environment != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(environment);
                }

                if (attributeList != IntPtr.Zero)
                {
                    DeleteProcThreadAttributeList(attributeList);
                    Marshal.FreeHGlobal(attributeList);
                }

                if (pseudoConsole != IntPtr.Zero)
                {
                    ClosePseudoConsoleNative(pseudoConsole);
                }
            }
        }

        public void Own()
        {
            _treeOwner = ExternalProcessTreeOwner.Attach(Process);
            Resume();
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken) =>
            Process.WaitForExitAsync(cancellationToken);

        public void Terminate() => _treeOwner?.Terminate(exitCode: HostExitCodes.Cancelled);

        public void Kill() => TerminateProcess(Process);

        public void Close()
        {
            _treeOwner?.Dispose();
            _treeOwner = null;
            ClosePseudoConsole();
        }

        private void Resume()
        {
            if (_threadHandle != IntPtr.Zero)
            {
                ResumeThread(_threadHandle);
                CloseHandle(_threadHandle);
                _threadHandle = IntPtr.Zero;
            }
        }

        public void Resize(int columns, int rows)
        {
            IntPtr pseudoConsole = _pseudoConsole;
            if (pseudoConsole != IntPtr.Zero
                && ResizePseudoConsole(pseudoConsole, new Coordinate { X = (short)columns, Y = (short)rows }) == 0)
            {
                Columns = columns;
                Rows = rows;
            }
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

        public int GetExitCode()
        {
            if (!GetExitCodeProcess(_processHandle, out int exitCode))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return exitCode;
        }

        private void ClosePseudoConsole()
        {
            IntPtr pseudoConsole = Interlocked.Exchange(ref _pseudoConsole, IntPtr.Zero);
            if (pseudoConsole != IntPtr.Zero)
            {
                ClosePseudoConsoleNative(pseudoConsole);
            }
        }

        public void Dispose()
        {
            if (_threadHandle != IntPtr.Zero)
            {
                CloseHandle(_threadHandle);
                _threadHandle = IntPtr.Zero;
            }

            Close();
            _input.Dispose();
            _output.Dispose();
            DeleteProcThreadAttributeList(_attributeList);
            Marshal.FreeHGlobal(_attributeList);
            _processHandle.Dispose();
            Process.Dispose();
        }

        /// <summary>
        /// صفحة ترميز الطرفية الزائفة الافتراضية هي OEM النظام، فيتلف الخرج العربي UTF-8 على
        /// معظم الأجهزة. يشغَّل chcp.com صامتًا داخل الطرفية نفسها قبل استئناف البرنامج ليجعل
        /// الإدخال والخرج UTF-8. فشله لا يمنع التشغيل.
        /// </summary>
        private void SelectUtf8CodePage()
        {
            var inheritable = new SecurityAttributes
            {
                Length = Marshal.SizeOf<SecurityAttributes>(),
                InheritHandle = 1
            };
            using SafeFileHandle nul = CreateFileW("NUL", 0xC0000000, 3, ref inheritable, 3, 0, IntPtr.Zero);
            if (nul.IsInvalid)
            {
                return;
            }

            var startupInfo = new StartupInfoEx();
            startupInfo.StartupInfo.Size = Marshal.SizeOf<StartupInfoEx>();
            startupInfo.StartupInfo.Flags = StartfUseStdHandles;
            startupInfo.StartupInfo.StandardInput = nul.DangerousGetHandle();
            startupInfo.StartupInfo.StandardOutput = nul.DangerousGetHandle();
            startupInfo.StartupInfo.StandardError = nul.DangerousGetHandle();
            startupInfo.AttributeList = _attributeList;

            string chcp = Path.Combine(Environment.SystemDirectory, "chcp.com");
            var commandLine = new StringBuilder(BuildWindowsCommandLine(chcp, ["65001"]));
            if (!CreateProcessW(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    true,
                    ExtendedStartupInfoPresent,
                    IntPtr.Zero,
                    null,
                    ref startupInfo,
                    out ProcessInformation helper))
            {
                return;
            }

            WaitForSingleObject(helper.Process, 5000);
            CloseHandle(helper.Thread);
            CloseHandle(helper.Process);
        }

        private static IntPtr CreatePseudoConsoleAttributeList(IntPtr pseudoConsole)
        {
            IntPtr size = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            IntPtr attributeList = Marshal.AllocHGlobal(size);
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
            {
                int error = Marshal.GetLastWin32Error();
                Marshal.FreeHGlobal(attributeList);
                throw new Win32Exception(error);
            }

            if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    PseudoConsoleAttribute,
                    pseudoConsole,
                    IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
                throw new Win32Exception(error);
            }

            return attributeList;
        }

        // Zero يعني وراثة بيئة أربش كما هي.
        private static IntPtr CreateEnvironmentBlock(ExternalProcessRequest request)
        {
            if (request.InheritEnvironment && request.EnvironmentChanges.Count == 0)
            {
                return IntPtr.Zero;
            }

            var variables = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

            var block = new StringBuilder();
            foreach (KeyValuePair<string, string> variable in variables)
            {
                block.Append(variable.Key).Append('=').Append(variable.Value).Append('\0');
            }

            block.Append('\0');
            char[] characters = block.ToString().ToCharArray();
            IntPtr memory = Marshal.AllocHGlobal(characters.Length * sizeof(char));
            Marshal.Copy(characters, 0, memory, characters.Length);
            return memory;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Coordinate
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes
        {
            public int Length;
            public IntPtr SecurityDescriptor;
            public int InheritHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StartupInfo
        {
            public int Size;
            public IntPtr Reserved;
            public IntPtr Desktop;
            public IntPtr Title;
            public int X;
            public int Y;
            public int XSize;
            public int YSize;
            public int XCountChars;
            public int YCountChars;
            public int FillAttribute;
            public int Flags;
            public short ShowWindow;
            public short Reserved2Size;
            public IntPtr Reserved2;
            public IntPtr StandardInput;
            public IntPtr StandardOutput;
            public IntPtr StandardError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StartupInfoEx
        {
            public StartupInfo StartupInfo;
            public IntPtr AttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr Process;
            public IntPtr Thread;
            public int ProcessId;
            public int ThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreatePipe(
            out SafeFileHandle readPipe,
            out SafeFileHandle writePipe,
            IntPtr pipeAttributes,
            int size);

        [DllImport("kernel32.dll")]
        private static extern int CreatePseudoConsole(
            Coordinate size,
            SafeFileHandle input,
            SafeFileHandle output,
            uint flags,
            out IntPtr pseudoConsole);

        [DllImport("kernel32.dll")]
        private static extern int ResizePseudoConsole(IntPtr pseudoConsole, Coordinate size);

        [DllImport("kernel32.dll", EntryPoint = "ClosePseudoConsole")]
        private static extern void ClosePseudoConsoleNative(IntPtr pseudoConsole);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InitializeProcThreadAttributeList(
            IntPtr attributeList,
            int attributeCount,
            int flags,
            ref IntPtr size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateProcThreadAttribute(
            IntPtr attributeList,
            uint flags,
            IntPtr attribute,
            IntPtr value,
            IntPtr size,
            IntPtr previousValue,
            IntPtr returnSize);

        [DllImport("kernel32.dll")]
        private static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcessW(
            string? applicationName,
            StringBuilder commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string? currentDirectory,
            ref StartupInfoEx startupInfo,
            out ProcessInformation processInformation);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            ref SecurityAttributes securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll")]
        private static extern uint ResumeThread(IntPtr thread);

        [DllImport("kernel32.dll", EntryPoint = "TerminateProcess")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcessNative(IntPtr process, uint exitCode);

        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetExitCodeProcess(SafeProcessHandle process, out int exitCode);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
