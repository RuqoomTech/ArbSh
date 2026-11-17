using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ArbSh.Core;
using ArbSh.ProcessFixture;

namespace ArbSh.Test;

/// <summary>
/// يثبت عقد arbsh-host-v1: الوسائط ورموز الخروج وأحداث JSONL، داخل العملية وعبر عملية arbsh حقيقية.
/// </summary>
public sealed class HostContractTests
{
    [Theory]
    [InlineData(new string[0], HostMode.Interactive)]
    [InlineData(new[] { "--interactive" }, HostMode.Interactive)]
    [InlineData(new[] { "--version" }, HostMode.Version)]
    [InlineData(new[] { "--help" }, HostMode.Help)]
    [InlineData(new[] { "--command", "اطبع س" }, HostMode.Command)]
    [InlineData(new[] { "--command=اطبع س", "--events=jsonl" }, HostMode.Command)]
    [InlineData(new[] { "--working-dir", "x", "--debug-console" }, HostMode.Interactive)]
    public void TryParse_ValidArguments_SelectsMode(string[] arguments, HostMode expected)
    {
        Assert.True(HostCommandLine.TryParse(arguments, out HostCommandLine commandLine, out string? error));
        Assert.Null(error);
        Assert.Equal(expected, commandLine.Mode);
    }

