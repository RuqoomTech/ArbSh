using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using ArbSh.Core;
using ArbSh.Core.Hosting;
using ArbSh.Core.Processes;
using ArbSh.Terminal.Models;

namespace ArbSh.Terminal.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const string ExitCommand = "اخرج";
    private readonly ShellSessionState _session;
    private readonly ObservableCollection<TerminalLine> _lines = [];
    private readonly ReadOnlyObservableCollection<TerminalLine> _readonlyLines;
    private readonly InteractiveConsole _interactiveConsole = new();
    private CancellationTokenSource? _activeExecution;
    private string _statusText = "جاهز";
    private int _liveLineCount;
    private int _liveUpdatePending;
    private string? _pendingInput;

    public MainWindowViewModel(string? initialWorkingDirectory = null, CommandHistory? history = null)
    {
        _session = new ShellSessionState(initialWorkingDirectory);
        History = history ?? new CommandHistory();
        _readonlyLines = new ReadOnlyObservableCollection<TerminalLine>(_lines);
        _interactiveConsole.ScreenChanged += HandleScreenChanged;
        AddLine("مرحبًا بك في أربش", TerminalLineKind.System);
        AddLine("صدفة عربية حديثة — اكتب «الأوامر» للقائمة أو «مساعدة» للدليل.", TerminalLineKind.System);
        AddLine(string.Empty, TerminalLineKind.System);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? BufferChanged;
    public event EventHandler? ExitRequested;

    public ReadOnlyObservableCollection<TerminalLine> Lines => _readonlyLines;

    public string CurrentDirectory => _session.CurrentDirectory;

    /// <summary>
    /// سجل الأوامر المنفذة للتنقل بالسهمين.
    /// </summary>
    public CommandHistory History { get; }

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
    public string Prompt => _pendingInput is null
        ? $"أربش {LeftToRightIsolate}{_session.CurrentDirectory}{PopDirectionalIsolate}>{RightToLeftMark} "
        : ContinuationPrompt;

    /// <summary>
    /// موجه الأسطر التالية لأمر متعدد الأسطر لم يكتمل بعد.
    /// </summary>
    public const string ContinuationPrompt = ">>\u200F ";

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

    /// <summary>
    /// ينفذ السطر المكتوب، أو يضمه إلى أمر متعدد الأسطر إذا كان ناقصًا (اقتباس أو قوس مفتوح
    /// أو «|» في آخره) أو طلب المستخدم سطرًا آخر بـ<paramref name="continueInput"/> (Shift+Enter).
    /// </summary>
    public async Task SubmitInputAsync(string logicalInput, CancellationToken cancellationToken = default, bool continueInput = false)
    {
        // أثناء عمل برنامج تفاعلي يذهب السطر، ولو كان فارغًا، إلى إدخاله؛ وصداه يعود من طرفيته.
        if (_interactiveConsole.TrySendLine(logicalInput))
        {
            return;
        }

        if (_pendingInput is null && string.IsNullOrWhiteSpace(logicalInput))
        {
            return;
        }

        // يبقى الأمر في السجل بعد موجهه، كما تعرضه الطرفيات المعتادة.
        AddLine(Prompt + logicalInput, TerminalLineKind.Input);

        string fullInput = _pendingInput is null ? logicalInput : _pendingInput + "\n" + logicalInput;
        _pendingInput = continueInput || Parser.IsIncomplete(fullInput) ? fullInput : null;
        OnPropertyChanged(nameof(Prompt));
        if (_pendingInput is not null)
        {
            return;
        }

        logicalInput = fullInput;
        History.Add(Parser.ToSingleLine(logicalInput.Trim()));
        string trimmedInput = logicalInput.Trim();
        if (string.Equals(trimmedInput, ExitCommand, StringComparison.Ordinal))
        {
            Dispatcher.UIThread.Post(() => ExitRequested?.Invoke(this, EventArgs.Empty));
            return;
        }

        var sink = new TerminalExecutionSink(this);
        var options = new ExecutionOptions
        {
            EmitDebug = false,
            InteractiveConsole = _interactiveConsole
        };

        using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeExecution = execution;
        try
        {
            StatusText = "جارٍ التنفيذ…";
            await Task.Run(
                () => ShellEngine.ExecuteInput(
                    logicalInput,
                    sink,
                    options,
                    _session,
                    cancellationToken: execution.Token));
        }
        catch (Exception ex)
        {
            AddLine($"خطأ: {ex.Message}", TerminalLineKind.Error);
        }
        finally
        {
            if (ReferenceEquals(_activeExecution, execution))
            {
                _activeExecution = null;
            }

            OnPropertyChanged(nameof(CurrentDirectory));
            OnPropertyChanged(nameof(Prompt));
            StatusText = "جاهز";
        }
    }

    /// <summary>
    /// يضبط حجم الطرفية الزائفة بأعمدة السطح وصفوفه؛ يصل البرنامج الجاري إشعار تغيير الحجم.
    /// </summary>
    public void ResizeTerminal(int columns, int rows) => _interactiveConsole.Resize(columns, rows);

    /// <summary>
    /// هل يعمل برنامج في الطرفية الزائفة الآن؟ عندها تذهب المفاتيح إليه مباشرة لا إلى سطر أربش.
    /// </summary>
    public bool IsProgramAttached => _interactiveConsole.IsAttached;

    /// <summary>
    /// آخر لقطة للشاشة الحية: مؤشر البرنامج وأوضاع إدخاله.
    /// </summary>
    public VtLiveView LiveView { get; private set; } = VtLiveView.Empty;

    /// <summary>
    /// موضع أول سطر حي في <see cref="Lines"/>؛ يضاف إليه سطر المؤشر في <see cref="LiveView"/>.
    /// </summary>
    public int LiveLineStart => _lines.Count - _liveLineCount;

    /// <summary>
    /// يرسل نصًا أو تسلسل مفتاح إلى البرنامج الجاري كما هو.
    /// </summary>
    public bool SendProgramInput(string input) => _interactiveConsole.TrySendInput(input);

    /// <summary>
    /// يكمل الكلمة عند المؤشر، ويعرض المرشحين إذا تعددوا ولم يبق جزء مشترك يُكمل.
    /// </summary>
    public CompletionResult Complete(string input, int caret)
    {
        CompletionResult result = TabCompletion.Complete(input, caret, _session.CurrentDirectory);
        if (result.Candidates.Count > 0 && result.Text == input)
        {
            AddLine(string.Join("    ", result.Candidates), TerminalLineKind.System);
        }

        return result;
    }

    /// <summary>
    /// يلغي الأمر الجاري وينهي شجرة عملياته، أو يتخلى عن أمر متعدد الأسطر لم يكتمل.
    /// يعيد false إذا لم يكن هناك ما يُلغى.
    /// </summary>
    public bool CancelExecution()
    {
        CancellationTokenSource? execution = _activeExecution;
        if (execution is null)
        {
            if (_pendingInput is null)
            {
                return false;
            }

            _pendingInput = null;
            OnPropertyChanged(nameof(Prompt));
            return true;
        }

        execution.Cancel();
        return true;
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
        // الأسطر المسلَّمة تسبق شاشة البرنامج الحية التي تبقى في أسفل السجل.
        _lines.Insert(_lines.Count - _liveLineCount, new TerminalLine(text, kind, DateTimeOffset.UtcNow));
        TrimLines();
        BufferChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleScreenChanged(object? sender, EventArgs e)
    {
        // يأتي من خيط القراءة مع كل قطعة خرج؛ تكفي إعادة رسم واحدة لكل دفعة.
        if (Interlocked.Exchange(ref _liveUpdatePending, 1) == 0)
        {
            Dispatcher.UIThread.Post(UpdateLiveLines);
        }
    }

    private void UpdateLiveLines()
    {
        Volatile.Write(ref _liveUpdatePending, 0);
        VtLiveView view = _interactiveConsole.GetLiveView();
        IReadOnlyList<string> live = view.Lines;
        LiveView = view;
        for (; _liveLineCount > 0; _liveLineCount--)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (string line in live)
        {
            _lines.Add(new TerminalLine(line, TerminalLineKind.Output, now));
        }

        _liveLineCount = live.Count;
        TrimLines();
        BufferChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TrimLines()
    {
        const int maxLines = 5000;
        while (_lines.Count > maxLines && _lines.Count > _liveLineCount)
        {
            _lines.RemoveAt(0);
        }
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
