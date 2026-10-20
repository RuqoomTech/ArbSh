using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using ArbSh.Core.I18n;
using ArbSh.Terminal.Input;
using ArbSh.Terminal.Models;
using ArbSh.Terminal.ViewModels;

namespace ArbSh.Terminal.Rendering;

public sealed class TerminalSurface : Control
{
    private const double CaretDistanceEpsilon = 0.01;
    private const int PageScrollOverlapLines = 1;
    private const double FontZoomStep = 2;

    private MainWindowViewModel? _viewModel;
    private bool _isPromptPointerSelecting;
    private bool _isOutputPointerSelecting;
    private int _scrollbackOffsetLines;
    private int _lastKnownLineCount;

    private readonly TerminalInputBuffer _inputBuffer = new();
    private readonly OutputSelectionBuffer _outputSelection = new();
    private readonly TerminalRenderConfig _renderConfig = new();
    private readonly TerminalTextPipeline _textPipeline = new();
    private readonly TerminalLayoutEngine _layoutEngine = new();

    private PromptLayoutSnapshot? _promptSnapshot;
    private TerminalFrameLayout? _frameSnapshot;
    private string _frameSnapshotInputText = string.Empty;
    private int _frameSnapshotLineCount;
    private Size _frameSnapshotSize;

    public TerminalSurface()
    {
        Focusable = true;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.BufferChanged -= HandleBufferChanged;
            _viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.BufferChanged += HandleBufferChanged;
            _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
            _lastKnownLineCount = _viewModel.Lines.Count;
        }
        else
        {
            _lastKnownLineCount = 0;
        }