    [Theory]
    [InlineData("--bogus")]
    [InlineData("--command")]
    [InlineData("--command", "  ")]
    [InlineData("--events=jsonl")]
    [InlineData("--command", "اطبع س", "--events=text")]
    [InlineData("--command", "اطبع س", "--interactive")]
    [InlineData("--version", "--help")]
    [InlineData("--version", "--working-dir", "x")]
    [InlineData("--VERSION")]
    public void TryParse_InvalidArguments_ReportsArabicError(params string[] arguments)
    {
        Assert.False(HostCommandLine.TryParse(arguments, out _, out string? error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryParse_KeepsWorkingDirectoryAndCommandVerbatim()
    {
        Assert.True(HostCommandLine.TryParse(
            ["--working-dir=مجلد عربي", "--command", "اطبع \"مرحبا بالعالم\"", "--events=jsonl"],
            out HostCommandLine commandLine,
            out _));

        Assert.Equal("مجلد عربي", commandLine.WorkingDirectory);
        Assert.Equal("اطبع \"مرحبا بالعالم\"", commandLine.Command);
        Assert.True(commandLine.EventsJsonl);
    }

    [Fact]
    public void Run_Version_PrintsVersionAndContract()
    {
        (int code, string output, string error) = Run(new HostCommandLine(HostMode.Version));

        Assert.Equal(HostExitCodes.Success, code);
        Assert.Equal($"arbsh {CommandModeHost.Version}\narbsh-host-v1\n", output);
        Assert.Empty(error);
        Assert.DoesNotContain('+', CommandModeHost.Version);
    }

    [Fact]
    public void Run_Help_PrintsUsage()
    {
        (int code, string output, _) = Run(new HostCommandLine(HostMode.Help));

        Assert.Equal(HostExitCodes.Success, code);
        Assert.Equal(HostCommandLine.Usage, output);
    }

    [Fact]
    public void Run_BuiltinOutput_IsLogicalOrderWithLf()
    {
        (int code, string output, string error) = Run(Command("اطبع \"مرحبا بالعالم\""));

        Assert.Equal(HostExitCodes.Success, code);
        Assert.Equal("مرحبا بالعالم\n", output);
        Assert.Empty(error);
    }

    [Fact]
    public void Run_BuiltinReportsError_ExitsOne()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"أربش-غير-موجود-{Guid.NewGuid():N}");

        (int code, string output, string error) = Run(Command($"انتقل '{missing}'"));

        Assert.Equal(HostExitCodes.CommandFailed, code);
        Assert.Empty(output);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Run_MissingExternalProgram_ExitsLaunchFailed()
    {
        (int code, _, string error) = Run(Command($"arbsh-no-such-program-{Guid.NewGuid():N}"));

        Assert.Equal(HostExitCodes.LaunchFailed, code);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Run_ExternalProgram_ExitsWithItsOwnCode()
    {
        (int code, string output, string error) = Run(Command(
            $"{Quote(ResolveDotNetHost())} {Quote(FixtureAssemblyPath)} streams خرج خطأ 7"));

        Assert.Equal(7, code);
        Assert.Equal("خرج\n", output);
        Assert.Equal("خطأ\n", error);
    }

    [Fact]
    public void Run_MissingWorkingDirectory_ExitsUsageWithoutEvents()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"أربش-غير-موجود-{Guid.NewGuid():N}");

        (int code, string output, string error) = Run(
            new HostCommandLine(HostMode.Command, missing, "اطبع س", EventsJsonl: true));

        Assert.Equal(HostExitCodes.Usage, code);
        Assert.Empty(output);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Run_Cancelled_ExitsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        (int code, _, _) = Run(
            Command($"{Quote(ResolveDotNetHost())} {Quote(FixtureAssemblyPath)} streams خرج خطأ 0"),
            cancellation.Token);

        Assert.Equal(HostExitCodes.Cancelled, code);
    }

    [Fact]
    public void Run_EventsJsonl_WritesStartOutputErrorAndExit()
    {
        string workingDirectory = Path.GetTempPath();

        (int code, string output, string error) = Run(new HostCommandLine(
            HostMode.Command,
            workingDirectory,
            $"{Quote(ResolveDotNetHost())} {Quote(FixtureAssemblyPath)} streams سلام تنبيه 3",
            EventsJsonl: true));

        Assert.Equal(3, code);
        Assert.Empty(error);
        Assert.EndsWith("\n", output);
        Assert.DoesNotContain("\\u0", output);
        List<JsonElement> events = output.TrimEnd('\n').Split('\n')
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .ToList();

        JsonElement start = events[0];
        Assert.Equal("start", start.GetProperty("type").GetString());
        Assert.Equal("arbsh-events-v1", start.GetProperty("schema_version").GetString());
        Assert.Equal(CommandModeHost.Version, start.GetProperty("arbsh_version").GetString());
        Assert.Equal(Path.GetFullPath(workingDirectory), start.GetProperty("working_directory").GetString());

        Assert.Contains(events, e => e.GetProperty("type").GetString() == "output" && e.GetProperty("text").GetString() == "سلام");
        Assert.Contains(events, e => e.GetProperty("type").GetString() == "error" && e.GetProperty("text").GetString() == "تنبيه");

        JsonElement exit = events[^1];
        Assert.Equal("exit", exit.GetProperty("type").GetString());
        Assert.Equal(3, exit.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Process_CommandMode_WritesUtf8WithoutBomAndReturnsChildCode()
    {
        (int code, byte[] output, string error) = await RunArbShAsync(
            "--command",
            $"{Quote(ResolveDotNetHost())} {Quote(FixtureAssemblyPath)} streams مرحبا خطأ 9");

        Assert.Equal(9, code);
        Assert.Equal(Encoding.UTF8.GetBytes("مرحبا\n"), output);
        Assert.Equal("خطأ\n", error);
    }

    [Fact]
    public async Task Process_EventsAndVersion_FollowContract()
    {
        (int code, byte[] output, _) = await RunArbShAsync("--command", "اطبع \"أ ب\"", "--events=jsonl");

        Assert.Equal(HostExitCodes.Success, code);
        string[] lines = Encoding.UTF8.GetString(output).TrimEnd('\n').Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("{\"type\":\"output\",\"text\":\"أ ب\"}", lines[1]);
        Assert.Equal("{\"type\":\"exit\",\"code\":0}", lines[2]);

        (int versionCode, byte[] version, _) = await RunArbShAsync("--version");
        Assert.Equal(HostExitCodes.Success, versionCode);
        Assert.Equal($"arbsh {CommandModeHost.Version}\narbsh-host-v1\n", Encoding.UTF8.GetString(version));
    }

    [Fact]
    public async Task Process_UnknownOption_ExitsUsage()
    {
        (int code, byte[] output, string error) = await RunArbShAsync("--bogus");

        Assert.Equal(HostExitCodes.Usage, code);
        Assert.Empty(output);
        Assert.Contains("--bogus", error);
        Assert.Contains("arbsh --version", error);
    }

    [Fact]
    public async Task Process_InteractiveOnPipes_KeepsArabicInLogicalOrderForTheHost()
    {
        (int code, byte[] output, string error) = await RunArbShWithInputAsync(
            "اطبع 'مرحبا عالم'\nاخرج\n",
            "--interactive");

        string text = Encoding.UTF8.GetString(output);
        Assert.Equal(HostExitCodes.Success, code);
        Assert.Contains("مرحبا عالم", text);
        Assert.Contains("مرحباً بكم في أربش", text);
        Assert.DoesNotContain("تحذير", text);
        Assert.Empty(error);
    }

    private static HostCommandLine Command(string command) => new(HostMode.Command, Command: command);

    private static (int Code, string Output, string Error) Run(
        HostCommandLine commandLine,
        CancellationToken cancellationToken = default)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int code = CommandModeHost.Run(commandLine, output, error, cancellationToken: cancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    private static Task<(int Code, byte[] Output, string Error)> RunArbShAsync(params string[] arguments) =>
        RunArbShWithInputAsync(string.Empty, arguments);

    private static async Task<(int Code, byte[] Output, string Error)> RunArbShWithInputAsync(
        string input,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(ResolveDotNetHost())
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = new UTF8Encoding(false),
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "arbsh.dll"));
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        await process.StandardInput.BaseStream.WriteAsync(new UTF8Encoding(false).GetBytes(input));
        process.StandardInput.Close();
        using var output = new MemoryStream();
        Task copy = process.StandardOutput.BaseStream.CopyToAsync(output);
        Task<string> error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await process.WaitForExitAsync(timeout.Token);
        await copy;
        return (process.ExitCode, output.ToArray(), await error);
    }

    private static string FixtureAssemblyPath => typeof(ProcessFixtureMarker).Assembly.Location;

    private static string Quote(string value) => $"'{value}'";

    private static string ResolveDotNetHost()
    {
        string? configuredHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configuredHost) && File.Exists(configuredHost))
        {
            return configuredHost;
        }

        string hostName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        string hostPath = Path.Combine(
            Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..")),
            hostName);
        return File.Exists(hostPath) ? hostPath : hostName;
    }
}
