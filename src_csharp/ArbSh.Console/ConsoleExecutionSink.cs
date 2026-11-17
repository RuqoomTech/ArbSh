using ArbSh.Core;
using ArbSh.Core.I18n;
using ArbSh.Console.I18n;

namespace ArbSh.Console;

/// <summary>
/// Routes core execution output to the interactive console host.
/// </summary>
public sealed class ConsoleExecutionSink : IExecutionSink
{
    /// <inheritdoc />
    public void WriteOutput(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (ConsoleRTLDisplay.ShouldUseRTLDisplay(message))
        {
            ConsoleRTLDisplay.DisplayRTLText(message, rightAlign: true);
            return;
        }

        System.Console.WriteLine(message);
    }

    /// <inheritdoc />
    public void WriteError(string message)
    {
        WriteColored(message, ConsoleColor.Red);
    }

    /// <inheritdoc />
    public void WriteWarning(string message)
    {
        WriteColored(message, ConsoleColor.Yellow);
    }

    /// <inheritdoc />
    public void WriteDebug(string message)
    {
        WriteColored(message, ConsoleColor.DarkGray);
    }

    private static void WriteColored(string message, ConsoleColor color)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var previousColor = System.Console.ForegroundColor;
        System.Console.ForegroundColor = color;

        try
        {
            string displayText = !System.Console.IsOutputRedirected && BiDiTextProcessor.ContainsArabicText(message)
                ? ConsoleRTLDisplay.ProcessTextForRTLDisplay(message)
                : message;

            System.Console.WriteLine(displayText);
        }
        finally
        {
            System.Console.ForegroundColor = previousColor;
        }
    }
}
