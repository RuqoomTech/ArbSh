using System.Text;
using System.Text.Json;
using System.Diagnostics;

namespace ArbSh.ProcessFixture;

/// <summary>
/// علامة عامة تمكّن الاختبارات من تحديد ناتج مشروع العملية المساعدة.
/// </summary>
public static class ProcessFixtureMarker;

internal static class Program
{
    public static async Task<int> Main(string[] arguments)
    {
        // قبل ضبط الترميز عمدًا: يثبت أن الطرفية الزائفة نفسها تسلّم UTF-8 للبرامج التي لا تضبطه.
        if (arguments is ["console-echo", ..])
        {
            return ConsoleEcho(arguments[1..]);
        }

        if (arguments is ["console-size"])
        {
            return ConsoleSize();
        }

        if (arguments is ["vt-screen"])
        {
            return VtScreen();
        }

        if (arguments is ["read-keys"])
        {
            return ReadKeys();
        }

        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        if (arguments.Length == 0)
        {
            Console.Error.Write("missing fixture command");
            return 2;
        }

        return arguments[0] switch
        {
            "inspect" => await InspectAsync(arguments[1..]).ConfigureAwait(false),
            "streams" => WriteStreams(arguments[1..]),
            "live-streams" => await WriteLiveStreamsAsync(arguments[1..]).ConfigureAwait(false),
            "environment" => WriteEnvironment(arguments[1..]),
            "wait" => await WaitAsync(arguments[1..]).ConfigureAwait(false),
            "spawn-tree" => await SpawnTreeAsync(arguments[1..]).ConfigureAwait(false),
            "spawn-and-exit" => await SpawnAndExitAsync(arguments[1..]).ConfigureAwait(false),
            "child-wait" => await ChildWaitAsync(arguments[1..]).ConfigureAwait(false),
            "stdin-length" => await WriteStandardInputLengthAsync().ConfigureAwait(false),
            _ => UnknownCommand(arguments[0])
        };
    }

    private static int ConsoleEcho(string[] arguments)
    {
        // موجه بلا نهاية سطر ثم قراءة: لا يظهر عبر الأنابيب، ويظهر فورًا عبر طرفية.
        Console.Out.Write("اسمك؟ ");
        string? line = Console.In.ReadLine();
        Console.Out.WriteLine($"مرحبا {line}");
        Console.Out.WriteLine($"tty={!Console.IsInputRedirected && !Console.IsOutputRedirected}");
        Console.Out.WriteLine($"cwd={arguments.Length > 0 && string.Equals(Environment.CurrentDirectory, arguments[0], StringComparison.OrdinalIgnoreCase)}");
        Console.Out.WriteLine($"env={Environment.GetEnvironmentVariable("ARBSH_PHASE2_VALUE")}");
        Console.Out.WriteLine($"arg={(arguments.Length > 1 ? arguments[1] : null)}");
        return 5;
    }

    private static int ConsoleSize()
    {
        // يطبع حجم طرفيته، وينتظر سطرًا يغير المضيف الحجم قبله، ثم يطبع الحجم الجديد.
        Console.Out.WriteLine($"size={Console.WindowWidth}x{Console.WindowHeight}");
        Console.Out.Write("غيّر الحجم؟ ");
        Console.In.ReadLine();
        Console.Out.WriteLine($"size={Console.WindowWidth}x{Console.WindowHeight}");
        return 0;
    }

    private static int ReadKeys()
    {
        // يقرأ مفاتيح منفردة دون انتظار Enter، كما يفعل محرر أو قائمة تفاعلية.
        Console.Out.Write("مفاتيح؟ ");
        var keys = new List<string>();
        for (int index = 0; index < 3; index++)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            keys.Add(key.KeyChar is >= 'a' and <= 'z' ? key.KeyChar.ToString() : key.Key.ToString());
        }

