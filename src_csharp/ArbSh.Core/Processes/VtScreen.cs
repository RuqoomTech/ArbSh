using System.Globalization;
using System.Text;

namespace ArbSh.Core.Processes;

/// <summary>
/// شبكة شاشة VT لخرج الطرفية الزائفة: مؤشر وأنماط SGR وشاشة بديلة ومنطقة تمرير.
/// السطر الذي يغادره المؤشر بسطر جديد في الشاشة الأساسية يُسلَّم نهائيًا إلى سجل الطرفية،
/// وما تحته يبقى حيًّا (موجه بلا نهاية سطر، شريط تقدم، تطبيق ملء الشاشة) حتى يغادره أو تنتهي العملية.
/// </summary>
/// <remarks>
/// shortcut: الكتابة فوق سطر سُلِّم (المؤشر يصعد فوق المنطقة الحية) تُطبق على الشبكة ولا تظهر،
/// وتغيير العرض يقص الأسطر ولا يعيد لفّها، وعرض كل خلية حرف واحد ولو كان حرفًا شرقيًا عريضًا؛
/// يُرقّى ذلك حين يحتاج برنامج حقيقي إعادة رسم أسطر سابقة أو أحرفًا عريضة.
/// </remarks>
public sealed class VtScreen
{
    private const char Escape = '\u001b';
    private const int MaxParameter = 9999;

    private Cell[][] _primary;
    private Cell[][]? _alternate;
    private bool[] _primaryWrapped;
    private bool[]? _alternateWrapped;
    private int _row;
    private int _column;
    private bool _pendingWrap;
    private int _scrollTop;
    private int _scrollBottom;
    private int _firstLiveRow;
    private bool _autoWrap = true;
    private bool _applicationCursorKeys;
    private bool _bracketedPaste;
    private bool _cursorVisible = true;
    private (int Row, int Column, string Style) _saved;
    private (int Row, int Column, string Style) _primarySaved;
    private string _style = string.Empty;
    private SgrState _sgr = new();

    private ParserState _state;
    private readonly StringBuilder _parameters = new();
    private char _privateMarker;

    public VtScreen(int columns, int rows)
    {
        Columns = Math.Max(1, columns);
        Rows = Math.Max(1, rows);
        _primary = CreateGrid(Columns, Rows);
        _primaryWrapped = new bool[Rows];
        _scrollBottom = Rows - 1;
    }

    private enum ParserState
    {
        Text,
        Escape,
        EscapeIntermediate,
        ControlSequence,
        ControlString,
        ControlStringEscape
    }

    public int Columns { get; private set; }

    public int Rows { get; private set; }

    public bool IsAlternateScreen => _alternate is not null;

    public int CursorRow => _row;

    public int CursorColumn => _column;

    /// <summary>طلب البرنامج مفاتيح المؤشر بصيغة التطبيق (<c>ESC O A</c>) بدل <c>ESC [ A</c>.</summary>
    public bool ApplicationCursorKeys => _applicationCursorKeys;

    /// <summary>طلب البرنامج إحاطة النص الملصق بـ<c>ESC [200~</c> و<c>ESC [201~</c>.</summary>
    public bool BracketedPaste => _bracketedPaste;

    private Cell[][] Grid => _alternate ?? _primary;

    private bool[] Wrapped => _alternateWrapped ?? _primaryWrapped;

