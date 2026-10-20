using ArbSh.Core;
using ArbSh.Core.Eco;
using ArbSh.Core.Processes;

namespace ArbSh.Test;

public sealed class EcoRunTests
{
    private const string IntegrationVariable = "ARBSH_ECO_INTEGRATION";

    [Fact]
    public void Plan_SingleFile_CompilesIntoTemporaryDirectoryThenRunsWithRemainingArguments()
    {
        string root = CreateTempDirectory();
        string source = Path.Combine(root, "برنامج.baa");
        File.WriteAllText(source, string.Empty);

        try
        {
            using EcoRunPlan plan = EcoRunPlanner.Plan(["برنامج.baa", "أول", "--ثاني"], root);

            Assert.Null(plan.Error);
            Assert.NotNull(plan.TemporaryDirectory);
            Assert.True(Directory.Exists(plan.TemporaryDirectory));
            Assert.Equal(2, plan.Steps.Count);

            ExternalProcessRequest compile = plan.Steps[0].Request;
            Assert.Equal(EcoRunPlanner.BaaExecutable, compile.Executable);
            Assert.Equal(3, compile.Arguments.Count);
            Assert.Equal(source, compile.Arguments[0]);
            Assert.Equal("-o", compile.Arguments[1]);
            string program = compile.Arguments[2];
            Assert.Equal(plan.TemporaryDirectory, Path.GetDirectoryName(program));
            Assert.StartsWith("برنامج", Path.GetFileName(program), StringComparison.Ordinal);
            Assert.Equal(root, compile.WorkingDirectory);

            ExternalProcessRequest run = plan.Steps[1].Request;
            Assert.Equal(program, run.Executable);
            Assert.Equal(["أول", "--ثاني"], run.Arguments);
            Assert.Equal(root, run.WorkingDirectory);
            Assert.Equal(ExternalProcessInputMode.Closed, run.StandardInputMode);
            Assert.Equal("برنامج.baa", plan.Steps[1].DisplayName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Plan_ArabicExtensionSingleFile_PassesPipelineInputToProgramOnly()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "مرحبا.باء"), string.Empty);

        try
        {
            using EcoRunPlan plan = EcoRunPlanner.Plan(["مرحبا.باء"], root, "إدخال");

            Assert.Null(plan.Error);
            Assert.Equal(ExternalProcessInputMode.Closed, plan.Steps[0].Request.StandardInputMode);
            Assert.Equal(ExternalProcessInputMode.Text, plan.Steps[1].Request.StandardInputMode);
            Assert.Equal("إدخال", plan.Steps[1].Request.StandardInputText);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Plan_MissingSourceFile_FailsWithoutSteps()
    {
        string root = CreateTempDirectory();

        try
        {
            using EcoRunPlan plan = EcoRunPlanner.Plan(["مفقود.baa"], root);

            Assert.Equal("ملف باء غير موجود: مفقود.baa", plan.Error);
            Assert.Empty(plan.Steps);
            Assert.Null(plan.TemporaryDirectory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Plan_DirectoryWithoutManifest_FailsWithoutSteps()
    {
        string root = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(root, "مجلد"));

        try
        {
            using EcoRunPlan plan = EcoRunPlanner.Plan(["مجلد"], root);

            Assert.NotNull(plan.Error);
            Assert.Contains(EcoRunPlanner.ProjectManifestName, plan.Error, StringComparison.Ordinal);
            Assert.Empty(plan.Steps);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Plan_NoArgumentsOutsideProject_Fails()
    {
        string root = CreateTempDirectory();

        try
        {
            using EcoRunPlan plan = EcoRunPlanner.Plan([], root);

            Assert.NotNull(plan.Error);
            Assert.StartsWith("لا يوجد مشروع تكوين", plan.Error, StringComparison.Ordinal);
            Assert.Empty(plan.Steps);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Plan_FromProjectSubdirectory_RunsTakweenFromProjectRootWithAllArguments()
    {
        string root = CreateTempDirectory();
        string project = Path.Combine(root, "مشروع عربي");
        string nested = Path.Combine(project, "المصدر", "داخلي");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(project, EcoRunPlanner.ProjectManifestName), string.Empty);

        try
        {
            using EcoRunPlan plan = EcoRunPlanner.Plan(["أول", "ثانٍ"], nested);

            Assert.Null(plan.Error);
            Assert.Null(plan.TemporaryDirectory);
            EcoRunStep step = Assert.Single(plan.Steps);
            Assert.Equal(EcoRunPlanner.TakweenExecutable, step.Request.Executable);
            Assert.Equal(["تشغيل", "أول", "ثانٍ"], step.Request.Arguments);
            Assert.Equal(project, step.Request.WorkingDirectory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Plan_ExplicitProjectDirectory_RunsTakweenWithRemainingArguments()
    {
        string root = CreateTempDirectory();
        string project = Path.Combine(root, "مشروع");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, EcoRunPlanner.ProjectManifestName), string.Empty);

        try
        {
            using EcoRunPlan plan = EcoRunPlanner.Plan(["مشروع", "وسيط"], root);

            Assert.Null(plan.Error);
            EcoRunStep step = Assert.Single(plan.Steps);
            Assert.Equal(["تشغيل", "وسيط"], step.Request.Arguments);
            Assert.Equal(project, step.Request.WorkingDirectory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Dispose_DeletesTemporaryDirectory()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "برنامج.baa"), string.Empty);

        try
        {
            EcoRunPlan plan = EcoRunPlanner.Plan(["برنامج.baa"], root);
            string temporaryDirectory = plan.TemporaryDirectory!;
            File.WriteAllText(Path.Combine(temporaryDirectory, "ناتج"), "x");

            plan.Dispose();

            Assert.False(Directory.Exists(temporaryDirectory));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExecuteInput_SingleFile_CompilesThenRunsAndPropagatesProgramExitCode()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "برنامج.baa"), string.Empty);
        var runner = new RecordingRunner(request => request.Executable == EcoRunPlanner.BaaExecutable
            ? Exited(0)
            : Exited(7, standardOutput: "مرحبا من البرنامج"));
        var sink = new CaptureSink();
        var session = new ShellSessionState(root);

        try
        {
            ShellEngine.ExecuteInput("تشغيل برنامج.baa وسيط", sink, session: session, externalProcessRunner: runner);

            Assert.Equal(2, runner.Requests.Count);
            Assert.Equal(EcoRunPlanner.BaaExecutable, runner.Requests[0].Executable);
            Assert.Equal(["وسيط"], runner.Requests[1].Arguments);
            Assert.Contains("مرحبا من البرنامج", sink.Outputs);
            Assert.Equal(7, session.LastExitCode);
            Assert.False(Directory.Exists(Path.GetDirectoryName(runner.Requests[1].Executable)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExecuteInput_CompileFailure_StopsBeforeRunAndKeepsCompilerDiagnostics()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "برنامج.baa"), string.Empty);
        var runner = new RecordingRunner(_ => Exited(1, standardError: "خطأ: رمز غير متوقع"));
        var sink = new CaptureSink();
        var session = new ShellSessionState(root);

        try
        {
            ShellEngine.ExecuteInput("تشغيل برنامج.baa", sink, session: session, externalProcessRunner: runner);

            ExternalProcessRequest request = Assert.Single(runner.Requests);
            Assert.Equal(EcoRunPlanner.BaaExecutable, request.Executable);
            Assert.Contains("خطأ: رمز غير متوقع", sink.Errors);
            Assert.Equal(1, session.LastExitCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExecuteInput_Project_PropagatesTakweenExitCode()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, EcoRunPlanner.ProjectManifestName), string.Empty);
        var runner = new RecordingRunner(_ => Exited(4));
        var sink = new CaptureSink();
        var session = new ShellSessionState(root);

        try
        {
            ShellEngine.ExecuteInput("تشغيل", sink, session: session, externalProcessRunner: runner);

            ExternalProcessRequest request = Assert.Single(runner.Requests);
            Assert.Equal(EcoRunPlanner.TakweenExecutable, request.Executable);
            Assert.Equal(["تشغيل"], request.Arguments);
            Assert.Equal(4, session.LastExitCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExecuteInput_PlanError_ReportsArabicErrorWithUsageExitCode()
    {
        string root = CreateTempDirectory();
        var runner = new RecordingRunner(_ => Exited(0));
        var sink = new CaptureSink();
        var session = new ShellSessionState(root);

        try
        {
            ShellEngine.ExecuteInput("تشغيل مفقود.baa", sink, session: session, externalProcessRunner: runner);

            Assert.Empty(runner.Requests);
            Assert.Contains("ملف باء غير موجود: مفقود.baa", sink.Errors);
            Assert.Equal(EcoRunPlanner.UsageExitCode, session.LastExitCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Integration_RunsRealBaaFileAndTakweenProject()
    {
        // يحتاج baa وتكوين ونظم على PATH؛ تفعّله مهمة CI البيئية.
        if (Environment.GetEnvironmentVariable(IntegrationVariable) != "1")
        {
            return;
        }

        string root = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(root, "مرحبا.baa"),
                "صحيح الرئيسية() {\n    اطبع \"مرحباً من تشغيل\".\n    إرجع ٣.\n}\n");
            var fileSink = new CaptureSink();
            var fileSession = new ShellSessionState(root);

            ShellEngine.ExecuteInput("تشغيل مرحبا.baa", fileSink, session: fileSession);

            Assert.True(
                fileSink.Outputs.Any(line => line.Contains("مرحباً من تشغيل", StringComparison.Ordinal)),
                string.Join(Environment.NewLine, fileSink.Outputs.Concat(fileSink.Errors)));
            Assert.Equal(3, fileSession.LastExitCode);

            string project = Path.Combine(root, "مشروع");
            string sources = Path.Combine(project, "المصدر");
            Directory.CreateDirectory(sources);
            File.WriteAllText(
                Path.Combine(project, EcoRunPlanner.ProjectManifestName),
                "الاسم: مشروع_تشغيل\nالإصدار: 1.0.0\nالنوع: تنفيذي\nالمصدر: المصدر/\nالمخرج: بناء/\nملفات: الرئيسية.baa\n");
            File.WriteAllText(
                Path.Combine(sources, "الرئيسية.baa"),
                "صحيح الرئيسية() {\n    اطبع \"مرحباً من مشروع\".\n    إرجع ٠.\n}\n");
            var projectSink = new CaptureSink();
            var projectSession = new ShellSessionState(sources);

            ShellEngine.ExecuteInput("تشغيل", projectSink, session: projectSession);

            Assert.True(
                projectSink.Outputs.Any(line => line.Contains("مرحباً من مشروع", StringComparison.Ordinal)),
                string.Join(Environment.NewLine, projectSink.Outputs.Concat(projectSink.Errors)));
            Assert.Equal(0, projectSession.LastExitCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), $"أربش تشغيل {Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static ExternalProcessResult Exited(int exitCode, string standardOutput = "", string standardError = "") =>
        new(ExternalProcessTerminationReason.Exited, exitCode, standardOutput, standardError, TimeSpan.Zero, null,
            ExternalProcessTreeOwnershipMode.None);

    private sealed class RecordingRunner(Func<ExternalProcessRequest, ExternalProcessResult> respond)
        : IExternalProcessRunner
    {
        private readonly object _sync = new();

        public List<ExternalProcessRequest> Requests { get; } = [];

        public Task<ExternalProcessResult> RunAsync(ExternalProcessRequest request, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                Requests.Add(request);
            }

            return Task.FromResult(respond(request));
        }
    }

    private sealed class CaptureSink : IExecutionSink
    {
        private readonly object _sync = new();

        public List<string> Outputs { get; } = [];

        public List<string> Errors { get; } = [];

        public void WriteOutput(string message)
        {
            lock (_sync)
            {
                Outputs.Add(message);
            }
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
        }

        public void WriteDebug(string message)
        {
        }
    }
}
