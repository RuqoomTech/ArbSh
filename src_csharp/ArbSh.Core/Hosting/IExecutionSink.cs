namespace ArbSh.Core;

/// <summary>
/// Represents a host-provided sink for shell output, diagnostics, and errors.
/// </summary>
public interface IExecutionSink
{
    /// <summary>
    /// Writes a standard output line.
    /// </summary>
    /// <param name="message">The output message.</param>
    void WriteOutput(string message);

    /// <summary>
    /// Writes an error output line.
    /// </summary>
    /// <param name="message">The error message.</param>
    void WriteError(string message);

    /// <summary>
    /// Writes a warning line.
    /// </summary>
    /// <param name="message">The warning message.</param>
    void WriteWarning(string message);

    /// <summary>
    /// Writes a debug/trace line.
    /// </summary>
    /// <param name="message">The debug message.</param>
    void WriteDebug(string message);
}

/// <summary>
/// Controls execution-time diagnostic emission behavior.
/// </summary>
public sealed class ExecutionOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether debug/trace messages should be emitted.
    /// </summary>
    public bool EmitDebug { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether warning messages should be emitted.
    /// </summary>
    public bool EmitWarnings { get; init; } = true;

    /// <summary>
    /// طرفية المضيف التفاعلية. عند توفرها تعمل البرامج الأمامية التي يشغّلها «تشغيل»
    /// داخل طرفية زائفة وتقرأ ما يكتبه المستخدم أثناء عملها.
    /// </summary>
    public Processes.InteractiveConsole? InteractiveConsole { get; init; }
}

/// <summary>
/// A no-op execution sink used as a default for non-hosted operations.
/// </summary>
public sealed class NullExecutionSink : IExecutionSink
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static NullExecutionSink Instance { get; } = new();

    private NullExecutionSink()
    {
    }

    /// <inheritdoc />
    public void WriteOutput(string message)
    {
    }

    /// <inheritdoc />
    public void WriteError(string message)
    {
    }

    /// <inheritdoc />
    public void WriteWarning(string message)
    {
    }

    /// <inheritdoc />
    public void WriteDebug(string message)
    {
    }
}

/// <summary>
/// A basic execution sink that writes lines to system console streams.
/// </summary>
public sealed class StandardConsoleExecutionSink : IExecutionSink
{
    /// <inheritdoc />
    public void WriteOutput(string message)
    {
        System.Console.WriteLine(message);
    }

    /// <inheritdoc />
    public void WriteError(string message)
    {
        var previousColor = System.Console.ForegroundColor;
        System.Console.ForegroundColor = ConsoleColor.Red;
        System.Console.Error.WriteLine(message);
        System.Console.ForegroundColor = previousColor;
    }

    /// <inheritdoc />
    public void WriteWarning(string message)
    {
        var previousColor = System.Console.ForegroundColor;
        System.Console.ForegroundColor = ConsoleColor.Yellow;
        System.Console.WriteLine(message);
        System.Console.ForegroundColor = previousColor;
    }

    /// <inheritdoc />
    public void WriteDebug(string message)
    {
        var previousColor = System.Console.ForegroundColor;
        System.Console.ForegroundColor = ConsoleColor.DarkGray;
        System.Console.WriteLine(message);
        System.Console.ForegroundColor = previousColor;
    }
}