    /// <summary>
    /// يطبق نصًا مفكوكًا؛ قد ينقسم التسلسل بين استدعاءين.
    /// </summary>
    /// <param name="committed">يضاف إليه كل سطر منطقي سُلِّم، بتسلسلات SGR إن كان ملونًا.</param>
    /// <param name="reply">يستقبل ردود الطرفية على استعلامات البرنامج (موضع المؤشر، هويتها).</param>
    public void Write(ReadOnlySpan<char> text, List<string> committed, Action<string>? reply = null)
    {
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            switch (_state)
            {
                case ParserState.Text:
                    if (character == Escape)
                    {
                        _state = ParserState.Escape;
                    }
                    else if (character < ' ' || character == '\u007f')
                    {
                        ExecuteControl(character, committed);
                    }
                    else if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                    {
                        Print(text.Slice(index, 2).ToString(), committed);
                        index++;
                    }
                    else
                    {
                        Print(character.ToString(), committed);
                    }

                    break;

                case ParserState.Escape:
                    ExecuteEscape(character, committed);
                    break;

                case ParserState.EscapeIntermediate:
                    if (character is >= '0' and <= '~')
                    {
                        _state = ParserState.Text;
                    }

                    break;

                case ParserState.ControlSequence:
                    if (character is >= '@' and <= '~')
                    {
                        _state = ParserState.Text;
                        ExecuteControlSequence(character, committed, reply);
                    }
                    else if (character is '?' or '>' or '<' or '=' && _parameters.Length == 0)
                    {
                        _privateMarker = character;
                    }
                    else if (character is >= '0' and <= '9' or ';' or ':')
                    {
                        _parameters.Append(character);
                    }

                    break;

                case ParserState.ControlString:
                    if (character == '\u0007')
                    {
                        _state = ParserState.Text;
                    }
                    else if (character == Escape)
                    {
                        _state = ParserState.ControlStringEscape;
                    }

                    break;

                case ParserState.ControlStringEscape:
                    _state = character == '\\' ? ParserState.Text : ParserState.ControlString;
                    break;
            }
        }
    }

    /// <summary>
    /// يغير حجم الشبكة. في الشاشة الأساسية يبقى المؤشر ظاهرًا بإزاحة الأسطر العليا وتسليم الحي منها.
    /// </summary>
    public void Resize(int columns, int rows, List<string> committed)
    {
        columns = Math.Max(1, columns);
        rows = Math.Max(1, rows);
        if (columns == Columns && rows == Rows)
        {
            return;
        }

        int shift = Math.Max(0, _row - (rows - 1));
        if (_alternate is null)
        {
            CommitRows(_firstLiveRow, Math.Min(shift, Rows) - 1, committed);
        }

        _primary = CopyGrid(_primary, columns, rows, _alternate is null ? shift : 0);
        _primaryWrapped = CopyFlags(_primaryWrapped, rows, _alternate is null ? shift : 0);
        if (_alternate is not null)
        {
            _alternate = CopyGrid(_alternate, columns, rows, shift);
            _alternateWrapped = CopyFlags(_alternateWrapped!, rows, shift);
        }

        _firstLiveRow = Math.Max(0, _firstLiveRow - shift);
        Columns = columns;
        Rows = rows;
        _row = Math.Clamp(_row - shift, 0, rows - 1);
        _column = Math.Min(_column, columns - 1);
        _pendingWrap = false;
        _scrollTop = 0;
        _scrollBottom = rows - 1;
    }

    /// <summary>
    /// يسلّم ما بقي حيًّا في الشاشة الأساسية عند انتهاء العملية؛ محتوى الشاشة البديلة لا يُسلَّم.
    /// </summary>
    public void Finish(List<string> committed)
    {
        if (_alternate is not null)
        {
            LeaveAlternateScreen();
        }

        int last = LastContentRow();
        CommitRows(_firstLiveRow, last, committed);
        _firstLiveRow = Math.Max(_firstLiveRow, Math.Max(last, _row) + 1);
    }

    /// <summary>
    /// الأسطر الحية الظاهرة الآن: في الشاشة الأساسية ما تحت آخر سطر سُلِّم حتى آخر محتوى أو سطر المؤشر،
    /// وفي الشاشة البديلة كل صفوفها.
    /// </summary>
    public IReadOnlyList<string> GetLiveLines() => GetLiveView().Lines;

    /// <summary>
    /// الأسطر الحية مع موضع المؤشر فيها: رقم السطر الحي وموضعه في نصه بلا تسلسلات،
    /// أو سطر -1 إذا أخفاه البرنامج أو لم يكن على سطر حي.
    /// </summary>
    public VtLiveView GetLiveView()
    {
        var lines = new List<string>();
        int cursorLine = -1;
        int cursorIndex = 0;
        if (_alternate is not null)
        {
            for (int row = 0; row < Rows; row++)
            {
                bool cursorRow = row == _row;
                if (cursorRow)
                {
                    cursorLine = row;
                    cursorIndex = PlainLength(_alternate[row], _column);
                }

                lines.Add(RowText(_alternate[row], styled: true, keep: cursorRow ? _column : 0));
            }
        }
        else
        {
            int last = Math.Max(LastContentRow(), _row >= _firstLiveRow ? _row : -1);
            (cursorLine, cursorIndex) = AppendLogicalLines(_firstLiveRow, last, lines, cursorRow: _row);
        }

        return new VtLiveView(lines, _cursorVisible ? cursorLine : -1, cursorIndex, _applicationCursorKeys, _bracketedPaste);
    }

    /// <summary>
    /// يحذف تسلسلات SGR من سطر سُلِّم؛ الأسطر المسلَّمة لا تحمل غيرها.
    /// </summary>
    public static string StripStyles(string line)
    {
        if (line.IndexOf(Escape) < 0)
        {
            return line;
        }

        var plain = new StringBuilder(line.Length);
        for (int index = 0; index < line.Length; index++)
        {
            if (line[index] == Escape)
            {
                int end = line.IndexOf('m', index);
                if (end >= 0)
                {
                    index = end;
                    continue;
                }
            }

            plain.Append(line[index]);
        }

        return plain.ToString();
    }

    private void Print(string grapheme, List<string> committed)
    {
        // علامات التشكيل والوصل تلتحق بالحرف السابق ولا تشغل خلية.
        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(grapheme, 0);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format
            || grapheme == "‍")
        {
            if (TryAttachToPrevious(grapheme))
            {
                return;
            }
        }

        if (_pendingWrap)
        {
            Wrapped[_row] = true;
            _column = 0;
            _pendingWrap = false;
            LineFeed(committed, commit: false);
        }

        Grid[_row][_column] = new Cell(grapheme, _style);
        if (_column == Columns - 1)
        {
            _pendingWrap = _autoWrap;
        }
        else
        {
            _column++;
        }
    }

    private bool TryAttachToPrevious(string mark)
    {
        int column = _pendingWrap ? _column : _column - 1;
        if (column < 0 || Grid[_row][column].Text.Length == 0)
        {
            return false;
        }

        Cell previous = Grid[_row][column];
        Grid[_row][column] = previous with { Text = previous.Text + mark };
        return true;
    }

    private void ExecuteControl(char character, List<string> committed)
    {
        switch (character)
        {
            case '\b':
                _pendingWrap = false;
                _column = Math.Max(0, _column - 1);
                break;
            case '\t':
                _pendingWrap = false;
                _column = Math.Min(Columns - 1, ((_column / 8) + 1) * 8);
                break;
            case '\n' or '\v' or '\f':
                LineFeed(committed, commit: true);
                break;
            case '\r':
                _pendingWrap = false;
                _column = 0;
                break;
        }
    }

    private void ExecuteEscape(char character, List<string> committed)
    {
        _state = ParserState.Text;
        switch (character)
        {
            case '[':
                _state = ParserState.ControlSequence;
                _parameters.Clear();
                _privateMarker = '\0';
                break;
            case ']' or 'P' or '_' or '^' or 'X':
                _state = ParserState.ControlString;
                break;
            case '7':
                _saved = (_row, _column, _style);
                break;
            case '8':
                RestoreCursor(_saved);
                break;
            case 'D':
                LineFeed(committed, commit: true);
                break;
            case 'E':
                _column = 0;
                LineFeed(committed, commit: true);
                break;
            case 'M':
                ReverseIndex();
                break;
            case 'c':
                Reset(committed);
                break;
            default:
                if (character is >= ' ' and <= '/')
                {
                    _state = ParserState.EscapeIntermediate;
                }

                break;
        }
    }

    private void ExecuteControlSequence(char final, List<string> committed, Action<string>? reply)
    {
        int[] parameters = ParseParameters();
        int First(int fallback) => parameters.Length > 0 && parameters[0] > 0 ? parameters[0] : fallback;
        int At(int index, int fallback) => parameters.Length > index && parameters[index] > 0 ? parameters[index] : fallback;

        if (_privateMarker == '?')
        {
            if (final is 'h' or 'l')
            {
                SetPrivateModes(parameters, final == 'h');
            }

            return;
        }

        if (_privateMarker != '\0')
        {
            return;
        }

        if (final != 'm')
        {
            _pendingWrap = false;
        }

        switch (final)
        {
            case 'A':
                _row = Math.Max(_row >= _scrollTop ? _scrollTop : 0, _row - First(1));
                break;
            case 'B':
                _row = Math.Min(_row <= _scrollBottom ? _scrollBottom : Rows - 1, _row + First(1));
                break;
            case 'C':
                _column = Math.Min(Columns - 1, _column + First(1));
                break;
            case 'D':
                _column = Math.Max(0, _column - First(1));
                break;
            case 'E':
                _row = Math.Min(Rows - 1, _row + First(1));
                _column = 0;
                break;
            case 'F':
                _row = Math.Max(0, _row - First(1));
                _column = 0;
                break;
            case 'G' or '`':
                _column = Math.Clamp(First(1) - 1, 0, Columns - 1);
                break;
            case 'd':
                _row = Math.Clamp(First(1) - 1, 0, Rows - 1);
                break;
            case 'H' or 'f':
                _row = Math.Clamp(At(0, 1) - 1, 0, Rows - 1);
                _column = Math.Clamp(At(1, 1) - 1, 0, Columns - 1);
                break;
            case 'J':
                EraseDisplay(parameters.Length > 0 ? parameters[0] : 0, committed);
                break;
            case 'K':
                EraseLine(parameters.Length > 0 ? parameters[0] : 0);
                break;
            case 'X':
                ClearCells(_row, _column, Math.Min(Columns, _column + First(1)));
                break;
            case '@':
                ShiftCells(First(1), insert: true);
                break;
            case 'P':
                ShiftCells(First(1), insert: false);
                break;
            case 'L':
                if (_row >= _scrollTop && _row <= _scrollBottom)
                {
                    ScrollDown(_row, _scrollBottom, First(1));
                }

                break;
            case 'M':
                if (_row >= _scrollTop && _row <= _scrollBottom)
                {
                    ScrollUp(_row, _scrollBottom, First(1), committed);
                }

                break;
            case 'S':
                ScrollUp(_scrollTop, _scrollBottom, First(1), committed);
                break;
            case 'T':
                ScrollDown(_scrollTop, _scrollBottom, First(1));
                break;
            case 'm':
                _sgr.Apply(parameters);
                _style = _sgr.ToParameters();
                break;
            case 'r':
                int top = At(0, 1) - 1;
                int bottom = At(1, Rows) - 1;
                if (top < bottom && bottom < Rows)
                {
                    _scrollTop = top;
                    _scrollBottom = bottom;
                    _row = 0;
                    _column = 0;
                }

                break;
            case 's':
                _saved = (_row, _column, _style);
                break;
            case 'u':
                RestoreCursor(_saved);
                break;
            case 'n':
                if (First(0) == 6)
                {
                    reply?.Invoke($"\u001b[{_row + 1};{_column + 1}R");
                }
                else if (First(0) == 5)
                {
                    reply?.Invoke("\u001b[0n");
                }

                break;
            case 'c':
                reply?.Invoke("\u001b[?1;2c");
                break;
        }
    }

    private int[] ParseParameters()
    {
        if (_parameters.Length == 0)
        {
            return [];
        }

        string[] parts = _parameters.ToString().Split(';', ':');
        var values = new int[parts.Length];
        for (int index = 0; index < parts.Length; index++)
        {
            values[index] = int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                ? Math.Min(value, MaxParameter)
                : 0;
        }

        return values;
    }

    private void SetPrivateModes(int[] modes, bool enable)
    {
        foreach (int mode in modes)
        {
            switch (mode)
            {
                case 1:
                    _applicationCursorKeys = enable;
                    break;
                case 7:
                    _autoWrap = enable;
                    break;
                case 25:
                    _cursorVisible = enable;
                    break;
                case 2004:
                    _bracketedPaste = enable;
                    break;
                case 47 or 1047 or 1049:
                    if (enable && _alternate is null)
                    {
                        if (mode == 1049)
                        {
                            _primarySaved = (_row, _column, _style);
                        }

                        _alternate = CreateGrid(Columns, Rows);
                        _alternateWrapped = new bool[Rows];
                        _pendingWrap = false;
                    }
                    else if (!enable && _alternate is not null)
                    {
                        LeaveAlternateScreen();
                        if (mode == 1049)
                        {
                            RestoreCursor(_primarySaved);
                        }
                    }

                    break;
            }
        }
    }

    private void LeaveAlternateScreen()
    {
        _alternate = null;
        _alternateWrapped = null;
        _pendingWrap = false;
        _scrollTop = 0;
        _scrollBottom = Rows - 1;
    }

    private void RestoreCursor((int Row, int Column, string Style) saved)
    {
        _row = Math.Clamp(saved.Row, 0, Rows - 1);
        _column = Math.Clamp(saved.Column, 0, Columns - 1);
        _style = saved.Style;
        _sgr = SgrState.FromParameters(saved.Style);
        _pendingWrap = false;
    }

    private void Reset(List<string> committed)
    {
        Finish(committed);
        _primary = CreateGrid(Columns, Rows);
        _primaryWrapped = new bool[Rows];
        _row = 0;
        _column = 0;
        _firstLiveRow = 0;
        _pendingWrap = false;
        _autoWrap = true;
        _applicationCursorKeys = false;
        _bracketedPaste = false;
        _cursorVisible = true;
        _style = string.Empty;
        _sgr = new SgrState();
        _scrollTop = 0;
        _scrollBottom = Rows - 1;
    }

    /// <summary>
    /// ينقل المؤشر سطرًا لأسفل ويمرر عند قاع المنطقة. يبقى الصف حيًّا ما دام على الشاشة، لأن
    /// البرنامج قد يعود إليه (ConPTY يعيد رسم الشاشة كلها بعد تغيير الحجم)؛ يُسلَّم حين يخرج بالتمرير.
    /// </summary>
    private void LineFeed(List<string> committed, bool commit)
    {
        if (commit)
        {
            _pendingWrap = false;
        }

        if (_row == _scrollBottom)
        {
            ScrollUp(_scrollTop, _scrollBottom, 1, committed);
        }
        else if (_row < Rows - 1)
        {
            _row++;
        }
    }

    private void ReverseIndex()
    {
        _pendingWrap = false;
        if (_row == _scrollTop)
        {
            ScrollDown(_scrollTop, _scrollBottom, 1);
        }
        else if (_row > 0)
        {
            _row--;
        }
    }

    private void ScrollUp(int top, int bottom, int count, List<string> committed)
    {
        count = Math.Min(count, bottom - top + 1);
        for (int step = 0; step < count; step++)
        {
            // الصف العلوي للمنطقة يخرج: يُسلَّم إن كان حيًّا، وإلا تصعد حدود المنطقة الحية مع الأسطر.
            if (_alternate is null)
            {
                if (top >= _firstLiveRow)
                {
                    CommitRows(top, top, committed);
                }
                else if (_firstLiveRow <= bottom + 1)
                {
                    _firstLiveRow--;
                }
            }

            Cell[][] grid = Grid;
            bool[] wrapped = Wrapped;
            Cell[] recycled = grid[top];
            Array.Copy(grid, top + 1, grid, top, bottom - top);
            Array.Copy(wrapped, top + 1, wrapped, top, bottom - top);
            Array.Fill(recycled, Cell.Blank);
            grid[bottom] = recycled;
            wrapped[bottom] = false;
        }
    }

    private void ScrollDown(int top, int bottom, int count)
    {
        count = Math.Min(count, bottom - top + 1);
        for (int step = 0; step < count; step++)
        {
            Cell[][] grid = Grid;
            bool[] wrapped = Wrapped;
            Cell[] recycled = grid[bottom];
            Array.Copy(grid, top, grid, top + 1, bottom - top);
            Array.Copy(wrapped, top, wrapped, top + 1, bottom - top);
            Array.Fill(recycled, Cell.Blank);
            grid[top] = recycled;
            wrapped[top] = false;
        }
    }

    private void EraseDisplay(int mode, List<string> committed)
    {
        switch (mode)
        {
            case 0:
                ClearCells(_row, _column, Columns);
                for (int row = _row + 1; row < Rows; row++)
                {
                    ClearRow(row);
                }

                break;
            case 1:
                for (int row = 0; row < _row; row++)
                {
                    ClearRow(row);
                }

                ClearCells(_row, 0, _column + 1);
                break;
            case 2 or 3:
                // كما في Windows Terminal: يُدفع محتوى الشاشة الأساسية إلى السجل قبل مسحها.
                if (_alternate is null)
                {
                    CommitRows(_firstLiveRow, LastContentRow(), committed);
                    _firstLiveRow = 0;
                }

                for (int row = 0; row < Rows; row++)
                {
                    ClearRow(row);
                }

                break;
        }
    }

    private void EraseLine(int mode)
    {
        switch (mode)
        {
            case 0:
                ClearCells(_row, _column, Columns);
                Wrapped[_row] = false;
                break;
            case 1:
                ClearCells(_row, 0, _column + 1);
                break;
            case 2:
                ClearRow(_row);
                break;
        }
    }

    private void ClearRow(int row)
    {
        Array.Fill(Grid[row], Cell.Blank);
        Wrapped[row] = false;
    }

    private void ClearCells(int row, int from, int to)
    {
        Array.Fill(Grid[row], Cell.Blank, from, Math.Max(0, to - from));
    }

    private void ShiftCells(int count, bool insert)
    {
        Cell[] row = Grid[_row];
        count = Math.Min(count, Columns - _column);
        if (insert)
        {
            Array.Copy(row, _column, row, _column + count, Columns - _column - count);
            Array.Fill(row, Cell.Blank, _column, count);
        }
        else
        {
            Array.Copy(row, _column + count, row, _column, Columns - _column - count);
            Array.Fill(row, Cell.Blank, Columns - count, count);
        }
    }

    private int LastContentRow()
    {
        int last = -1;
        for (int row = Rows - 1; row >= _firstLiveRow; row--)
        {
            if (RowText(_primary[row], styled: false).Length > 0)
            {
                last = row;
                break;
            }
        }

        return last;
    }

    private void CommitRows(int from, int to, List<string> committed)
    {
        if (_alternate is null)
        {
            AppendLogicalLines(from, to, committed);
        }
    }

    /// <summary>
    /// يضم الصفوف الملفوفة أسطرًا منطقية، ويعيد موضع المؤشر إن وقع صفه في المدى.
    /// </summary>
    private (int Line, int Index) AppendLogicalLines(int from, int to, List<string> lines, int cursorRow = -1)
    {
        var line = new StringBuilder();
        int plainLength = 0;
        (int Line, int Index) cursor = (-1, 0);
        for (int row = Math.Max(0, from); row <= to && row < Rows; row++)
        {
            bool isCursorRow = row == cursorRow;
            if (isCursorRow)
            {
                cursor = (lines.Count, plainLength + PlainLength(_primary[row], _column));
            }

            string text = RowText(_primary[row], styled: true, trimEnd: !_primaryWrapped[row], keep: isCursorRow ? _column : 0);
            line.Append(text);
            plainLength += StripStyles(text).Length;
            if (!_primaryWrapped[row] || row == to)
            {
                lines.Add(line.ToString());
                line.Clear();
                plainLength = 0;
            }
        }

        return cursor;
    }

    private static int PlainLength(Cell[] row, int columns)
    {
        int length = 0;
        for (int column = 0; column < columns && column < row.Length; column++)
        {
            length += Math.Max(1, row[column].Text.Length);
        }

        return length;
    }

    private static string RowText(Cell[] row, bool styled, bool trimEnd = true, int keep = 0)
    {
        int end = row.Length;
        if (trimEnd)
        {
            while (end > keep && row[end - 1].IsBlank)
            {
                end--;
            }
        }

        var text = new StringBuilder(end);
        string style = string.Empty;
        for (int column = 0; column < end; column++)
        {
            Cell cell = row[column];
            if (styled && !ReferenceEquals(cell.Style, style) && cell.Style != style)
            {
                text.Append(Escape).Append("[0").Append(cell.Style.Length > 0 ? ";" : string.Empty)
                    .Append(cell.Style).Append('m');
                style = cell.Style;
            }

            text.Append(cell.Text.Length == 0 ? " " : cell.Text);
        }

        if (styled && style.Length > 0)
        {
            text.Append(Escape).Append("[0m");
        }

        return text.ToString();
    }

    private static Cell[][] CreateGrid(int columns, int rows)
    {
        var grid = new Cell[rows][];
        for (int row = 0; row < rows; row++)
        {
            grid[row] = new Cell[columns];
            Array.Fill(grid[row], Cell.Blank);
        }

        return grid;
    }

    private static Cell[][] CopyGrid(Cell[][] source, int columns, int rows, int shift)
    {
        Cell[][] grid = CreateGrid(columns, rows);
        for (int row = 0; row < rows && row + shift < source.Length; row++)
        {
            Array.Copy(source[row + shift], grid[row], Math.Min(columns, source[row + shift].Length));
        }

        return grid;
    }

    private static bool[] CopyFlags(bool[] source, int rows, int shift)
    {
        var flags = new bool[rows];
        for (int row = 0; row < rows && row + shift < source.Length; row++)
        {
            flags[row] = source[row + shift];
        }

        return flags;
    }

    private readonly record struct Cell(string Text, string Style)
    {
        public static Cell Blank { get; } = new(string.Empty, string.Empty);

        public bool IsBlank => (Text.Length == 0 || Text == " ") && Style.Length == 0;
    }

    /// <summary>
    /// حالة SGR الحالية، تُسلسل إلى معاملات مختصرة («1;31») لتخزن في الخلايا وتعاد بثها.
    /// </summary>
    private sealed class SgrState
    {
        private readonly SortedSet<int> _flags = [];
        private string? _foreground;
        private string? _background;

        public static SgrState FromParameters(string parameters)
        {
            var state = new SgrState();
            if (parameters.Length > 0)
            {
                state.Apply(parameters.Split(';').Select(part => int.Parse(part, CultureInfo.InvariantCulture)).ToArray());
            }

            return state;
        }

        public void Apply(int[] parameters)
        {
            if (parameters.Length == 0)
            {
                Reset();
                return;
            }

            for (int index = 0; index < parameters.Length; index++)
            {
                int code = parameters[index];
                switch (code)
                {
                    case 0:
                        Reset();
                        break;
                    case >= 1 and <= 9:
                        _flags.Add(code);
                        break;
                    case 22:
                        _flags.Remove(1);
                        _flags.Remove(2);
                        break;
                    case >= 23 and <= 29:
                        _flags.Remove(code - 20);
                        break;
                    case (>= 30 and <= 37) or (>= 90 and <= 97):
                        _foreground = code.ToString(CultureInfo.InvariantCulture);
                        break;
                    case 39:
                        _foreground = null;
                        break;
                    case (>= 40 and <= 47) or (>= 100 and <= 107):
                        _background = code.ToString(CultureInfo.InvariantCulture);
                        break;
                    case 49:
                        _background = null;
                        break;
                    case 38 or 48:
                        string? extended = ReadExtendedColor(parameters, ref index);
                        if (extended is not null)
                        {
                            if (code == 38)
                            {
                                _foreground = "38;" + extended;
                            }
                            else
                            {
                                _background = "48;" + extended;
                            }
                        }

                        break;
                }
            }
        }

        public string ToParameters()
        {
            var parts = new List<string>(_flags.Count + 2);
            parts.AddRange(_flags.Select(flag => flag.ToString(CultureInfo.InvariantCulture)));
            if (_foreground is not null)
            {
                parts.Add(_foreground);
            }

            if (_background is not null)
            {
                parts.Add(_background);
            }

            return string.Intern(string.Join(';', parts));
        }

        private static string? ReadExtendedColor(int[] parameters, ref int index)
        {
            if (index + 2 < parameters.Length && parameters[index + 1] == 5)
            {
                string color = "5;" + parameters[index + 2].ToString(CultureInfo.InvariantCulture);
                index += 2;
                return color;
            }

            if (index + 4 < parameters.Length && parameters[index + 1] == 2)
            {
                string color = string.Join(';', parameters.Skip(index + 1).Take(4));
                index += 4;
                return color;
            }

            index = parameters.Length;
            return null;
        }

        private void Reset()
        {
            _flags.Clear();
            _foreground = null;
            _background = null;
        }
    }
}

/// <summary>
/// لقطة الشاشة الحية: أسطرها (بتسلسلات SGR) وموضع المؤشر في نص سطره بلا تسلسلات.
/// </summary>
public sealed record VtLiveView(
    IReadOnlyList<string> Lines,
    int CursorLine,
    int CursorIndex,
    bool ApplicationCursorKeys = false,
    bool BracketedPaste = false)
{
    public static VtLiveView Empty { get; } = new([], -1, 0);
}
