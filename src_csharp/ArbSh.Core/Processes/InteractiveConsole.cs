namespace ArbSh.Core.Processes;

/// <summary>
/// جانب المضيف من الطرفية الزائفة: يرسل المضيف إليه أسطر المستخدم وحجم نافذته، ويربطه المنفذ
/// بالعملية الأمامية التفاعلية ما دامت تعمل، ويعرض شاشتها الحية عبر <see cref="ScreenChanged"/>.
/// </summary>
public sealed class InteractiveConsole
{
    public const int DefaultColumns = 120;
    public const int DefaultRows = 30;

    private readonly object _sync = new();
    private Session? _session;
    private int _columns = DefaultColumns;
    private int _rows = DefaultRows;

    /// <summary>
    /// يُطلق بعد كل تغيير في الشاشة الحية، وعند انفصال العملية؛ قد يأتي من خيط غير خيط الواجهة.
    /// </summary>
    public event EventHandler? ScreenChanged;

    /// <summary>
    /// هل توجد عملية أمامية تفاعلية تستقبل الإدخال الآن؟
    /// </summary>
    public bool IsAttached
    {
        get
        {
            lock (_sync)
            {
                return _session is not null;
            }
        }
    }

    public int Columns
    {
        get
        {
            lock (_sync)
            {
                return _columns;
            }
        }
    }

    public int Rows
    {
        get
        {
            lock (_sync)
            {
                return _rows;
            }
        }
    }

    /// <summary>
    /// يرسل سطرًا، ولو كان فارغًا، إلى العملية الأمامية التفاعلية كأن المستخدم ضغط Enter بعده.
    /// </summary>
    /// <returns>false إذا لم توجد عملية تفاعلية تستقبله.</returns>
    public bool TrySendLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        Session? session;
        lock (_sync)
        {
            session = _session;
        }

        if (session is null)
        {
            return false;
        }

        // مفتاح Enter في الطرفية هو CR؛ وتحوّله الطرفية الزائفة إلى نهاية سطر للبرنامج.
        session.WriteInput(line + "\r");
        return true;
    }

    /// <summary>
    /// يضبط حجم الطرفية بالأعمدة والصفوف، ويغير حجم الطرفية الزائفة الجارية إن وجدت.
    /// </summary>
    public void Resize(int columns, int rows)
    {
        columns = Math.Clamp(columns, 1, short.MaxValue);
        rows = Math.Clamp(rows, 1, short.MaxValue);
        Session? session;
        lock (_sync)
        {
            if (columns == _columns && rows == _rows)
            {
                return;
            }

            _columns = columns;
            _rows = rows;
            session = _session;
        }

        if (session is not null && session.Resize(columns, rows))
        {
            ScreenChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// الأسطر الحية للعملية الأمامية (بتسلسلات SGR إن كانت ملونة)، أو قائمة فارغة إن لم توجد.
    /// </summary>
    public IReadOnlyList<string> GetLiveLines()
    {
        Session? session;
        lock (_sync)
        {
            session = _session;
        }

        return session?.GetLiveLines() ?? [];
    }

    internal Session Attach(Action<string> writeInput, Action<int, int> resizeTerminal)
    {
        lock (_sync)
        {
            if (_session is not null)
            {
                throw new InvalidOperationException("توجد عملية تفاعلية أمامية أخرى على هذه الطرفية.");
            }

            _session = new Session(this, writeInput, resizeTerminal, _columns, _rows);
            return _session;
        }
    }

    private void Detach(Session session)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_session, session))
            {
                _session = null;
            }
        }

        ScreenChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// ربط عملية واحدة بالطرفية: شاشتها وإدخالها وحجم طرفيتها الزائفة.
    /// </summary>
    internal sealed class Session : IDisposable
    {
        private readonly InteractiveConsole _owner;
        private readonly Action<string> _writeInput;
        private readonly Action<int, int> _resizeTerminal;
        private readonly VtScreen _screen;
        private readonly object _screenSync = new();
        private readonly List<string> _resizeCommitted = [];

        public Session(
            InteractiveConsole owner,
            Action<string> writeInput,
            Action<int, int> resizeTerminal,
            int columns,
            int rows)
        {
            _owner = owner;
            _writeInput = writeInput;
            _resizeTerminal = resizeTerminal;
            _screen = new VtScreen(columns, rows);
        }

        public void WriteInput(string text) => _writeInput(text);

        /// <summary>
        /// يطبق خرج العملية على الشاشة ويعيد ما سُلِّم من أسطر منطقية بالترتيب.
        /// </summary>
        public List<string> Feed(ReadOnlySpan<char> text)
        {
            var committed = new List<string>();
            var replies = new List<string>();
            lock (_screenSync)
            {
                // أسطر أخرجها تصغير النافذة من أعلى الشاشة تسبق الخرج الجديد.
                committed.AddRange(_resizeCommitted);
                _resizeCommitted.Clear();
                _screen.Write(text, committed, replies.Add);
            }

            foreach (string reply in replies)
            {
                _writeInput(reply);
            }

            _owner.ScreenChanged?.Invoke(_owner, EventArgs.Empty);
            return committed;
        }

        /// <summary>
        /// يسلّم ما بقي على الشاشة الأساسية عند انتهاء العملية.
        /// </summary>
        public List<string> Finish()
        {
            lock (_screenSync)
            {
                var committed = new List<string>(_resizeCommitted);
                _resizeCommitted.Clear();
                _screen.Finish(committed);
                return committed;
            }
        }

        public bool Resize(int columns, int rows)
        {
            lock (_screenSync)
            {
                if (_screen.Columns == columns && _screen.Rows == rows)
                {
                    return false;
                }

                _screen.Resize(columns, rows, _resizeCommitted);
            }

            _resizeTerminal(columns, rows);
            return true;
        }

        public IReadOnlyList<string> GetLiveLines()
        {
            lock (_screenSync)
            {
                return _screen.GetLiveLines();
            }
        }

        public void Dispose() => _owner.Detach(this);
    }
}
