using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using ArbSh.Core.Processes;

namespace ArbSh.Core;

/// <summary>
/// ينفذ الأوضاع غير التفاعلية من <c>arbsh-host-v1</c>: الإصدار والمساعدة وأمر واحد.
/// يكتب نصًا منطقيًا بترميز UTF-8 ونهايات أسطر LF، دون إعادة ترتيب بصري للعربية.
/// </summary>
public static class CommandModeHost
{
    private static readonly JsonSerializerOptions EventJson = new()
    {
        // النص العربي يبقى مقروءًا في الأحداث؛ الخرج ليس HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// إصدار أربش بلا لاحقة البناء.
    /// </summary>
    public static string Version { get; } =
        (typeof(CommandModeHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(CommandModeHost).Assembly.GetName().Version?.ToString()
            ?? "0.0.0").Split('+')[0];

    /// <summary>
    /// ينفذ الوضع المطلوب ويعيد رمز الخروج. أخطاء الاستخدام تُكتب نصًا عاديًا على قناة الأخطاء
    /// حتى مع <c>--events=jsonl</c>، لأن الأحداث لا تبدأ إلا مع تنفيذ الأمر.
    /// </summary>
    public static int Run(
        HostCommandLine commandLine,
        TextWriter output,
        TextWriter error,
        IExternalProcessRunner? externalProcessRunner = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        switch (commandLine.Mode)
        {
            case HostMode.Version:
                output.Write($"arbsh {Version}\n{HostCommandLine.ContractName}\n");
                return HostExitCodes.Success;

            case HostMode.Help:
                output.Write(HostCommandLine.Usage);
                return HostExitCodes.Success;

            case HostMode.Command:
                break;

            default:
                throw new ArgumentException("الوضع التفاعلي يديره المضيف نفسه.", nameof(commandLine));
        }

        string? workingDirectory = null;
        if (commandLine.WorkingDirectory is not null)
        {
            try
            {
                workingDirectory = Path.GetFullPath(commandLine.WorkingDirectory);
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
            {
                error.Write($"مسار مجلد العمل غير صالح: {commandLine.WorkingDirectory}\n");
                return HostExitCodes.Usage;
            }

            if (!Directory.Exists(workingDirectory))
            {
                error.Write($"مجلد العمل غير موجود: {commandLine.WorkingDirectory}\n");
                return HostExitCodes.Usage;
            }
        }

        var session = new ShellSessionState(workingDirectory);
        IExecutionSink sink = commandLine.EventsJsonl
            ? new EventSink(output)
            : new TextSink(output, error);
        if (sink is EventSink events)
        {
            events.Write(new
            {
                type = "start",
                schema_version = HostCommandLine.EventsSchema,
                arbsh_version = Version,
                working_directory = session.CurrentDirectory
            });
        }

        int exitCode;
        try
        {
            ShellEngine.ExecuteInput(
                commandLine.Command!,
                sink,
                new ExecutionOptions { EmitDebug = false },
                session,
                externalProcessRunner,
                cancellationToken);
            // لا تبقى عملية بعد أربش: ينتظر مهام «&» قبل الخروج، ويوقفها عند الإلغاء.
            session.Jobs.WaitAll(cancellationToken, TimeSpan.FromSeconds(5));
            exitCode = session.LastExitCode;
        }
        catch (Exception exception)
        {
            sink.WriteError($"خطأ: {exception.Message}");
            exitCode = HostExitCodes.CommandFailed;
        }

        (sink as EventSink)?.Write(new { type = "exit", code = exitCode });
        return exitCode;
    }

    private sealed class TextSink(TextWriter output, TextWriter error) : IExecutionSink
    {
        private readonly object _sync = new();

        public void WriteOutput(string message) => WriteLine(output, message);

        public void WriteError(string message) => WriteLine(error, message);

        public void WriteWarning(string message) => WriteLine(error, message);

        public void WriteDebug(string message)
        {
        }

        private void WriteLine(TextWriter writer, string message)
        {
            lock (_sync)
            {
                writer.Write(message);
                writer.Write('\n');
            }
        }
    }

    private sealed class EventSink(TextWriter output) : IExecutionSink
    {
        private readonly object _sync = new();

        public void WriteOutput(string message) => Write(new { type = "output", text = message });

        public void WriteError(string message) => Write(new { type = "error", text = message });

        public void WriteWarning(string message) => Write(new { type = "warning", text = message });

        public void WriteDebug(string message)
        {
        }

        public void Write(object payload)
        {
            string line = JsonSerializer.Serialize(payload, EventJson);
            lock (_sync)
            {
                output.Write(line);
                output.Write('\n');
            }
        }
    }
}
