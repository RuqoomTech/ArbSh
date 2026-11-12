using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace ArbSh.Core.Processes;

/// <summary>
/// تنفيذ نظامي للعملية المنظمة باستخدام <see cref="ProcessStartInfo.ArgumentList"/> مباشرة.
/// </summary>
public sealed partial class SystemExternalProcessRunner : IStreamingExternalProcessRunner
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: false);

    /// <inheritdoc />
    public async Task<ExternalProcessResult> RunAsync(
        ExternalProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        return await RunCoreAsync(request, outputSink: null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ExternalProcessResult> RunStreamingAsync(
        ExternalProcessRequest request,
        IExternalProcessOutputSink outputSink,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outputSink);
        return await RunCoreAsync(request, outputSink, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ExternalProcessResult> RunCoreAsync(
        ExternalProcessRequest request,
        IExternalProcessOutputSink? outputSink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        if (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return CreateCancelledResult(
                stopwatch.Elapsed,
                string.Empty,
                string.Empty,
                ExternalProcessTreeOwnershipMode.None,
                failureMessage: null);
        }

        ProcessStartInfo startInfo;
        try
        {
            startInfo = CreateStartInfo(request);
        }
        catch (PlatformNotSupportedException exception)
        {
            stopwatch.Stop();
            return CreateOwnershipFailure(
                stopwatch.Elapsed,
                string.Empty,
                string.Empty,
                exception.Message);
        }
        catch (Exception exception) when (IsLaunchException(exception))
        {
            stopwatch.Stop();
            return CreateLaunchFailure(stopwatch.Elapsed, exception.Message);
        }

        using var process = new Process
        {
            StartInfo = startInfo
        };

        try
        {
            if (!process.Start())
            {
                stopwatch.Stop();
                return CreateLaunchFailure(stopwatch.Elapsed, "رفض النظام بدء العملية دون تقديم تفاصيل إضافية.");
            }
        }
        catch (Exception exception) when (IsLaunchException(exception))
        {
            stopwatch.Stop();
            return CreateLaunchFailure(stopwatch.Elapsed, exception.Message);
        }

        Task<string> standardOutputTask = CaptureStreamAsync(
            process.StandardOutput,
            ExternalProcessStream.StandardOutput,
            outputSink);
        Task<string> standardErrorTask = CaptureStreamAsync(
            process.StandardError,
            ExternalProcessStream.StandardError,
            outputSink);
        Task standardInputTask = WriteStandardInputAsync(process, request);
        IExternalProcessTreeOwner treeOwner;

        try
        {
            treeOwner = ExternalProcessTreeOwner.Attach(process);
        }
        catch (Exception exception) when (IsOwnershipException(exception))
        {
            string failureMessage = exception.Message;
            try
            {
                TerminateProcess(process);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception cleanupException) when (IsOwnershipException(cleanupException))
            {
                failureMessage += $" تعذر أيضًا إيقاف العملية بعد فشل الملكية: {cleanupException.Message}";
            }

            await ObserveStandardInputAsync(standardInputTask, process, cancelled: true).ConfigureAwait(false);
            string failedOutput = await standardOutputTask.ConfigureAwait(false);
            string failedError = await standardErrorTask.ConfigureAwait(false);
            stopwatch.Stop();
            return CreateOwnershipFailure(
                stopwatch.Elapsed,
                failedOutput,
                failedError,
                failureMessage);
        }

        ExternalProcessTreeOwnershipMode treeOwnershipMode = treeOwner.Mode;
        bool cancelled = false;
        string? cancellationFailure = null;

        try
        {
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                try
                {
                    treeOwner.Terminate(exitCode: HostExitCodes.Cancelled);
                }
                catch (Exception exception) when (IsOwnershipException(exception))
                {
                    cancellationFailure = exception.Message;
                    TerminateProcess(process);
                }

                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            // إغلاق المالك بعد خروج الجذر ينهي أي أبناء بقوا في الخلفية قبل
            // انتظار اكتمال أنابيب stdout/stderr الموروثة منهم.
            treeOwner.Dispose();
        }

        await ObserveStandardInputAsync(standardInputTask, process, cancelled).ConfigureAwait(false);
        string standardOutput = await standardOutputTask.ConfigureAwait(false);
        string standardError = await standardErrorTask.ConfigureAwait(false);
        stopwatch.Stop();

        if (cancelled)
        {
            return CreateCancelledResult(
                stopwatch.Elapsed,
                standardOutput,
                standardError,
                treeOwnershipMode,
                cancellationFailure);
        }

        return new ExternalProcessResult(
            ExternalProcessTerminationReason.Exited,
            process.ExitCode,
            standardOutput,
            standardError,
            stopwatch.Elapsed,
            failureMessage: null,
            treeOwnershipMode);
    }

    private static async Task<string> CaptureStreamAsync(
        StreamReader reader,
        ExternalProcessStream stream,
        IExternalProcessOutputSink? outputSink)
    {
        char[] buffer = new char[4096];
        var captured = new StringBuilder();

        while (true)
        {
            int characterCount = await reader
                .ReadAsync(buffer.AsMemory())
                .ConfigureAwait(false);
            if (characterCount == 0)
            {
                break;
            }

            string text = new(buffer, 0, characterCount);
            captured.Append(text);
            if (outputSink is not null)
            {
                await outputSink
                    .WriteAsync(
                        new ExternalProcessOutputChunk(stream, text),
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }

        return captured.ToString();
    }

    private static ProcessStartInfo CreateStartInfo(ExternalProcessRequest request)
    {
        bool useLinuxProcessGroup = OperatingSystem.IsLinux();
        string targetExecutable = useLinuxProcessGroup
            ? ResolveLinuxTargetExecutable(request)
            : request.Executable;
        var startInfo = new ProcessStartInfo
        {
            FileName = useLinuxProcessGroup
                ? ResolveLinuxSetSidExecutable()
                : request.Executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = request.StandardInputMode is not ExternalProcessInputMode.Inherit,
            StandardOutputEncoding = Utf8WithoutBom,
            StandardErrorEncoding = Utf8WithoutBom
        };

        if (startInfo.RedirectStandardInput)
        {
            startInfo.StandardInputEncoding = Utf8WithoutBom;
        }

        if (request.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        if (useLinuxProcessGroup)
        {
            startInfo.ArgumentList.Add("--");
            startInfo.ArgumentList.Add(targetExecutable);
        }

        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (!request.InheritEnvironment)
        {
            startInfo.Environment.Clear();
        }

        foreach (KeyValuePair<string, string?> change in request.EnvironmentChanges)
        {
            if (change.Value is null)
            {
                startInfo.Environment.Remove(change.Key);
            }
            else
            {
                startInfo.Environment[change.Key] = change.Value;
            }
        }

        return startInfo;
    }

    private static string ResolveLinuxSetSidExecutable()
    {
        string[] candidates = ["/usr/bin/setsid", "/bin/setsid"];
        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new PlatformNotSupportedException(
            "يتطلب امتلاك شجرة العملية على Linux أداة setsid من حزمة util-linux، ولم تُعثر عليها في /usr/bin أو /bin.");
    }

    private static string ResolveLinuxTargetExecutable(ExternalProcessRequest request)
    {
        string executable = request.Executable;
        string workingDirectory = request.WorkingDirectory ?? Environment.CurrentDirectory;

        if (Path.IsPathRooted(executable) || executable.Contains(Path.DirectorySeparatorChar))
        {
            string candidate = Path.IsPathRooted(executable)
                ? executable
                : Path.Combine(workingDirectory, executable);
            string fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }

            throw new FileNotFoundException(
                $"لم يُعثر على البرنامج الخارجي المطلوب: {executable}",
                fullPath);
        }

        string? pathValue = request.InheritEnvironment
            ? Environment.GetEnvironmentVariable("PATH")
            : null;
        foreach (KeyValuePair<string, string?> change in request.EnvironmentChanges)
        {
            if (string.Equals(change.Key, "PATH", StringComparison.Ordinal))
            {
                pathValue = change.Value;
            }
        }

        // تستخدم execvp مسار النظام الافتراضي عندما لا تكون PATH موجودة.
        pathValue ??= "/bin:/usr/bin";
        foreach (string directory in pathValue.Split(Path.PathSeparator))
        {
            string baseDirectory = string.IsNullOrEmpty(directory)
                ? workingDirectory
                : Path.IsPathRooted(directory)
                    ? directory
                    : Path.Combine(workingDirectory, directory);
            string candidate = Path.GetFullPath(Path.Combine(baseDirectory, executable));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"لم يُعثر على البرنامج الخارجي '{executable}' في PATH الفعالة.",
            executable);
    }

    private static async Task WriteStandardInputAsync(
        Process process,
        ExternalProcessRequest request)
    {
        if (request.StandardInputMode is ExternalProcessInputMode.Inherit)
        {
            return;
        }

        try
        {
            if (request.StandardInputMode is ExternalProcessInputMode.Text)
            {
                await process.StandardInput.WriteAsync(request.StandardInputText).ConfigureAwait(false);
                await process.StandardInput.FlushAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

    private static async Task ObserveStandardInputAsync(
        Task standardInputTask,
        Process process,
        bool cancelled)
    {
        try
        {
            await standardInputTask.ConfigureAwait(false);
        }
        catch (Exception exception) when (
            (cancelled || process.HasExited)
            && exception is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // خروج العملية أو إلغاؤها قد يغلق الأنبوب قبل انتهاء الكاتب.
        }
    }

    private static void TerminateProcess(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            // انتهت العملية بالتزامن مع طلب الإلغاء.
        }
    }

    private static bool IsLaunchException(Exception exception)
    {
        return exception is Win32Exception
            or FileNotFoundException
            or DirectoryNotFoundException
            or UnauthorizedAccessException
            or InvalidOperationException
            or PlatformNotSupportedException;
    }

    private static bool IsOwnershipException(Exception exception)
    {
        return exception is Win32Exception
            or UnauthorizedAccessException
            or InvalidOperationException
            or PlatformNotSupportedException;
    }

    private static ExternalProcessResult CreateLaunchFailure(TimeSpan duration, string message)
    {
        return new ExternalProcessResult(
            ExternalProcessTerminationReason.LaunchFailed,
            exitCode: null,
            standardOutput: string.Empty,
            standardError: string.Empty,
            duration,
            message,
            ExternalProcessTreeOwnershipMode.None);
    }

    private static ExternalProcessResult CreateCancelledResult(
        TimeSpan duration,
        string standardOutput,
        string standardError,
        ExternalProcessTreeOwnershipMode treeOwnershipMode,
        string? failureMessage)
    {
        return new ExternalProcessResult(
            ExternalProcessTerminationReason.Cancelled,
            exitCode: null,
            standardOutput,
            standardError,
            duration,
            failureMessage,
            treeOwnershipMode);
    }

    private static ExternalProcessResult CreateOwnershipFailure(
        TimeSpan duration,
        string standardOutput,
        string standardError,
        string failureMessage)
    {
        return new ExternalProcessResult(
            ExternalProcessTerminationReason.OwnershipFailed,
            exitCode: null,
            standardOutput,
            standardError,
            duration,
            failureMessage,
            ExternalProcessTreeOwnershipMode.None);
    }
}
