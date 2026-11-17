namespace ArbSh.Core;

/// <summary>
/// High-level shell entry point for hosts.
/// </summary>
public static class ShellEngine
{
    /// <summary>
    /// Parses and executes a single input line.
    /// </summary>
    /// <param name="inputLine">The logical input line.</param>
    /// <param name="sink">The host sink for output.</param>
    /// <param name="options">Execution options.</param>
    /// <param name="session">حالة الجلسة الحالية (مثل المجلد الحالي).</param>
    /// <param name="externalProcessRunner">منفذ العمليات الخارجية، أو التنفيذ النظامي الافتراضي.</param>
    /// <param name="cancellationToken">إشارة إلغاء العملية الخارجية النشطة.</param>
    public static void ExecuteInput(
        string inputLine,
        IExecutionSink sink,
        ExecutionOptions? options = null,
        ShellSessionState? session = null,
        Processes.IExternalProcessRunner? externalProcessRunner = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputLine);
        ArgumentNullException.ThrowIfNull(sink);

        session ??= new ShellSessionState();

        if (Parser.TrySplitBackground(inputLine, out string backgroundCommand))
        {
            StartBackgroundJob(backgroundCommand, sink, options, session, externalProcessRunner);
            return;
        }

        using var sessionScope = ShellSessionContext.Push(session);
        using var scope = CoreConsole.PushSink(sink, options);
        var commands = Parser.Parse(inputLine);
        Executor.Execute(commands, sink, options, externalProcessRunner, cancellationToken);
    }

    /// <summary>
    /// يبدأ الأمر مهمة خلفية بنسخة من الجلسة، فلا يغيّر «انتقل» فيها مجلد المقدمة، وعلى الأنابيب
    /// دائمًا كي لا تنازع المقدمة على الطرفية. يُعلن رقم المهمة عند بدئها وحالتها عند انتهائها.
    /// </summary>
    private static void StartBackgroundJob(
        string command,
        IExecutionSink sink,
        ExecutionOptions? options,
        ShellSessionState session,
        Processes.IExternalProcessRunner? externalProcessRunner)
    {
        var jobSession = new ShellSessionState(session.CurrentDirectory);
        var jobOptions = new ExecutionOptions
        {
            EmitDebug = options?.EmitDebug ?? false,
            EmitWarnings = options?.EmitWarnings ?? true
        };

        BackgroundJob job = session.Jobs.Add(command);
        // يُعلن البدء قبل التشغيل كي لا يسبقه إعلان انتهاء مهمة سريعة.
        sink.WriteOutput($"[{job.Id}] بدأت: {command}");
        session.LastExitCode = 0;
        job.Start(
            token =>
            {
                ExecuteInput(command, sink, jobOptions, jobSession, externalProcessRunner, token);
                return jobSession.LastExitCode;
            },
            finished =>
            {
                if (finished.Error is not null)
                {
                    sink.WriteError($"[{finished.Id}] خطأ: {finished.Error}");
                }

                string state = finished.State == BackgroundJobState.Stopped ? "أُوقفت" : $"انتهت بالرمز {finished.ExitCode}";
                sink.WriteOutput($"[{finished.Id}] {state}: {finished.Command}");
            });
    }
}
