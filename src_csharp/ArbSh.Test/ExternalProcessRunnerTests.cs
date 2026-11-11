using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ArbSh.Core.Processes;

namespace ArbSh.Test;

public sealed class ExternalProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_PreservesArabicPathsArgumentsEnvironmentAndInput()
    {
        using var fixture = FixtureCopy.Create();
        string workingDirectory = Path.Combine(fixture.RootDirectory, "مجلد عمل عربي");
        Directory.CreateDirectory(workingDirectory);
        string[] logicalArguments = [string.Empty, "وسيط عربي", "value with spaces", "; & | < >"];
        const string environmentValue = "قيمة بيئية عربية";
        const string standardInput = "مدخل عربي\nسطر ثان";
        var runner = new SystemExternalProcessRunner();
        var request = new ExternalProcessRequest(
            ResolveDotNetHost(),
            [fixture.AssemblyPath, "inspect", .. logicalArguments],
            workingDirectory,
            new Dictionary<string, string?>
            {
                ["ARBSH_PHASE2_VALUE"] = environmentValue
            },
            standardInputMode: ExternalProcessInputMode.Text,
            standardInputText: standardInput);

        ExternalProcessResult result = await runner.RunAsync(request);

        Assert.Equal(ExternalProcessTerminationReason.Exited, result.TerminationReason);
        Assert.Equal(ExpectedOwnershipMode, result.TreeOwnershipMode);
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Succeeded);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.Null(result.FailureMessage);

        InspectionPayload payload = Assert.IsType<InspectionPayload>(
            JsonSerializer.Deserialize<InspectionPayload>(result.StandardOutput));
        Assert.Equal(logicalArguments, payload.Arguments);
        Assert.Equal(Path.GetFullPath(workingDirectory), Path.GetFullPath(payload.WorkingDirectory));
        Assert.Equal(environmentValue, payload.EnvironmentValue);
        Assert.Equal(standardInput, payload.StandardInput);
    }

    [Fact]
    public async Task RunAsync_CapturesSeparateStreamsAndPreservesExitCode()
    {
        using var fixture = FixtureCopy.Create();
        var runner = new SystemExternalProcessRunner();
        var request = new ExternalProcessRequest(
            ResolveDotNetHost(),
            [fixture.AssemblyPath, "streams", "خرج عربي", "خطأ عربي", "37"]);

        ExternalProcessResult result = await runner.RunAsync(request);

        Assert.Equal(ExternalProcessTerminationReason.Exited, result.TerminationReason);
        Assert.Equal(ExpectedOwnershipMode, result.TreeOwnershipMode);
        Assert.Equal(37, result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.Equal("خرج عربي", result.StandardOutput);
        Assert.Equal("خطأ عربي", result.StandardError);
        Assert.Null(result.FailureMessage);
    }

    [Fact]
    public async Task RunStreamingAsync_DeliversOutputBeforeExitAndStillCapturesTheResult()
    {
        using var fixture = FixtureCopy.Create();
        string readyFile = Path.Combine(fixture.RootDirectory, "البث-جاهز.txt");
        string releaseFile = Path.Combine(fixture.RootDirectory, "اسمح-بالخروج.txt");
        var runner = new SystemExternalProcessRunner();
        var outputSink = new RecordingOutputSink();
        var request = new ExternalProcessRequest(
            ResolveDotNetHost(),
            [fixture.AssemblyPath, "live-streams", readyFile, releaseFile]);

        Task<ExternalProcessResult> runningProcess = runner.RunStreamingAsync(request, outputSink);
        ExternalProcessOutputChunk firstChunk = await outputSink.FirstChunk
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(ExternalProcessStream.StandardOutput, firstChunk.Stream);
        Assert.Contains("خرج حي أول", firstChunk.Text, StringComparison.Ordinal);
        Assert.False(runningProcess.IsCompleted);

        await File.WriteAllTextAsync(releaseFile, "تابع");
        ExternalProcessResult result = await runningProcess.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(ExternalProcessTerminationReason.Exited, result.TerminationReason);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"خرج حي أول{Environment.NewLine}خرج حي أخير", result.StandardOutput);
        Assert.Equal($"خطأ حي{Environment.NewLine}", result.StandardError);
        Assert.Contains(
            outputSink.Chunks,
            chunk => chunk.Stream is ExternalProcessStream.StandardError
                && chunk.Text.Contains("خطأ حي", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_DefaultInputModeClosesStandardInput()
    {
        using var fixture = FixtureCopy.Create();
        var runner = new SystemExternalProcessRunner();
        var request = new ExternalProcessRequest(
            ResolveDotNetHost(),
            [fixture.AssemblyPath, "stdin-length"]);

        ExternalProcessResult result = await runner.RunAsync(request);

        Assert.Equal(ExternalProcessTerminationReason.Exited, result.TerminationReason);
        Assert.Equal(ExpectedOwnershipMode, result.TreeOwnershipMode);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("0", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_CanStartWithCleanEnvironmentAndApplyExplicitValues()
    {
        using var fixture = FixtureCopy.Create();
        string inheritedName = $"ARBSH_PHASE2_PARENT_{Guid.NewGuid():N}";
        string explicitName = $"ARBSH_PHASE2_CHILD_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(inheritedName, "parent-only");

        try
        {
            var runner = new SystemExternalProcessRunner();
            var request = new ExternalProcessRequest(
                ResolveDotNetHost(),
                [fixture.AssemblyPath, "environment", inheritedName, explicitName],
                environmentChanges: new Dictionary<string, string?>
                {
                    [explicitName] = "قيمة صريحة"
                },
                inheritEnvironment: false);

            ExternalProcessResult result = await runner.RunAsync(request);
            string?[] values = Assert.IsType<string?[]>(
                JsonSerializer.Deserialize<string?[]>(result.StandardOutput));

            Assert.Equal(ExternalProcessTerminationReason.Exited, result.TerminationReason);
            Assert.Equal(0, result.ExitCode);
            Assert.Collection(
                values,
                value => Assert.Null(value),
                value => Assert.Equal("قيمة صريحة", value));
        }
        finally
        {
            Environment.SetEnvironmentVariable(inheritedName, null);
        }
    }

    [Fact]
    public async Task RunAsync_NullEnvironmentChangeRemovesInheritedValue()
    {
        using var fixture = FixtureCopy.Create();
        string variableName = $"ARBSH_PHASE2_REMOVE_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(variableName, "remove-me");

        try
        {
            var runner = new SystemExternalProcessRunner();
            var request = new ExternalProcessRequest(
                ResolveDotNetHost(),
                [fixture.AssemblyPath, "environment", variableName],
                environmentChanges: new Dictionary<string, string?>
                {
                    [variableName] = null
                });

            ExternalProcessResult result = await runner.RunAsync(request);
            string?[] values = Assert.IsType<string?[]>(
                JsonSerializer.Deserialize<string?[]>(result.StandardOutput));

            Assert.Equal(ExternalProcessTerminationReason.Exited, result.TerminationReason);
            Assert.Collection(values, value => Assert.Null(value));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    [Fact]
    public async Task RunAsync_MissingExecutableReturnsLaunchFailure()
    {
        var runner = new SystemExternalProcessRunner();
        string missingExecutable = Path.Combine(
            Path.GetTempPath(),
            $"أربش-غير-موجود-{Guid.NewGuid():N}",
            "برنامج مفقود");
        var request = new ExternalProcessRequest(missingExecutable);

        ExternalProcessResult result = await runner.RunAsync(request);

        Assert.Equal(ExternalProcessTerminationReason.LaunchFailed, result.TerminationReason);
        Assert.Equal(ExternalProcessTreeOwnershipMode.None, result.TreeOwnershipMode);
        Assert.Null(result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureMessage));
    }

    [Fact]
    public async Task RunAsync_CancellationTerminatesTheProcess()
    {
        using var fixture = FixtureCopy.Create();
        string readyFile = Path.Combine(fixture.RootDirectory, "جاهز.txt");
        var runner = new SystemExternalProcessRunner();
        var request = new ExternalProcessRequest(
            ResolveDotNetHost(),
            [fixture.AssemblyPath, "wait", readyFile]);
        using var cancellation = new CancellationTokenSource();

        Task<ExternalProcessResult> runningProcess = runner.RunAsync(request, cancellation.Token);
        await WaitForFileAsync(readyFile, TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        ExternalProcessResult result = await runningProcess.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(ExternalProcessTerminationReason.Cancelled, result.TerminationReason);
        Assert.Equal(ExpectedOwnershipMode, result.TreeOwnershipMode);
        Assert.Null(result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.Equal("ready", result.StandardOutput);
        Assert.Null(result.FailureMessage);
    }

    [Fact]
    public async Task RunAsync_AlreadyCancelledDoesNotLaunch()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var runner = new SystemExternalProcessRunner();
        var request = new ExternalProcessRequest("برنامج-لن-يعمل");

        ExternalProcessResult result = await runner.RunAsync(request, cancellation.Token);

        Assert.Equal(ExternalProcessTerminationReason.Cancelled, result.TerminationReason);
        Assert.Equal(ExternalProcessTreeOwnershipMode.None, result.TreeOwnershipMode);
        Assert.Null(result.ExitCode);
        Assert.Null(result.FailureMessage);
    }

    [Fact]
    public void Request_RejectsInputTextOutsideTextMode()
    {
        Assert.Throws<ArgumentException>(() => new ExternalProcessRequest(
            "برنامج",
            standardInputText: "نص غير مسموح"));
    }

    [Fact]
    public void Request_SnapshotsArgumentsAndEnvironmentChanges()
    {
        var arguments = new List<string> { "الأول" };
        var environment = new Dictionary<string, string?>
        {
            ["ARBSH_PHASE2_SNAPSHOT"] = "قبل"
        };
        var request = new ExternalProcessRequest(
            "برنامج",
            arguments,
            environmentChanges: environment);

        arguments[0] = "الثاني";
        environment["ARBSH_PHASE2_SNAPSHOT"] = "بعد";

        Assert.Equal(["الأول"], request.Arguments);
        Assert.Equal("قبل", request.EnvironmentChanges["ARBSH_PHASE2_SNAPSHOT"]);
    }

    [Fact]
    public async Task RunAsync_CancellationTerminatesDescendantProcess()
    {
        using var fixture = FixtureCopy.Create();
        string triggerFile = Path.Combine(fixture.RootDirectory, "ابدأ-الابن.txt");
        string childPidFile = Path.Combine(fixture.RootDirectory, "معرف-الابن.txt");
        var runner = new SystemExternalProcessRunner();
        var request = new ExternalProcessRequest(
            ResolveDotNetHost(),
            [fixture.AssemblyPath, "spawn-tree", triggerFile, childPidFile]);
        using var cancellation = new CancellationTokenSource();

        Task<ExternalProcessResult> runningProcess = runner.RunAsync(request, cancellation.Token);
        await File.WriteAllTextAsync(triggerFile, "ابدأ");
        await WaitForFileAsync(childPidFile, TimeSpan.FromSeconds(10));
        int childPid = int.Parse(
            await File.ReadAllTextAsync(childPidFile),
            System.Globalization.CultureInfo.InvariantCulture);

        cancellation.Cancel();
        ExternalProcessResult result = await runningProcess.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitForProcessExitAsync(childPid, TimeSpan.FromSeconds(10));

        Assert.Equal(ExternalProcessTerminationReason.Cancelled, result.TerminationReason);
        Assert.Equal(ExpectedOwnershipMode, result.TreeOwnershipMode);
        Assert.False(IsProcessAlive(childPid));
    }

    [Fact]
    public async Task RunAsync_NormalRootExitCleansUpBackgroundDescendant()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            return;
        }

        using var fixture = FixtureCopy.Create();
        string childPidFile = Path.Combine(fixture.RootDirectory, "معرف-ابن-الخلفية.txt");
        var runner = new SystemExternalProcessRunner();
        var request = new ExternalProcessRequest(
            ResolveDotNetHost(),
            [fixture.AssemblyPath, "spawn-and-exit", childPidFile]);

        ExternalProcessResult result = await runner.RunAsync(request).WaitAsync(TimeSpan.FromSeconds(10));
        int childPid = int.Parse(
            await File.ReadAllTextAsync(childPidFile),
            System.Globalization.CultureInfo.InvariantCulture);
        await WaitForProcessExitAsync(childPid, TimeSpan.FromSeconds(10));

        Assert.Equal(ExternalProcessTerminationReason.Exited, result.TerminationReason);
        Assert.Equal(ExpectedOwnershipMode, result.TreeOwnershipMode);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("parent-exited", result.StandardOutput);
        Assert.False(IsProcessAlive(childPid));
    }

    private static ExternalProcessTreeOwnershipMode ExpectedOwnershipMode =>
        OperatingSystem.IsWindows()
            ? ExternalProcessTreeOwnershipMode.WindowsJobObject
            : OperatingSystem.IsLinux()
                ? ExternalProcessTreeOwnershipMode.PosixProcessGroup
                : ExternalProcessTreeOwnershipMode.DotNetProcessTree;

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

    private static async Task WaitForProcessExitAsync(int processId, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (IsProcessAlive(processId))
        {
            if (stopwatch.Elapsed >= timeout)
            {
                throw new TimeoutException($"بقيت العملية الابنة {processId} بعد إلغاء الشجرة.");
            }

            await Task.Delay(25);
        }
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private sealed class RecordingOutputSink : IExternalProcessOutputSink
    {
        private readonly object _sync = new();
        private readonly TaskCompletionSource<ExternalProcessOutputChunk> _firstChunk =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<ExternalProcessOutputChunk> Chunks { get; } = [];

        public Task<ExternalProcessOutputChunk> FirstChunk => _firstChunk.Task;

        public ValueTask WriteAsync(
            ExternalProcessOutputChunk chunk,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                Chunks.Add(chunk);
            }

            _firstChunk.TrySetResult(chunk);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task RunInteractiveAsync_ShowsPromptBeforeInputAndPreservesArabicExitCodeAndEnvironment()
    {
        // طرفية Linux الزائفة لم تُنفذ بعد.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = FixtureCopy.Create();
        var runner = new SystemExternalProcessRunner();
        var console = new InteractiveConsole();
        // لا يُرسل الاسم إلا بعد ظهور الموجه غير المنتهي بسطر؛ عبر الأنابيب يعلق هذا إلى الأبد.
        var sink = new PromptAnsweringSink("اسمك؟", () => console.TrySendLine("عمر"));
        const string quotedArgument = "وسيط \"مقتبس\" بمسافات";
        var request = new ExternalProcessRequest(
            ResolveDotNetHost(),
            [fixture.AssemblyPath, "console-echo", fixture.RootDirectory, quotedArgument],
            fixture.RootDirectory,
            new Dictionary<string, string?>
            {
                ["ARBSH_PHASE2_VALUE"] = "قيمة عربية"
            });

        Assert.True(runner.SupportsInteractive);
        ExternalProcessResult result = await runner
            .RunInteractiveAsync(request, console, sink)
            .WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(ExternalProcessTerminationReason.Exited, result.TerminationReason);
        Assert.Equal(5, result.ExitCode);
        Assert.Equal(ExternalProcessTreeOwnershipMode.WindowsJobObject, result.TreeOwnershipMode);
        Assert.Contains("مرحبا عمر", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("tty=True", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("cwd=True", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("env=قيمة عربية", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains($"arg={quotedArgument}", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain((char)27, result.StandardOutput);
        Assert.False(console.IsAttached);
    }

    [Fact]
    public async Task RunInteractiveAsync_CancellationEndsAProgramWaitingForInput()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = FixtureCopy.Create();
        using var cancellation = new CancellationTokenSource();
        var runner = new SystemExternalProcessRunner();
        var console = new InteractiveConsole();
        var sink = new PromptAnsweringSink("اسمك؟", cancellation.Cancel);
        var request = new ExternalProcessRequest(
            ResolveDotNetHost(),
            [fixture.AssemblyPath, "console-echo"],
            fixture.RootDirectory);

        ExternalProcessResult result = await runner
            .RunInteractiveAsync(request, console, sink, cancellation.Token)
            .WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(ExternalProcessTerminationReason.Cancelled, result.TerminationReason);
        Assert.DoesNotContain("مرحبا", result.StandardOutput, StringComparison.Ordinal);
        Assert.False(console.IsAttached);
    }

    [Fact]
    public async Task RunInteractiveAsync_MissingProgramIsALaunchFailure()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var runner = new SystemExternalProcessRunner();
        var request = new ExternalProcessRequest($"برنامج-مفقود-{Guid.NewGuid():N}");

        ExternalProcessResult result = await runner.RunInteractiveAsync(
            request,
            new InteractiveConsole(),
            new PromptAnsweringSink("لا شيء", () => { }));

        Assert.Equal(ExternalProcessTerminationReason.LaunchFailed, result.TerminationReason);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureMessage));
    }

    [Fact]
    public void BuildWindowsCommandLine_QuotesArgumentsForCommandLineToArgv()
    {
        string commandLine = SystemExternalProcessRunner.BuildWindowsCommandLine(
            "C:\\مجلد عربي\\برنامج.exe",
            ["plain", string.Empty, "with space", "q\"uote", "trail\\", "sp ace\\", "a\\\"b"]);

        Assert.Equal(
            "\"C:\\مجلد عربي\\برنامج.exe\" plain \"\" \"with space\" \"q\\\"uote\" trail\\ \"sp ace\\\\\" \"a\\\\\\\"b\"",
            commandLine);
    }

    private sealed class PromptAnsweringSink(string prompt, Action answer) : IExternalProcessOutputSink
    {
        private readonly System.Text.StringBuilder _text = new();
        private bool _answered;

        public ValueTask WriteAsync(
            ExternalProcessOutputChunk chunk,
            CancellationToken cancellationToken = default)
        {
            bool answerNow;
            lock (_text)
            {
                _text.Append(chunk.Text);
                answerNow = !_answered && _text.ToString().Contains(prompt, StringComparison.Ordinal);
                _answered |= answerNow;
            }

            if (answerNow)
            {
                answer();
            }

            return ValueTask.CompletedTask;
        }
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

    private sealed class FixtureCopy : IDisposable
    {
        private FixtureCopy(string rootDirectory, string assemblyPath)
        {
            RootDirectory = rootDirectory;
            AssemblyPath = assemblyPath;
        }

        public string RootDirectory { get; }

        public string AssemblyPath { get; }

        public static FixtureCopy Create()
        {
            var testOutputDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            string targetFramework = testOutputDirectory.Name;
            string configuration = testOutputDirectory.Parent?.Name
                ?? throw new InvalidOperationException("تعذر تحديد إعداد بناء الاختبارات.");
            string sourceRoot = testOutputDirectory.Parent?.Parent?.Parent?.Parent?.FullName
                ?? throw new InvalidOperationException("تعذر تحديد مجلد مشاريع ArbSh.");
            string fixtureOutputDirectory = Path.Combine(
                sourceRoot,
                "ArbSh.ProcessFixture",
                "bin",
                configuration,
                targetFramework);
            string rootDirectory = Path.Combine(
                Path.GetTempPath(),
                $"أربش مرحلة 2 {Guid.NewGuid():N}");
            Directory.CreateDirectory(rootDirectory);

            foreach (string sourcePath in Directory.EnumerateFiles(
                fixtureOutputDirectory,
                "ArbSh.ProcessFixture.*",
                SearchOption.TopDirectoryOnly))
            {
                string destinationPath = Path.Combine(rootDirectory, Path.GetFileName(sourcePath));
                File.Copy(sourcePath, destinationPath);
            }

            string assemblyPath = Path.Combine(rootDirectory, "ArbSh.ProcessFixture.dll");
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException("تعذر تجهيز العملية المساعدة للاختبارات.", assemblyPath);
            }

            return new FixtureCopy(rootDirectory, assemblyPath);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
            catch
            {
                // لا ينبغي أن يحجب فشل تنظيف مجلد مؤقت نتيجة الاختبار الأساسية.
            }
        }
    }

    private sealed record InspectionPayload(
        string[] Arguments,
        string WorkingDirectory,
        string? EnvironmentValue,
        string StandardInput);
}