        _scrollbackOffsetLines = 0;
        _outputSelection.Clear();
        _frameSnapshot = null;
        _frameSnapshotInputText = string.Empty;
        _frameSnapshotLineCount = _lastKnownLineCount;
        _frameSnapshotSize = Bounds.Size;
        _promptSnapshot = null;

        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Focus();

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Point point = e.GetPosition(this);
            bool extendSelection = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            if (TryBeginOutputSelection(point, extendSelection))
            {
                _isOutputPointerSelecting = true;
                _isPromptPointerSelecting = false;
                e.Pointer.Capture(this);
                e.Handled = true;
            }
            else if (UpdateCaretFromPointer(point, extendSelection))
            {
                _outputSelection.Clear();
                _isPromptPointerSelecting = true;
                _isOutputPointerSelecting = false;
                e.Pointer.Capture(this);
                e.Handled = true;
            }
        }

        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            base.OnPointerMoved(e);
            return;
        }

        if (_isOutputPointerSelecting)
        {
            UpdateOutputSelectionFromPointer(e.GetPosition(this));
            e.Handled = true;
            base.OnPointerMoved(e);
            return;
        }

        if (_isPromptPointerSelecting)
        {
            UpdateCaretFromPointer(e.GetPosition(this), extendSelection: true);
            e.Handled = true;
        }

        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_isPromptPointerSelecting || _isOutputPointerSelecting)
        {
            _isPromptPointerSelecting = false;
            _isOutputPointerSelecting = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        base.OnPointerReleased(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            int zoomDirection = Math.Sign(e.Delta.Y);
            if (zoomDirection != 0)
            {
                ChangeFontSize(zoomDirection * FontZoomStep);
                e.Handled = true;
                return;
            }
        }

        if (_viewModel is null)
        {
            base.OnPointerWheelChanged(e);
            return;
        }

        int direction = Math.Sign(e.Delta.Y);
        if (direction == 0 || !TryGetFrameSnapshot(out TerminalFrameLayout frame))
        {
            base.OnPointerWheelChanged(e);
            return;
        }

        int wheelSteps = Math.Max(1, (int)Math.Ceiling(Math.Abs(e.Delta.Y)));
        int deltaLines = wheelSteps * _renderConfig.ScrollLinesPerWheelStep;

        if (direction > 0)
        {
            ScrollbackBy(deltaLines, frame);
            e.Handled = true;
            return;
        }

        ScrollbackBy(-deltaLines, frame);
        e.Handled = true;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        bool hasPrintableChar = e.Text.Any(c => !char.IsControl(c));
        if (!hasPrintableChar)
        {
            return;
        }

        _outputSelection.Clear();
        _inputBuffer.InsertText(e.Text);
        _scrollbackOffsetLines = 0;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.A:
                    _outputSelection.Clear();
                    _inputBuffer.SelectAll();
                    InvalidateVisual();
                    e.Handled = true;
                    return;

                case Key.C:
                    await CopySelectionAsync();
                    e.Handled = true;
                    return;

                case Key.X:
                    await CutSelectionAsync();
                    InvalidateVisual();
                    e.Handled = true;
                    return;

                case Key.V:
                    await PasteClipboardAsync();
                    InvalidateVisual();
                    e.Handled = true;
                    return;

                case Key.Add:
                case Key.OemPlus:
                    ChangeFontSize(FontZoomStep);
                    e.Handled = true;
                    return;

                case Key.Subtract:
                case Key.OemMinus:
                    ChangeFontSize(-FontZoomStep);
                    e.Handled = true;
                    return;

                case Key.D0:
                case Key.NumPad0:
                    ResetFontSize();
                    e.Handled = true;
                    return;
            }
        }

        switch (e.Key)
        {
            case Key.PageUp:
                ScrollbackByPage(upward: true);
                InvalidateVisual();
                e.Handled = true;
                break;

            case Key.PageDown:
                ScrollbackByPage(upward: false);
                InvalidateVisual();
                e.Handled = true;
                break;

            case Key.Left:
                _outputSelection.Clear();
                MoveCaretVisual(moveLeft: true, extendSelection: shift);
                InvalidateVisual();
                e.Handled = true;
                break;

            case Key.Right:
                _outputSelection.Clear();
                MoveCaretVisual(moveLeft: false, extendSelection: shift);
                InvalidateVisual();
                e.Handled = true;
                break;

            case Key.Home:
                _outputSelection.Clear();
                _inputBuffer.MoveCaretHome(shift);
                InvalidateVisual();
                e.Handled = true;
                break;

            case Key.End:
                _outputSelection.Clear();
                _inputBuffer.MoveCaretEnd(shift);
                InvalidateVisual();
                e.Handled = true;
                break;

            case Key.Back:
                _outputSelection.Clear();
                _inputBuffer.Backspace();
                _scrollbackOffsetLines = 0;
                InvalidateVisual();
                e.Handled = true;
                break;

            case Key.Delete:
                _outputSelection.Clear();
                _inputBuffer.DeleteForward();
                _scrollbackOffsetLines = 0;
                InvalidateVisual();
                e.Handled = true;
                break;

            case Key.Escape:
                _inputBuffer.ClearSelection();
                _outputSelection.Clear();
                InvalidateVisual();
                e.Handled = true;
                break;

            case Key.Enter:
                _outputSelection.Clear();
                _scrollbackOffsetLines = 0;
                await SubmitInputBufferAsync();
                e.Handled = true;
                break;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.DrawRectangle(_renderConfig.BackgroundBrush, null, new Rect(Bounds.Size));

        if (_viewModel is null)
        {
            return;
        }

        IReadOnlyList<TerminalLine> lineSnapshot = [.. _viewModel.Lines];
        TerminalFrameLayout frame = _layoutEngine.BuildFrameLayout(
            lineSnapshot,
            _viewModel.Prompt,
            _inputBuffer.Text,
            Bounds.Size,
            _renderConfig,
            _textPipeline,
            _scrollbackOffsetLines);

        _scrollbackOffsetLines = frame.ScrollbackOffsetLines;
        _frameSnapshot = frame;
        _frameSnapshotInputText = _inputBuffer.Text;
        _frameSnapshotLineCount = lineSnapshot.Count;
        _frameSnapshotSize = Bounds.Size;

        _promptSnapshot = null;
        DrawOutputSelection(context, frame);

        foreach (TerminalDrawInstruction instruction in frame.Instructions)
        {
            if (instruction.IsPromptLine)
            {
                DrawPromptLine(context, instruction);
                continue;
            }

            DrawOutputLine(context, instruction);
        }

        if (IsFocused && _promptSnapshot is not null)
        {
            DrawCaret(context, _promptSnapshot);
        }
    }

    private void DrawPromptLine(DrawingContext context, TerminalDrawInstruction instruction)
    {
        if (_viewModel is null)
        {
            return;
        }

        bool isPromptRtl = IsTextRtl(instruction.Run.LogicalText);
        FlowDirection flow = isPromptRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        TextLayout layout = _renderConfig.CreateTextLayout(instruction.Run.LogicalText, instruction.Brush, flow);
        var snapshot = new PromptLayoutSnapshot(layout, instruction.Position, instruction.Run.LogicalText, _viewModel.Prompt.Length);
        _promptSnapshot = snapshot;

        DrawSelection(context, snapshot);
        layout.Draw(context, instruction.Position);
    }

    private void DrawOutputLine(DrawingContext context, TerminalDrawInstruction instruction)
    {
        if (string.IsNullOrEmpty(instruction.Run.VisualText))
        {
            return;
        }

        bool isRtl = IsTextRtl(instruction.Run.VisualText);
        FlowDirection flow = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        TextLayout layout = _renderConfig.CreateTextLayout(instruction.Run.VisualText, instruction.Brush, flow);
        DrawAnsiBackgrounds(context, instruction, layout);

        FormattedText formatted = _renderConfig.CreateFormattedText(instruction.Run.VisualText, instruction.Brush, flow);
        ApplyAnsiForegroundStyles(formatted, instruction);
        context.DrawText(formatted, instruction.Position + new Vector(0, _renderConfig.GetBaselineOffset(formatted.Baseline)));
    }

    private void DrawSelection(DrawingContext context, PromptLayoutSnapshot snapshot)
    {
        if (_inputBuffer.Selection is not { HasSelection: true } selection)
        {
            return;
        }

        IReadOnlyList<Rect> rects = snapshot.GetSelectionRects(selection.Start, selection.Length);
        foreach (Rect rect in rects)
        {
            context.DrawRectangle(_renderConfig.SelectionBrush, null, rect);
        }
    }

    private void DrawOutputSelection(DrawingContext context, TerminalFrameLayout frame)
    {
        if (!_outputSelection.TryGetRange(out int selectionStart, out int selectionEnd))
        {
            return;
        }

        double width = Math.Max(0, Bounds.Width - _renderConfig.Padding.Left - _renderConfig.Padding.Right);
        if (width <= 0)
        {
            return;
        }

        foreach (TerminalDrawInstruction instruction in frame.Instructions)
        {
            if (instruction.IsPromptLine || instruction.LogicalLineIndex < selectionStart || instruction.LogicalLineIndex > selectionEnd)
            {
                continue;
            }

            var rect = new Rect(_renderConfig.Padding.Left, instruction.Position.Y, width, _renderConfig.LineHeight);
            context.DrawRectangle(_renderConfig.OutputSelectionBrush, null, rect);
        }
    }

    private void DrawAnsiBackgrounds(DrawingContext context, TerminalDrawInstruction instruction, TextLayout layout)
    {
        if (instruction.Run.StyleSpans.Count == 0 || layout.TextLines.Count == 0)
        {
            return;
        }

        TextLine line = layout.TextLines[0];
        int textLength = instruction.Run.VisualText.Length;

        foreach (AnsiStyleSpan span in instruction.Run.StyleSpans)
        {
            if (!TryClampSpan(span.Start, span.Length, textLength, out int start, out int length))
            {
                continue;
            }

            IBrush? background = _renderConfig.ResolveAnsiBackgroundBrush(instruction.Run.Kind, span.Style);
            if (background is null)
            {
                continue;
            }

            IReadOnlyList<TextBounds> bounds = line.GetTextBounds(start, length);
            foreach (TextBounds bound in bounds)
            {
                Rect rect = bound.Rectangle;
                context.DrawRectangle(
                    background,
                    null,
                    new Rect(
                        instruction.Position.X + rect.X,
                        instruction.Position.Y + rect.Y,
                        rect.Width,
                        rect.Height));
            }
        }
    }

    private void ApplyAnsiForegroundStyles(FormattedText formatted, TerminalDrawInstruction instruction)
    {
        if (instruction.Run.StyleSpans.Count == 0)
        {
            return;
        }

        int textLength = instruction.Run.VisualText.Length;
        foreach (AnsiStyleSpan span in instruction.Run.StyleSpans)
        {
            if (!TryClampSpan(span.Start, span.Length, textLength, out int start, out int length))
            {
                continue;
            }

            AnsiStyleState style = span.Style;
            if (style.IsDefault)
            {
                continue;
            }

            IBrush fg = _renderConfig.ResolveAnsiForegroundBrush(instruction.Run.Kind, style);
            formatted.SetForegroundBrush(fg, start, length);

            if (style.Bold)
            {
                formatted.SetFontWeight(_renderConfig.ResolveAnsiFontWeight(style), start, length);
            }

            if (style.Italic)
            {
                formatted.SetFontStyle(_renderConfig.ResolveAnsiFontStyle(style), start, length);
            }

            TextDecorationCollection? decorations = _renderConfig.ResolveAnsiTextDecorations(style);
            if (decorations is not null)
            {
                formatted.SetTextDecorations(decorations, start, length);
            }
        }
    }

    private static bool TryClampSpan(int start, int length, int maxLength, out int clampedStart, out int clampedLength)
    {
        clampedStart = 0;
        clampedLength = 0;

        if (length <= 0 || maxLength <= 0)
        {
            return false;
        }

        int safeStart = Math.Clamp(start, 0, maxLength);
        int safeEnd = Math.Clamp(start + length, 0, maxLength);
        if (safeEnd <= safeStart)
        {
            return false;
        }

        clampedStart = safeStart;
        clampedLength = safeEnd - safeStart;
        return true;
    }

    private void DrawCaret(DrawingContext context, PromptLayoutSnapshot snapshot)
    {
        double cursorX = snapshot.GetCaretXForInputIndex(_inputBuffer.CaretIndex);

        double minCursorX = _renderConfig.Padding.Left;
        double maxCursorX = Math.Max(minCursorX, Bounds.Width - _renderConfig.Padding.Right);
        cursorX = Math.Clamp(cursorX, minCursorX, maxCursorX);

        double cursorY = snapshot.Origin.Y + 2;
        var cursorRect = new Rect(cursorX, cursorY, 2, Math.Max(6, _renderConfig.LineHeight - 6));
        context.DrawRectangle(_renderConfig.PromptBrush, null, cursorRect);
    }

    private async Task SubmitInputBufferAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        string input = _inputBuffer.Text;
        _inputBuffer.Clear();
        _scrollbackOffsetLines = 0;
        _outputSelection.Clear();
        InvalidateVisual();

        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        await _viewModel.SubmitInputAsync(input);
    }

    private void MoveCaretVisual(bool moveLeft, bool extendSelection)
    {
        if (!TryGetPromptSnapshot(out PromptLayoutSnapshot snapshot) || snapshot.Layout.TextLines.Count == 0)
        {
            if (moveLeft)
            {
                _inputBuffer.MoveCaretLeft(extendSelection);
            }
            else
            {
                _inputBuffer.MoveCaretRight(extendSelection);
            }

            return;
        }

        TextLine line = snapshot.Layout.TextLines[0];

        int minFull = snapshot.PromptLength;
        int maxFull = snapshot.PromptLength + _inputBuffer.Text.Length;
        int fullIndex = Math.Clamp(snapshot.PromptLength + _inputBuffer.CaretIndex, minFull, maxFull);

        var currentHit = new CharacterHit(fullIndex, 0);
        double currentDistance = line.GetDistanceFromCharacterHit(currentHit);

        CharacterHit previousHit = line.GetPreviousCaretCharacterHit(currentHit);
        CharacterHit nextHit = line.GetNextCaretCharacterHit(currentHit);

        int previousIndex = Math.Clamp(ToFullIndex(previousHit), minFull, maxFull);
        int nextIndex = Math.Clamp(ToFullIndex(nextHit), minFull, maxFull);

        double previousDistance = line.GetDistanceFromCharacterHit(previousHit);
        double nextDistance = line.GetDistanceFromCharacterHit(nextHit);

        int targetFullIndex = fullIndex;

        if (moveLeft)
        {
            bool previousIsLeft = previousDistance < currentDistance - CaretDistanceEpsilon;
            bool nextIsLeft = nextDistance < currentDistance - CaretDistanceEpsilon;

            if (previousIsLeft && nextIsLeft)
            {
                targetFullIndex = previousDistance > nextDistance ? previousIndex : nextIndex;
            }
            else if (previousIsLeft)
            {
                targetFullIndex = previousIndex;
            }
            else if (nextIsLeft)
            {
                targetFullIndex = nextIndex;
            }
            else
            {
                if (previousIndex < fullIndex || nextIndex < fullIndex)
                {
                    int candidate = fullIndex;
                    if (previousIndex < fullIndex)
                    {
                        candidate = previousIndex;
                    }

                    if (nextIndex < fullIndex)
                    {
                        candidate = Math.Max(candidate, nextIndex);
                    }

                    targetFullIndex = candidate;
                }
            }
        }
        else
        {
            bool previousIsRight = previousDistance > currentDistance + CaretDistanceEpsilon;
            bool nextIsRight = nextDistance > currentDistance + CaretDistanceEpsilon;

            if (previousIsRight && nextIsRight)
            {
                targetFullIndex = previousDistance < nextDistance ? previousIndex : nextIndex;
            }
            else if (previousIsRight)
            {
                targetFullIndex = previousIndex;
            }
            else if (nextIsRight)
            {
                targetFullIndex = nextIndex;
            }
            else
            {
                if (previousIndex > fullIndex || nextIndex > fullIndex)
                {
                    int candidate = fullIndex;
                    if (previousIndex > fullIndex)
                    {
                        candidate = previousIndex;
                    }

                    if (nextIndex > fullIndex)
                    {
                        candidate = Math.Min(candidate, nextIndex);
                    }

                    targetFullIndex = candidate;
                }
            }
        }

        if (targetFullIndex == fullIndex)
        {
            if (moveLeft)
            {
                _inputBuffer.MoveCaretLeft(extendSelection);
            }
            else
            {
                _inputBuffer.MoveCaretRight(extendSelection);
            }

            return;
        }

        int targetInputIndex = Math.Clamp(targetFullIndex - snapshot.PromptLength, 0, _inputBuffer.Text.Length);
        _inputBuffer.SetCaretFromLogicalIndex(targetInputIndex, extendSelection);
    }

    private bool UpdateCaretFromPointer(Point point, bool extendSelection)
    {
        if (!TryGetPromptSnapshot(out PromptLayoutSnapshot snapshot) || !IsPointOnPromptLine(point, snapshot))
        {
            return false;
        }

        int inputIndex = snapshot.GetInputIndexFromPoint(point);
        _inputBuffer.SetCaretFromLogicalIndex(inputIndex, extendSelection);
        _scrollbackOffsetLines = 0;
        InvalidateVisual();
        return true;
    }

    private bool TryGetPromptSnapshot(out PromptLayoutSnapshot snapshot)
    {
        snapshot = null!;

        if (_viewModel is null)
        {
            return false;
        }

        string expectedText = string.Concat(_viewModel.Prompt, _inputBuffer.Text);
        if (_promptSnapshot is not null && _promptSnapshot.LogicalText == expectedText)
        {
            snapshot = _promptSnapshot;
            return true;
        }

        if (!TryGetFrameSnapshot(out TerminalFrameLayout frame))
        {
            return false;
        }

        TerminalDrawInstruction? promptInstruction = frame.Instructions.FirstOrDefault(x => x.IsPromptLine);
        if (promptInstruction is null)
        {
            return false;
        }

        bool isPromptRtl = IsTextRtl(promptInstruction.Run.LogicalText);
        FlowDirection flow = isPromptRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        TextLayout layout = _renderConfig.CreateTextLayout(promptInstruction.Run.LogicalText, promptInstruction.Brush, flow);
        _promptSnapshot = new PromptLayoutSnapshot(layout, promptInstruction.Position, promptInstruction.Run.LogicalText, _viewModel.Prompt.Length);

        snapshot = _promptSnapshot;
        return true;
    }

    private bool IsPointOnPromptLine(Point point, PromptLayoutSnapshot snapshot)
    {
        double top = snapshot.Origin.Y;
        double bottom = top + _renderConfig.LineHeight;
        return point.Y >= top && point.Y <= bottom;
    }

    private bool TryBeginOutputSelection(Point point, bool extendSelection)
    {
        if (!TryGetFrameSnapshot(out TerminalFrameLayout frame))
        {
            return false;
        }

        if (!TryGetOutputLineIndexFromPoint(point, frame, out int lineIndex))
        {
            return false;
        }

        _outputSelection.BeginOrExtend(lineIndex, extendSelection);
        InvalidateVisual();
        return true;
    }

    private void UpdateOutputSelectionFromPointer(Point point)
    {
        if (!TryGetFrameSnapshot(out TerminalFrameLayout frame))
        {
            return;
        }

        if (!TryGetOutputLineIndexFromPoint(point, frame, out int lineIndex))
        {
            return;
        }

        _outputSelection.UpdateActive(lineIndex);
        InvalidateVisual();
    }

    private bool TryGetOutputLineIndexFromPoint(Point point, TerminalFrameLayout frame, out int lineIndex)
    {
        lineIndex = -1;

        IReadOnlyList<TerminalDrawInstruction> outputLines = frame.Instructions.Where(x => !x.IsPromptLine).ToList();
        if (outputLines.Count == 0)
        {
            return false;
        }

        double top = outputLines[0].Position.Y;
        double bottom = outputLines[^1].Position.Y + _renderConfig.LineHeight;
        if (point.Y < top || point.Y > bottom)
        {
            return false;
        }

        int row = (int)Math.Floor((point.Y - top) / _renderConfig.LineHeight);
        row = Math.Clamp(row, 0, outputLines.Count - 1);

        lineIndex = outputLines[row].LogicalLineIndex;
        return lineIndex >= 0;
    }

    private bool TryGetFrameSnapshot(out TerminalFrameLayout frame)
    {
        frame = null!;

        if (_viewModel is null)
        {
            return false;
        }

        bool isSnapshotCurrent = _frameSnapshot is not null
            && _frameSnapshotInputText == _inputBuffer.Text
            && _frameSnapshotLineCount == _viewModel.Lines.Count
            && _frameSnapshotSize == Bounds.Size
            && _frameSnapshot.ScrollbackOffsetLines == _scrollbackOffsetLines;

        if (isSnapshotCurrent)
        {
            frame = _frameSnapshot!;
            return true;
        }

        IReadOnlyList<TerminalLine> lineSnapshot = [.. _viewModel.Lines];
        frame = _layoutEngine.BuildFrameLayout(
            lineSnapshot,
            _viewModel.Prompt,
            _inputBuffer.Text,
            Bounds.Size,
            _renderConfig,
            _textPipeline,
            _scrollbackOffsetLines);

        _frameSnapshot = frame;
        _frameSnapshotInputText = _inputBuffer.Text;
        _frameSnapshotLineCount = lineSnapshot.Count;
        _frameSnapshotSize = Bounds.Size;
        _scrollbackOffsetLines = frame.ScrollbackOffsetLines;
        return true;
    }

    private void ScrollbackByPage(bool upward)
    {
        if (!TryGetFrameSnapshot(out TerminalFrameLayout frame))
        {
            return;
        }

        int pageSize = Math.Max(1, frame.MaxVisibleOutputLines - PageScrollOverlapLines);
        int delta = upward ? pageSize : -pageSize;
        ScrollbackBy(delta, frame);
    }

    private void ChangeFontSize(double delta)
    {
        if (_renderConfig.SetFontSize(_renderConfig.FontSize + delta))
        {
            InvalidateLayoutSnapshots();
        }
    }

    private void ResetFontSize()
    {
        if (_renderConfig.ResetFontSize())
        {
            InvalidateLayoutSnapshots();
        }
    }

    private void InvalidateLayoutSnapshots()
    {
        _frameSnapshot = null;
        _promptSnapshot = null;
        _frameSnapshotSize = default;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void ScrollbackBy(int deltaLines, TerminalFrameLayout frame)
    {
        if (deltaLines == 0)
        {
            return;
        }

        int target = _scrollbackOffsetLines + deltaLines;
        int clamped = Math.Clamp(target, 0, frame.MaxScrollbackOffsetLines);
        if (clamped == _scrollbackOffsetLines)
        {
            return;
        }

        _scrollbackOffsetLines = clamped;
        _promptSnapshot = null;
        _frameSnapshot = null;
        InvalidateVisual();
    }

    private async Task CopySelectionAsync()
    {
        string selected = string.Empty;

        if (_outputSelection.HasSelection && _viewModel is not null)
        {
            selected = _outputSelection.GetSelectedText([.. _viewModel.Lines]);
        }
        else if (_inputBuffer.HasSelection)
        {
            selected = _inputBuffer.GetSelectedText();
        }

        if (string.IsNullOrEmpty(selected))
        {
            return;
        }

        IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(selected);
    }

    private async Task CutSelectionAsync()
    {
        if (!_inputBuffer.HasSelection)
        {
            return;
        }

        string selected = _inputBuffer.GetSelectedText();
        if (string.IsNullOrEmpty(selected))
        {
            return;
        }

        IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(selected);
        _inputBuffer.DeleteSelectionIfAny();
    }

    private async Task PasteClipboardAsync()
    {
        IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        string? text = await clipboard.TryGetTextAsync();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _outputSelection.Clear();
        _inputBuffer.InsertText(text);
        _scrollbackOffsetLines = 0;
    }

    private static int ToFullIndex(CharacterHit hit)
    {
        return hit.FirstCharacterIndex + Math.Max(0, hit.TrailingLength);
    }

    internal static bool IsTextRtl(string logical)
    {
        if (string.IsNullOrWhiteSpace(logical))
        {
            return false;
        }

        try
        {
            var runs = BidiAlgorithm.ProcessRuns(logical, -1);
            return runs.Count > 0 && (runs[0].Level % 2 != 0);
        }
        catch
        {
            return false;
        }
    }

    private void HandleViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // يتغير الموجه مع المجلد الحالي ولو لم يطبع الأمر شيئًا.
        if (e.PropertyName == nameof(MainWindowViewModel.Prompt))
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _frameSnapshot = null;
                _promptSnapshot = null;
                InvalidateVisual();
            });
        }
    }

    private void HandleBufferChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            int currentCount = _viewModel.Lines.Count;
            int delta = currentCount - _lastKnownLineCount;

            if (delta > 0 && _scrollbackOffsetLines > 0)
            {
                _scrollbackOffsetLines += delta;
            }

            _lastKnownLineCount = currentCount;
        }

        _frameSnapshot = null;
        _promptSnapshot = null;
        InvalidateVisual();
    }
}
