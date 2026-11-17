using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ArbSh.Core;
using ArbSh.Core.Processes;
using ArbSh.ProcessFixture;

namespace ArbSh.Test;

public sealed class ExternalCommandIntegrationTests
{
    [Fact]
    public void Parser_PreservesOrderedExternalArgumentsAndQuotedExecutable()
    {
        List<List<ParsedCommand>> statements = Parser.Parse(
            "'C:\\Program Files\\أداة.exe' --flag -c '' \"وسيط عربي\" prefix\"وسط\"suffix");

        ParsedCommand command = Assert.Single(Assert.Single(statements));
        Assert.Equal("C:\\Program Files\\أداة.exe", command.CommandName);
        Assert.Equal(
            ["--flag", "-c", string.Empty, "وسيط عربي", "prefixوسطsuffix"],
            command.InvocationArguments);
    }

    [Fact]
    public void ExecuteInput_RunsExternalCommandWithArabicWorkingDirectoryAndArguments()
    {
        string root = CreateTempDirectory();
        string workingDirectory = Path.Combine(root, "مجلد عمل عربي");
        Directory.CreateDirectory(workingDirectory);
        var session = new ShellSessionState(workingDirectory);
        var sink = new CaptureSink();
        string command = JoinCommand(
            ResolveDotNetHost(),
            FixtureAssemblyPath,
            "inspect",
            string.Empty,
            "وسيط عربي",
            "value with spaces",
            "; & | < >");

        try
        {
            ShellEngine.ExecuteInput(command, sink, session: session);

            string json = Assert.Single(sink.Outputs);
            InspectionPayload payload = Assert.IsType<InspectionPayload>(
                JsonSerializer.Deserialize<InspectionPayload>(json));
            Assert.Equal(
                [string.Empty, "وسيط عربي", "value with spaces", "; & | < >"],
                payload.Arguments);
            Assert.Equal(Path.GetFullPath(workingDirectory), Path.GetFullPath(payload.WorkingDirectory));
            Assert.Equal(string.Empty, payload.StandardInput);
            Assert.Empty(sink.Errors);
            Assert.Equal(0, session.LastExitCode);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void ExecuteInput_PreservesExternalStreamsAndNonZeroExitCode()
    {
        var session = new ShellSessionState();
        var sink = new CaptureSink();
        string command = JoinCommand(
            ResolveDotNetHost(),
            FixtureAssemblyPath,
            "streams",
            "خرج عربي",
            "خطأ عربي",
            "37");

        ShellEngine.ExecuteInput(command, sink, session: session);

        Assert.Equal(["خرج عربي"], sink.Outputs);
        Assert.Equal(["خطأ عربي"], sink.Errors);
        Assert.Equal(37, session.LastExitCode);
    }

    [Fact]
    public async Task ExecuteInput_PublishesExternalOutputBeforeTheProcessExits()
    {
        string root = CreateTempDirectory();
        string readyFile = Path.Combine(root, "المحرك-جاهز.txt");
        string releaseFile = Path.Combine(root, "حرر-المحرك.txt");
        var session = new ShellSessionState(root);
        var sink = new CaptureSink();
        string command = JoinCommand(
            ResolveDotNetHost(),
            FixtureAssemblyPath,
            "live-streams",
            readyFile,
            releaseFile);

        try
        {
            Task execution = Task.Run(() => ShellEngine.ExecuteInput(
                command,
                sink,
                session: session));

            string firstOutput = await sink.FirstOutput.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("خرج حي أول", firstOutput);
            Assert.False(execution.IsCompleted);

            await File.WriteAllTextAsync(releaseFile, "تابع");
            await execution.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(["خرج حي أول", "خرج حي أخير"], sink.Outputs);
            Assert.Equal(["خطأ حي"], sink.Errors);
            Assert.Equal(0, session.LastExitCode);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void ExecuteInput_AppliesShellRedirectionToExternalStreams()
    {
        string root = CreateTempDirectory();
        string outputPath = Path.Combine(root, "خرج عربي.txt");
        string errorPath = Path.Combine(root, "خطأ عربي.txt");
        var session = new ShellSessionState(root);
        var sink = new CaptureSink();
        string command = JoinCommand(
            ResolveDotNetHost(),
            FixtureAssemblyPath,
            "streams",
            "خرج إلى ملف",
            "خطأ إلى ملف",
            "23");

        try
        {
            ShellEngine.ExecuteInput(
                $"{command} > {QuoteArgument(outputPath)} 2> {QuoteArgument(errorPath)}",
                sink,
                session: session);

            Assert.Empty(sink.Outputs);
            Assert.Empty(sink.Errors);
            Assert.Equal("خرج إلى ملف" + Environment.NewLine, File.ReadAllText(outputPath));
            Assert.Equal("خطأ إلى ملف" + Environment.NewLine, File.ReadAllText(errorPath));
            Assert.Equal(23, session.LastExitCode);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void ExecuteInput_PipesBuiltinOutputToExternalStandardInput()
    {
        var session = new ShellSessionState();
        var sink = new CaptureSink();
        string externalCommand = JoinCommand(
            ResolveDotNetHost(),
            FixtureAssemblyPath,
            "inspect");

        ShellEngine.ExecuteInput($"اطبع نص | {externalCommand}", sink, session: session);

        string json = Assert.Single(sink.Outputs);
        InspectionPayload payload = Assert.IsType<InspectionPayload>(
            JsonSerializer.Deserialize<InspectionPayload>(json));
        Assert.Equal("نص" + Environment.NewLine, payload.StandardInput);
        Assert.Empty(sink.Errors);
        Assert.Equal(0, session.LastExitCode);
    }

    [Fact]
    public void ExecuteInput_PipesExternalOutputToBuiltin()
    {
        var session = new ShellSessionState();
        var sink = new CaptureSink();
        string externalCommand = JoinCommand(
            ResolveDotNetHost(),
            FixtureAssemblyPath,
            "streams",
            "خرج من برنامج",
            string.Empty,
            "0");

        ShellEngine.ExecuteInput($"{externalCommand} | اطبع", sink, session: session);

        Assert.Equal(["خرج من برنامج"], sink.Outputs);
        Assert.Empty(sink.Errors);
        Assert.Equal(0, session.LastExitCode);
    }

    [Fact]
    public void ExecuteInput_PrefersArabicBuiltinOverExternalRunner()
    {
        var runner = new FailingIfCalledRunner();
        var sink = new CaptureSink();
        var session = new ShellSessionState();

        ShellEngine.ExecuteInput(
            "اطبع قيمة",
            sink,
            session: session,
            externalProcessRunner: runner);

        Assert.False(runner.WasCalled);
        Assert.Equal(["قيمة"], sink.Outputs);
        Assert.Equal(0, session.LastExitCode);
    }

    [Fact]
    public void ExecuteInput_ClassifiesMissingExternalCommandAs127()
    {
        string commandName = $"برنامج-غير-موجود-{Guid.NewGuid():N}";
        var sink = new CaptureSink();
        var session = new ShellSessionState();

        ShellEngine.ExecuteInput(commandName, sink, session: session);

        Assert.Empty(sink.Outputs);
        Assert.Contains(
            sink.Errors,
            line => line.Contains("تعذر تشغيل الأمر الخارجي", StringComparison.Ordinal)
                && line.Contains(commandName, StringComparison.Ordinal));
        Assert.Equal(127, session.LastExitCode);
    }

    [Fact]
    public async Task ExecuteInput_PropagatesCancellationAndSets130()
    {
        string root = CreateTempDirectory();
        string readyFile = Path.Combine(root, "جاهز.txt");
        var sink = new CaptureSink();
        var session = new ShellSessionState(root);
        using var cancellation = new CancellationTokenSource();
        string command = JoinCommand(
            ResolveDotNetHost(),
            FixtureAssemblyPath,
            "wait",
            readyFile);

        try
        {
            Task execution = Task.Run(() => ShellEngine.ExecuteInput(
                command,
                sink,
                session: session,
                cancellationToken: cancellation.Token));

            await WaitForFileAsync(readyFile, TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await execution.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Contains("ready", sink.Outputs);
            Assert.Contains(
                sink.Errors,
                line => line.Contains("أُلغي الأمر الخارجي", StringComparison.Ordinal));
            Assert.Equal(130, session.LastExitCode);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task BackgroundJob_RunsWhileTheForegroundContinuesAndStopsOnRequest()
    {
        string root = CreateTempDirectory();
        string readyFile = Path.Combine(root, "مهمة جاهزة.txt");
        var sink = new CaptureSink();
        var session = new ShellSessionState(root);
        string command = JoinCommand(ResolveDotNetHost(), FixtureAssemblyPath, "wait", readyFile);

        try
        {
            ShellEngine.ExecuteInput(command + " &", sink, session: session);

            BackgroundJob job = Assert.Single(session.Jobs.List());
            Assert.Equal(command, job.Command);
            Assert.Equal(0, session.LastExitCode);
            await WaitForFileAsync(readyFile, TimeSpan.FromSeconds(10));

            // المقدمة حرة والمهمة ما زالت تعمل.
            ShellEngine.ExecuteInput("المهام", sink, session: session);
            Assert.Contains($"[1] تعمل: {command}", Snapshot(sink));

            ShellEngine.ExecuteInput("أوقف ١", sink, session: session);
            await job.Completion.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(BackgroundJobState.Stopped, job.State);
            Assert.Equal(130, job.ExitCode);
            Assert.Equal($"[1] بدأت: {command}", Snapshot(sink)[0]);
            Assert.Contains($"[1] أُوقفت: {command}", Snapshot(sink));

            ShellEngine.ExecuteInput("أوقف 1", sink, session: session);
            Assert.Contains("المهمة [1] منتهية بالفعل.", Snapshot(sink));
            ShellEngine.ExecuteInput("أوقف ٧", sink, session: session);
            Assert.Contains(sink.Errors, line => line.Contains("لا توجد مهمة خلفية بالرقم", StringComparison.Ordinal));
        }
        finally
        {
            session.Jobs.StopAll(TimeSpan.FromSeconds(10));
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task BackgroundJob_UsesACopyOfTheSessionAndReportsItsExitCode()
    {
        string root = CreateTempDirectory();
        string child = Path.Combine(root, "فرعي");
        Directory.CreateDirectory(child);
        var sink = new CaptureSink();
        var session = new ShellSessionState(root);

        try
        {
            ShellEngine.ExecuteInput($"انتقل '{child}'; المسار &", sink, session: session);
            await Assert.Single(session.Jobs.List()).Completion.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(Path.GetFullPath(root), Path.GetFullPath(session.CurrentDirectory));
            Assert.Contains(Path.GetFullPath(child), Snapshot(sink));
            Assert.Contains($"[1] انتهت بالرمز 0: انتقل '{child}'; المسار", Snapshot(sink));
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void CommandMode_WaitsForBackgroundJobsBeforeExiting()
    {
        string command = JoinCommand(ResolveDotNetHost(), FixtureAssemblyPath, "streams", "خرج المهمة", "", "0");
        var output = new StringWriter();

        int code = CommandModeHost.Run(new HostCommandLine(HostMode.Command, Command: command + " &"), output, new StringWriter());

        Assert.Equal(0, code);
        Assert.Contains("خرج المهمة", output.ToString(), StringComparison.Ordinal);
        Assert.Contains($"[1] انتهت بالرمز 0: {command}", output.ToString(), StringComparison.Ordinal);
    }

    private static List<string> Snapshot(CaptureSink sink) => sink.OutputSnapshot();

    private static string FixtureAssemblyPath => typeof(ProcessFixtureMarker).Assembly.Location;

    private static string JoinCommand(params string[] arguments)
    {
        return string.Join(" ", arguments.Select(QuoteArgument));
    }

    private static string QuoteArgument(string value)
    {
        if (value.Contains('\''))
        {
            throw new InvalidOperationException("لا تدعم أداة الاختبار الفاصلة العليا داخل الوسيط.");
        }

        return $"'{value}'";
    }

    private static string ResolveDotNetHost()
    {
        string? configuredHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configuredHost) && File.Exists(configuredHost))
        {
            return configuredHost;
        }

        string hostName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        string dotNetRoot = Path.GetFullPath(Path.Combine(
            RuntimeEnvironment.GetRuntimeDirectory(),
            "..",
            "..",
            ".."));
        string hostPath = Path.Combine(dotNetRoot, hostName);
        return File.Exists(hostPath) ? hostPath : hostName;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"أربش مرحلة 3 {Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task WaitForFileAsync(string path, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!File.Exists(path))
        {
            if (stopwatch.Elapsed >= timeout)
            {
                throw new TimeoutException($"لم تنشئ العملية المساعدة ملف الجاهزية: {path}");
            }

            await Task.Delay(25);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // لا يحجب فشل تنظيف ملف مؤقت نتيجة الاختبار الأساسية.
        }
    }

    private sealed class FailingIfCalledRunner : IExternalProcessRunner
    {
        public bool WasCalled { get; private set; }

        public Task<ExternalProcessResult> RunAsync(
            ExternalProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            throw new InvalidOperationException("لا ينبغي استدعاء المنفذ الخارجي لأمر داخلي.");
        }
    }

    private sealed class CaptureSink : IExecutionSink
    {
        private readonly object _sync = new();
        private readonly TaskCompletionSource<string> _firstOutput =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Outputs { get; } = [];

        public List<string> Errors { get; } = [];

        public List<string> Warnings { get; } = [];

        public List<string> Debugs { get; } = [];

        public Task<string> FirstOutput => _firstOutput.Task;

        public List<string> OutputSnapshot()
        {
            lock (_sync)
            {
                return [.. Outputs];
            }
        }

        public void WriteOutput(string message)
        {
            lock (_sync)
            {
                Outputs.Add(message);
            }

            _firstOutput.TrySetResult(message);
        }

        public void WriteError(string message)
        {
            lock (_sync)
            {
                Errors.Add(message);
            }
        }

        public void WriteWarning(string message)
        {
            lock (_sync)
            {
                Warnings.Add(message);
            }
        }

        public void WriteDebug(string message)
        {
            lock (_sync)
            {
                Debugs.Add(message);
            }
        }
    }

    private sealed record InspectionPayload(
        string[] Arguments,
        string WorkingDirectory,
        string? EnvironmentValue,
        string StandardInput);
}
