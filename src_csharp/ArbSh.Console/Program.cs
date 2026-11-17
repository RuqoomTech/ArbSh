using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ArbSh.Core;
using ArbSh.Console.I18n;

namespace ArbSh.Console
{
    internal static class Program
    {
        private const string ExitCommand = "اخرج";

        private static int Main(string[] args)
        {
            if (!HostCommandLine.TryParse(args, out HostCommandLine commandLine, out string? parseError))
            {
                using var usageError = OpenStandardWriter(System.Console.OpenStandardError());
                usageError.Write($"{parseError}\n{HostCommandLine.Usage}");
                return HostExitCodes.Usage;
            }

            if (commandLine.Mode != HostMode.Interactive)
            {
                return RunNonInteractive(commandLine);
            }

            if (ConsoleEnvironment.TryLaunchInBetterTerminal(args))
            {
                return HostExitCodes.Success;
            }

            System.Console.InputEncoding = Encoding.UTF8;
            System.Console.OutputEncoding = Encoding.UTF8;

            var sink = new ConsoleExecutionSink();
            var session = new ShellSessionState(ResolveInitialWorkingDirectory(commandLine.WorkingDirectory, sink));
            var executionOptions = new ExecutionOptions
            {
                EmitDebug = commandLine.DebugConsole
            };

            ConsoleRTLDisplay.DisplayRTLText("مرحباً بكم في أربش (النموذج الأولي)!", rightAlign: true);
            ConsoleRTLDisplay.DisplayRTLText("اكتب 'اخرج' للخروج.", rightAlign: true);
            System.Console.WriteLine();

            if (executionOptions.EmitDebug)
            {
                ConsoleEnvironment.DisplayConsoleInfo();
                System.Console.WriteLine(ArabicConsoleInput.GetInputInfo());
                System.Console.WriteLine();
            }
            else
            {
                var consoleInfo = ConsoleEnvironment.DetectConsoleEnvironment();
                if (consoleInfo.ArabicSupport == ConsoleEnvironment.ArabicSupportLevel.Poor
                    && !System.Console.IsOutputRedirected)
                {
                    ConsoleRTLDisplay.DisplayRTLText("⚠️  تحذير: المحطة الحالية لا تدعم النص العربي بشكل جيد", rightAlign: true);
                    ConsoleRTLDisplay.DisplayRTLText("💡 نصيحة: استخدم Windows Terminal للحصول على أفضل عرض للنص العربي", rightAlign: true);
                    System.Console.WriteLine();
                }
            }

            ArabicConsoleInput.Initialize(ArabicConsoleInput.InputStrategy.Auto);

            try
            {
                while (true)
                {
                    string? inputLine;

                    if (!System.Console.IsInputRedirected)
                    {
                        // Read RTL interactively while preserving logical order for parser/executor.
                        inputLine = RTLConsoleInput.ReadRTLLine();
                    }
                    else
                    {
                        inputLine = ArabicConsoleInput.ReadLine();
                    }

                    if (inputLine is null)
                    {
                        break;
                    }

                    inputLine = inputLine.Trim();
                    if (string.IsNullOrWhiteSpace(inputLine))
                    {
                        continue;
                    }

                    if (string.Equals(inputLine, ExitCommand, StringComparison.Ordinal))
                    {
                        break;
                    }

                    try
                    {
                        ShellEngine.ExecuteInput(inputLine, sink, executionOptions, session);
                    }
                    catch (NotImplementedException ex)
                    {
                        sink.WriteWarning($"Feature not implemented: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        sink.WriteError($"ERROR: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                sink.WriteError($"FATAL ERROR: {ex.Message}");
            }
            finally
            {
                session.Jobs.StopAll(TimeSpan.FromSeconds(5));
                ArabicConsoleInput.Cleanup();
                if (!System.Console.IsInputRedirected)
                {
                    System.Console.WriteLine("تم إغلاق أربش.");
                }
            }

            return session.LastExitCode;
        }

        private static int RunNonInteractive(HostCommandLine commandLine)
        {
            // نص منطقي بلا BOM: الخرج موجه للبرامج لا للعرض البصري.
            using var output = OpenStandardWriter(System.Console.OpenStandardOutput());
            using var error = OpenStandardWriter(System.Console.OpenStandardError());
            using var cancellation = new CancellationTokenSource();
            using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, Cancel);
            using var quit = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, Cancel);
            using var terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Cancel);

            return CommandModeHost.Run(commandLine, output, error, cancellationToken: cancellation.Token);

            void Cancel(PosixSignalContext context)
            {
                context.Cancel = true;
                cancellation.Cancel();
            }
        }

        private static StreamWriter OpenStandardWriter(Stream stream) =>
            new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };

        private static string? ResolveInitialWorkingDirectory(string? requestedPath, ConsoleExecutionSink sink)
        {
            if (string.IsNullOrWhiteSpace(requestedPath))
            {
                return null;
            }

            try
            {
                string fullPath = System.IO.Path.GetFullPath(requestedPath);
                if (System.IO.Directory.Exists(fullPath))
                {
                    return fullPath;
                }

                sink.WriteWarning($"تحذير: المجلد الابتدائي غير موجود: {requestedPath}");
                return null;
            }
            catch (Exception ex)
            {
                sink.WriteWarning($"تحذير: تعذّر استخدام المجلد الابتدائي '{requestedPath}': {ex.Message}");
                return null;
            }
        }
    }
}
