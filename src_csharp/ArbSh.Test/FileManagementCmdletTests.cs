using ArbSh.Core;

namespace ArbSh.Test;

public sealed class FileManagementCmdletTests
{
    [Fact]
    public void ChangeDirectory_And_GetCurrentDirectory_WorkPerSession()
    {
        string root = CreateTempDirectory();
        string childDirectory = Path.Combine(root, "مشروع");
        Directory.CreateDirectory(childDirectory);

        var sink = new CaptureSink();
        var session = new ShellSessionState(root);
        string processDirectoryBefore = Environment.CurrentDirectory;

        try
        {
            ShellEngine.ExecuteInput("انتقل مشروع", sink, session: session);
            ShellEngine.ExecuteInput("المسار", sink, session: session);

            Assert.Equal(childDirectory, session.CurrentDirectory);
            Assert.Equal(childDirectory, sink.Outputs.Last());
            Assert.Equal(processDirectoryBefore, Environment.CurrentDirectory);
            Assert.Empty(sink.Errors);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void ListDirectory_HidesHiddenEntries_UnlessRequested()
    {
        string root = CreateTempDirectory();
        string visibleDirectory = Path.Combine(root, "مجلد");
        string visibleFile = Path.Combine(root, "ملف.txt");
        // Windows hides by attribute; POSIX hides by a leading dot, which .NET
        // reports as FileAttributes.Hidden and ignores in SetAttributes.
        string hiddenName = OperatingSystem.IsWindows() ? "سري.txt" : ".سري.txt";
        string hiddenFile = Path.Combine(root, hiddenName);

        Directory.CreateDirectory(visibleDirectory);
        File.WriteAllText(visibleFile, "visible");
        File.WriteAllText(hiddenFile, "hidden");
        File.SetAttributes(hiddenFile, File.GetAttributes(hiddenFile) | FileAttributes.Hidden);

        var sink = new CaptureSink();
        var session = new ShellSessionState(root);

        try
        {
            ShellEngine.ExecuteInput("اعرض", sink, session: session);
            Assert.Contains("مجلد/", sink.Outputs);
            Assert.Contains("ملف.txt", sink.Outputs);
            Assert.DoesNotContain(hiddenName, sink.Outputs);

            sink.Clear();
            ShellEngine.ExecuteInput("اعرض -مخفي", sink, session: session);
            Assert.Contains(hiddenName, sink.Outputs);
            Assert.Empty(sink.Errors);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void ChangeDirectory_WhenMissingPath_EmitsArabicError()
    {
        string root = CreateTempDirectory();
        var sink = new CaptureSink();
        var session = new ShellSessionState(root);

        try
        {
            ShellEngine.ExecuteInput("انتقل مسار-غير-موجود", sink, session: session);

            Assert.Contains(sink.Errors, line => line.Contains("المجلد غير موجود", StringComparison.Ordinal));
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ArbSh_FileCmdTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
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
            // Ignore cleanup failures in tests.
        }
    }

    private sealed class CaptureSink : IExecutionSink
    {
        public List<string> Outputs { get; } = [];

        public List<string> Errors { get; } = [];

        public List<string> Warnings { get; } = [];

        public List<string> Debugs { get; } = [];

        public void WriteOutput(string message)
        {
            Outputs.Add(message);
        }

        public void WriteError(string message)
        {
            Errors.Add(message);
        }

        public void WriteWarning(string message)
        {
            Warnings.Add(message);
        }

        public void WriteDebug(string message)
        {
            Debugs.Add(message);
        }

        public void Clear()
        {
            Outputs.Clear();
            Errors.Clear();
            Warnings.Clear();
            Debugs.Clear();
        }
    }
}
