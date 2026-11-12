namespace ArbSh.Core;

/// <summary>
/// وضع تشغيل أربش كما تحدده وسائط سطر الأوامر.
/// </summary>
public enum HostMode
{
    /// <summary>جلسة تفاعلية تقرأ الأسطر من الإدخال القياسي.</summary>
    Interactive,

    /// <summary>أمر واحد غير تفاعلي ثم الخروج برمزه.</summary>
    Command,

    /// <summary>طباعة الإصدار والعقد.</summary>
    Version,

    /// <summary>طباعة طريقة الاستخدام.</summary>
    Help
}

/// <summary>
/// وسائط سطر أوامر أربش المجمدة في <c>arbsh-host-v1</c>.
/// </summary>
/// <param name="Mode">وضع التشغيل.</param>
/// <param name="WorkingDirectory">مجلد العمل الابتدائي كما كُتب، أو null.</param>
/// <param name="Command">الأمر المنطقي في وضع <see cref="HostMode.Command"/>.</param>
/// <param name="EventsJsonl">هل يُكتب الخرج أحداث JSON سطرًا سطرًا.</param>
/// <param name="DebugConsole">هل تُعرض رسائل التشخيص الداخلية في الجلسة التفاعلية.</param>
public sealed record HostCommandLine(
    HostMode Mode,
    string? WorkingDirectory = null,
    string? Command = null,
    bool EventsJsonl = false,
    bool DebugConsole = false)
{
    /// <summary>اسم العقد الذي يطبقه هذا السطح.</summary>
    public const string ContractName = "arbsh-host-v1";

    /// <summary>اسم مخطط أحداث JSON.</summary>
    public const string EventsSchema = "arbsh-events-v1";

    /// <summary>نص المساعدة الذي يطبعه <c>arbsh --help</c>.</summary>
    public const string Usage =
        "الاستخدام:\n" +
        "  arbsh [--working-dir <مسار>] [--interactive]\n" +
        "  arbsh [--working-dir <مسار>] --command <أمر> [--events=jsonl]\n" +
        "  arbsh --version\n" +
        "  arbsh --help\n";

    /// <summary>
    /// يحلل الوسائط. عند الفشل يعيد false ورسالة عربية، ويخرج المضيف بالرمز <see cref="HostExitCodes.Usage"/>.
    /// </summary>
    public static bool TryParse(IReadOnlyList<string> arguments, out HostCommandLine commandLine, out string? error)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        commandLine = new HostCommandLine(HostMode.Interactive);
        error = null;
        string? workingDirectory = null;
        string? command = null;
        bool interactive = false;
        bool events = false;
        bool debug = false;
        bool version = false;
        bool help = false;

        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            string? inlineValue = null;
            string name = argument;
            int equals = argument.IndexOf('=');
            if (argument.StartsWith("--", StringComparison.Ordinal) && equals > 0)
            {
                name = argument[..equals];
                inlineValue = argument[(equals + 1)..];
            }

            switch (name)
            {
                case "--working-dir":
                case "--command":
                    string? value = inlineValue;
                    if (value is null)
                    {
                        if (index + 1 >= arguments.Count)
                        {
                            error = $"الخيار {name} يحتاج قيمة.";
                            return false;
                        }

                        value = arguments[++index];
                    }

                    if (name == "--working-dir")
                    {
                        workingDirectory = value;
                    }
                    else
                    {
                        command = value;
                    }

                    break;

                case "--events":
                    if (inlineValue != "jsonl")
                    {
                        error = "القيمة الوحيدة المدعومة للخيار --events هي jsonl.";
                        return false;
                    }

                    events = true;
                    break;

                case "--interactive" when inlineValue is null:
                    interactive = true;
                    break;

                case "--debug-console" when inlineValue is null:
                    debug = true;
                    break;

                case "--version" when inlineValue is null:
                    version = true;
                    break;

                case "--help" when inlineValue is null:
                    help = true;
                    break;

                default:
                    error = $"خيار غير معروف: {argument}";
                    return false;
            }
        }

        bool anythingElse = workingDirectory is not null || command is not null || interactive || events || debug;
        if ((version || help) && (anythingElse || (version && help)))
        {
            error = "يُستخدم --version أو --help وحده.";
            return false;
        }

        if (command is not null && interactive)
        {
            error = "لا يجتمع --command مع --interactive.";
            return false;
        }

        if (command is null && events)
        {
            error = "الخيار --events يتطلب --command.";
            return false;
        }

        if (command is not null && string.IsNullOrWhiteSpace(command))
        {
            error = "الخيار --command يحتاج أمرًا غير فارغ.";
            return false;
        }

        HostMode mode = version ? HostMode.Version
            : help ? HostMode.Help
            : command is not null ? HostMode.Command
            : HostMode.Interactive;
        commandLine = new HostCommandLine(mode, workingDirectory, command, events, debug);
        return true;
    }
}