        Console.Out.WriteLine();
        Console.Out.WriteLine("keys=" + string.Join(',', keys));
        return 0;
    }

    private static int VtScreen()
    {
        // عودة بداية السطر تكتب فوقه، واللون يبقى SGR، والشاشة البديلة لا تبقى في السجل.
        Console.Out.Write("abc\rX\n");
        Console.Out.Write("\u001b[31mأحمر\u001b[0m\n");
        Console.Out.Write("\u001b[?1049hشاشة-بديلة\u001b[?1049l");
        Console.Out.Write("تم\n");
        Console.Out.Flush();
        return 0;
    }

    private static async Task<int> InspectAsync(string[] arguments)
    {
        string standardInput = await Console.In.ReadToEndAsync().ConfigureAwait(false);
        var payload = new InspectionPayload(
            arguments,
            Environment.CurrentDirectory,
            Environment.GetEnvironmentVariable("ARBSH_PHASE2_VALUE"),
            standardInput);

        Console.Out.Write(JsonSerializer.Serialize(payload));
        return 0;
    }

    private static int WriteStreams(string[] arguments)
    {
        if (arguments.Length != 3 || !int.TryParse(arguments[2], out int exitCode))
        {
            return 2;
        }

        Console.Out.Write(arguments[0]);
        Console.Error.Write(arguments[1]);
        return exitCode;
    }

    private static async Task<int> WriteLiveStreamsAsync(string[] arguments)
    {
        if (arguments.Length != 2)
        {
            return 2;
        }

        string readyFile = arguments[0];
        string releaseFile = arguments[1];
        Console.Out.WriteLine("خرج حي أول");
        Console.Out.Flush();
        await WriteSignalFileAsync(readyFile, "جاهز").ConfigureAwait(false);

        while (!File.Exists(releaseFile))
        {
            await Task.Delay(10).ConfigureAwait(false);
        }

        Console.Error.WriteLine("خطأ حي");
        Console.Error.Flush();
        Console.Out.Write("خرج حي أخير");
        Console.Out.Flush();
        return 0;
    }

    private static int WriteEnvironment(string[] variableNames)
    {
        string?[] values = variableNames
            .Select(Environment.GetEnvironmentVariable)
            .ToArray();
        Console.Out.Write(JsonSerializer.Serialize(values));
        return 0;
    }

    private static async Task<int> WaitAsync(string[] arguments)
    {
        if (arguments.Length != 1)
        {
            return 2;
        }

        Console.Out.Write("ready");
        Console.Out.Flush();
        await WriteSignalFileAsync(arguments[0], "جاهز").ConfigureAwait(false);
        await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> WriteStandardInputLengthAsync()
    {
        string standardInput = await Console.In.ReadToEndAsync().ConfigureAwait(false);
        Console.Out.Write(standardInput.Length);
        return 0;
    }

    private static async Task<int> SpawnTreeAsync(string[] arguments)
    {
        if (arguments.Length != 2)
        {
            return 2;
        }

        string triggerFile = arguments[0];
        string childPidFile = arguments[1];
        while (!File.Exists(triggerFile))
        {
            await Task.Delay(10).ConfigureAwait(false);
        }

        using Process child = StartFixtureChild("child-wait", childPidFile);
        await WriteSignalFileAsync(
            childPidFile,
            child.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);
        Console.Out.Write("tree-ready");
        Console.Out.Flush();
        await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> ChildWaitAsync(string[] arguments)
    {
        if (arguments.Length != 1)
        {
            return 2;
        }

        await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> SpawnAndExitAsync(string[] arguments)
    {
        if (arguments.Length != 1)
        {
            return 2;
        }

        using Process child = StartFixtureChild("child-wait", arguments[0]);
        await WriteSignalFileAsync(
            arguments[0],
            child.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);
        Console.Out.Write("parent-exited");
        Console.Out.Flush();
        return 0;
    }

    private static Process StartFixtureChild(string command, string argument)
    {
        string processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("تعذر تحديد مضيف العملية المساعدة.");
        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (string.Equals(
            Path.GetFileNameWithoutExtension(processPath),
            "dotnet",
            StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(typeof(ProcessFixtureMarker).Assembly.Location);
        }

        startInfo.ArgumentList.Add(command);
        startInfo.ArgumentList.Add(argument);
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("رفض النظام بدء العملية الابنة المساعدة.");
    }

    private static async Task WriteSignalFileAsync(string path, string contents)
    {
        string temporaryPath = $"{path}.{Environment.ProcessId}.tmp";
        await File.WriteAllTextAsync(temporaryPath, contents, Encoding.UTF8).ConfigureAwait(false);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.Write($"unknown fixture command: {command}");
        return 2;
    }

    private sealed record InspectionPayload(
        string[] Arguments,
        string WorkingDirectory,
        string? EnvironmentValue,
        string StandardInput);
}
