using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ArbSh.Core.Processes;

public sealed partial class SystemExternalProcessRunner : IInteractiveExternalProcessRunner
{
    // ConPTY متاح منذ Windows 10 1809. طرفية Linux الزائفة لم تُنفذ بعد.
    /// <inheritdoc />
    public bool SupportsInteractive => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763);

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

        WindowsPseudoConsoleProcess session;
        try
        {
            session = WindowsPseudoConsoleProcess.StartSuspended(request);
        }
        catch (Exception exception) when (IsLaunchException(exception))
        {
            return CreateLaunchFailure(stopwatch.Elapsed, exception.Message);
        }

        using (session)
        {
            Task<string> outputTask = session.PumpOutputAsync(outputSink);
            IExternalProcessTreeOwner treeOwner;
            try
            {
                treeOwner = ExternalProcessTreeOwner.Attach(session.Process);
            }
            catch (Exception exception) when (IsOwnershipException(exception))
            {
                TerminateProcess(session.Process);
                session.ClosePseudoConsole();
                string failedOutput = await outputTask.ConfigureAwait(false);
                return CreateOwnershipFailure(stopwatch.Elapsed, failedOutput, string.Empty, exception.Message);
            }

            bool cancelled = false;
            string? cancellationFailure = null;
            try
            {
                using (console.Attach(session.WriteInput))
                {
                    session.Resume();
                    try
                    {
                        await session.Process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        cancelled = true;
                        try
                        {
                            treeOwner.Terminate(exitCode: 130);
                        }
                        catch (Exception exception) when (IsOwnershipException(exception))
                        {
                            cancellationFailure = exception.Message;
                            TerminateProcess(session.Process);
                        }

                        await session.Process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                treeOwner.Dispose();
            }

            // لا تنغلق قناة الخرج بخروج البرنامج؛ إغلاق الطرفية الزائفة يصرف ما بقي ثم ينهيها.
            int exitCode = session.GetExitCode();
            session.ClosePseudoConsole();
            string output = await outputTask.ConfigureAwait(false);
            stopwatch.Stop();

            return cancelled
                ? CreateCancelledResult(
                    stopwatch.Elapsed,
                    output,
                    string.Empty,
                    ExternalProcessTreeOwnershipMode.WindowsJobObject,
                    cancellationFailure)
                : new ExternalProcessResult(
                    ExternalProcessTerminationReason.Exited,
                    exitCode,
                    output,
                    string.Empty,
                    stopwatch.Elapsed,
                    failureMessage: null,
                    ExternalProcessTreeOwnershipMode.WindowsJobObject);
        }
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
    private sealed class WindowsPseudoConsoleProcess : IDisposable
    {
        // shortcut: حجم ثابت بلا تغيير حجم؛ يُربط بأعمدة الواجهة مع محاكي الشبكة الكامل.
        private const short Columns = 120;
        private const short Rows = 30;

        // موجه بلا نهاية سطر («الاسم: ») يجب أن يظهر قبل أن يكتب المستخدم؛ يُصرف كسطر بعد هذه المهلة.
        private static readonly TimeSpan PartialLineFlushDelay = TimeSpan.FromMilliseconds(150);

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

        private WindowsPseudoConsoleProcess(
            FileStream input,
            FileStream output,
            IntPtr pseudoConsole,
            IntPtr attributeList,
            ProcessInformation processInformation)
        {
            _input = input;
            _output = output;
            _pseudoConsole = pseudoConsole;
            _attributeList = attributeList;
            _processHandle = new SafeProcessHandle(processInformation.Process, ownsHandle: true);
            _threadHandle = processInformation.Thread;
            Process = Process.GetProcessById(processInformation.ProcessId);
        }

        public Process Process { get; }

        public static WindowsPseudoConsoleProcess StartSuspended(ExternalProcessRequest request)
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
                    new Coordinate { X = Columns, Y = Rows },
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
                        processInformation);
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

        public void Resume()
        {
            if (_threadHandle != IntPtr.Zero)
            {
                ResumeThread(_threadHandle);
                CloseHandle(_threadHandle);
                _threadHandle = IntPtr.Zero;
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

        public async Task<string> PumpOutputAsync(IExternalProcessOutputSink outputSink)
        {
            var captured = new StringBuilder();
            var filter = new TerminalOutputFilter();
            Decoder decoder = Utf8WithoutBom.GetDecoder();
            byte[] bytes = new byte[4096];
            char[] characters = new char[Utf8WithoutBom.GetMaxCharCount(bytes.Length)];
            bool midLine = false;

            async ValueTask EmitAsync(string text)
            {
                captured.Append(text);
                await outputSink
                    .WriteAsync(
                        new ExternalProcessOutputChunk(ExternalProcessStream.StandardOutput, text),
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }

            while (true)
            {
                Task<int> read = _output.ReadAsync(bytes, 0, bytes.Length);
                if (midLine && await Task.WhenAny(read, Task.Delay(PartialLineFlushDelay)).ConfigureAwait(false) != read)
                {
                    await EmitAsync("\n").ConfigureAwait(false);
                    midLine = false;
                }

                int byteCount;
                try
                {
                    byteCount = await read.ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is IOException or ObjectDisposedException)
                {
                    break;
                }

                if (byteCount == 0)
                {
                    break;
                }

                int characterCount = decoder.GetChars(bytes, 0, byteCount, characters, 0);
                string text = filter.Filter(characters.AsSpan(0, characterCount));
                if (text.Length == 0)
                {
                    continue;
                }

                midLine = text[^1] is not ('\n' or '\r');
                await EmitAsync(text).ConfigureAwait(false);
            }

            return captured.ToString();
        }

        public int GetExitCode()
        {
            if (!GetExitCodeProcess(_processHandle, out int exitCode))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return exitCode;
        }

        public void ClosePseudoConsole()
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

            ClosePseudoConsole();
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
