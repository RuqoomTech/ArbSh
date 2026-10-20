using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using ArbSh.Core;
using ArbSh.Terminal.Models;

namespace ArbSh.Terminal.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const string ExitCommand = "اخرج";
    private readonly ShellSessionState _session;
    private readonly ObservableCollection<TerminalLine> _lines = [];
    private readonly ReadOnlyObservableCollection<TerminalLine> _readonlyLines;
    private string _statusText = "جاهز";

    public MainWindowViewModel(string? initialWorkingDirectory = null)
    {
        _session = new ShellSessionState(initialWorkingDirectory);
        _readonlyLines = new ReadOnlyObservableCollection<TerminalLine>(_lines);
        AddLine("مرحبًا بك في أربش", TerminalLineKind.System);
        AddLine("صدفة عربية حديثة — اكتب «الأوامر» للقائمة أو «مساعدة» للدليل.", TerminalLineKind.System);
        AddLine(string.Empty, TerminalLineKind.System);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? BufferChanged;
    public event EventHandler? ExitRequested;

    public ReadOnlyObservableCollection<TerminalLine> Lines => _readonlyLines;

    public string CurrentDirectory => _session.CurrentDirectory;

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (string.Equals(_statusText, value, StringComparison.Ordinal))
            {
                return;
            }

            _statusText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// الموجه على نمط «PS المسار> » في Windows Terminal، معكوسًا للعربية.
    /// يُعزل المسار باتجاه يسار-يمين كي لا يعيد BiDi ترتيب فواصله حول أسماء المجلدات العربية،
    /// وتتبع «>» علامة يمين-يسار كي يبقى الأمر اللاتيني المكتوب بعدها على يسارها لا ملتصقًا بالمسار.
    /// </summary>
    public string Prompt => $"أربش {LeftToRightIsolate}{_session.CurrentDirectory}{PopDirectionalIsolate}>{RightToLeftMark} ";

    /// <summary>
    /// عنوان علامة التبويب، مسبوقًا بـ«المسؤول» عند التشغيل بصلاحيات مرتفعة كما في Windows Terminal.
    /// </summary>
    public string Title => IsElevated ? "المسؤول: أربش" : "أربش";

    /// <summary>
    /// هل تعمل العملية بصلاحيات المسؤول.
    /// </summary>
    public static bool IsElevated { get; } = DetectElevation();

    private const char LeftToRightIsolate = '\u2066';
    private const char PopDirectionalIsolate = '\u2069';
    private const char RightToLeftMark = '\u200F';

    public async Task SubmitInputAsync(string logicalInput, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(logicalInput))
        {
            return;
        }

        // يبقى الأمر في السجل بعد موجهه، كما تعرضه الطرفيات المعتادة.
        AddLine(Prompt + logicalInput, TerminalLineKind.Input);

        string trimmedInput = logicalInput.Trim();
        if (string.Equals(trimmedInput, ExitCommand, StringComparison.Ordinal))
        {
            Dispatcher.UIThread.Post(() => ExitRequested?.Invoke(this, EventArgs.Empty));
            return;
        }

        var sink = new TerminalExecutionSink(this);
        var options = new ExecutionOptions
        {
            EmitDebug = false
        };

        try
        {
            StatusText = "جارٍ التنفيذ…";
            await Task.Run(
                () => ShellEngine.ExecuteInput(
                    logicalInput,
                    sink,
                    options,
                    _session,
                    cancellationToken: cancellationToken),
                cancellationToken);
        }
        catch (Exception ex)
        {
            AddLine($"خطأ: {ex.Message}", TerminalLineKind.Error);
        }
        finally
        {
            OnPropertyChanged(nameof(CurrentDirectory));
            OnPropertyChanged(nameof(Prompt));
            StatusText = "جاهز";
        }
    }

    internal void PostLine(string message, TerminalLineKind kind)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Dispatcher.UIThread.Post(() => AddLine(message, kind));
    }

    private void AddLine(string text, TerminalLineKind kind)
    {
        _lines.Add(new TerminalLine(text, kind, DateTimeOffset.UtcNow));

        const int maxLines = 5000;
        while (_lines.Count > maxLines)
        {
            _lines.RemoveAt(0);
        }

        BufferChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool DetectElevation()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(identity)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }

            return Environment.UserName == "root";
        }
        catch
        {
            return false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class TerminalExecutionSink : IExecutionSink
    {
        private readonly MainWindowViewModel _owner;

        public TerminalExecutionSink(MainWindowViewModel owner)
        {
            _owner = owner;
        }

        public void WriteOutput(string message)
        {
            _owner.PostLine(message, TerminalLineKind.Output);
        }

        public void WriteError(string message)
        {
            _owner.PostLine(message, TerminalLineKind.Error);
        }

        public void WriteWarning(string message)
        {
            _owner.PostLine(message, TerminalLineKind.Warning);
        }

        public void WriteDebug(string message)
        {
            _owner.PostLine(message, TerminalLineKind.Debug);
        }
    }
}
