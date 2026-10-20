using ArbSh.Terminal.Models;

namespace ArbSh.Terminal.Input;

/// <summary>
/// يمثل تحديدًا منطقيًا لأسطر المخرجات في الطرفية.
/// Represents a logical selection over terminal output lines.
/// </summary>
public sealed class OutputSelectionBuffer
{
    private int? _anchorLineIndex;
    private int? _activeLineIndex;

    /// <summary>
    /// هل يوجد تحديد نشط في المخرجات.
    /// Indicates whether an output selection is active.
    /// </summary>
    public bool HasSelection => _anchorLineIndex.HasValue && _activeLineIndex.HasValue;

    /// <summary>
    /// يمسح حالة التحديد الحالية.
    /// Clears the current selection state.
    /// </summary>
    public void Clear()
    {
        _anchorLineIndex = null;
        _activeLineIndex = null;
    }

    /// <summary>
    /// يبدأ تحديدًا جديدًا أو يمدد التحديد الحالي.
    /// Starts a new selection or extends the current one.
    /// </summary>
    /// <param name="lineIndex">فهرس السطر المنطقي.</param>
    /// <param name="extendSelection">تمديد التحديد الحالي.</param>
    public void BeginOrExtend(int lineIndex, bool extendSelection)
    {
        int clamped = Math.Max(0, lineIndex);

        if (!extendSelection || !_anchorLineIndex.HasValue)
        {
            _anchorLineIndex = clamped;
        }

        _activeLineIndex = clamped;
    }

    /// <summary>
    /// يحدّث الطرف النشط أثناء السحب بالماوس.
    /// Updates active edge while pointer-dragging.
    /// </summary>
    /// <param name="lineIndex">فهرس السطر المنطقي الجديد.</param>
    public void UpdateActive(int lineIndex)
    {
        if (!_anchorLineIndex.HasValue)
        {
            return;
        }

        _activeLineIndex = Math.Max(0, lineIndex);
    }

    /// <summary>
    /// يحاول إرجاع حدود التحديد (شاملة الطرفين).
    /// Tries to return selection bounds (inclusive).
    /// </summary>
    /// <param name="startLineIndex">أول سطر محدد.</param>
    /// <param name="endLineIndex">آخر سطر محدد.</param>
    /// <returns>صحيح إذا كان التحديد نشطًا.</returns>
    public bool TryGetRange(out int startLineIndex, out int endLineIndex)
    {
        startLineIndex = 0;
        endLineIndex = 0;

        if (!HasSelection)
        {
            return false;
        }

        int anchor = _anchorLineIndex!.Value;
        int active = _activeLineIndex!.Value;
        startLineIndex = Math.Min(anchor, active);
        endLineIndex = Math.Max(anchor, active);
        return true;
    }

    /// <summary>
    /// يحول التحديد إلى نص قابل للنسخ بترتيب منطقي.
    /// Converts selection to copy-ready text in logical order.
    /// </summary>
    /// <param name="lines">قائمة الأسطر المنطقية الحالية.</param>
    /// <returns>النص المحدد أو سلسلة فارغة.</returns>
    public string GetSelectedText(IReadOnlyList<TerminalLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (!TryGetRange(out int start, out int end) || lines.Count == 0)
        {
            return string.Empty;
        }

        int clampedStart = Math.Clamp(start, 0, lines.Count - 1);
        int clampedEnd = Math.Clamp(end, 0, lines.Count - 1);
        if (clampedEnd < clampedStart)
        {
            return string.Empty;
        }

        var selected = new List<string>(clampedEnd - clampedStart + 1);
        for (int i = clampedStart; i <= clampedEnd; i++)
        {
            selected.Add(StripDirectionalIsolates(lines[i].Text));
        }

        return string.Join(Environment.NewLine, selected);
    }

    // تضبط أسطر الموجه اتجاه المسار بعلامات غير مرئية؛ لا تُنسخ إلى الحافظة.
    private static string StripDirectionalIsolates(string text)
    {
        return text.IndexOfAny(DirectionalMarks) < 0
            ? text
            : string.Concat(text.Where(c => Array.IndexOf(DirectionalMarks, c) < 0));
    }

    private static readonly char[] DirectionalMarks = ['\u200E', '\u200F', '\u2066', '\u2067', '\u2068', '\u2069'];
}
