using Avalonia.Media;

namespace ArbSh.Terminal.Rendering;

/// <summary>
/// سمات ألوان واجهة طرفية ArbSh.
/// ArbSh terminal theme colors.
/// </summary>
public sealed class TerminalTheme
{
    /// <summary>
    /// لون خلفية الطرفية الأساسي.
    /// Terminal background color.
    /// </summary>
    public Color Background { get; init; } = Color.Parse("#0C0C0C");

    /// <summary>
    /// اللون الأساسي للنص العام.
    /// Default foreground color.
    /// </summary>
    public Color Foreground { get; init; } = Color.Parse("#CCCCCC");

    /// <summary>
    /// لون نص الموجه.
    /// Prompt foreground color.
    /// </summary>
    public Color PromptForeground { get; init; } = Color.Parse("#CCCCCC");

    /// <summary>
    /// لون سطر النظام.
    /// System line color.
    /// </summary>
    public Color SystemForeground { get; init; } = Color.Parse("#CCCCCC");

    /// <summary>
    /// لون سطر الإدخال.
    /// Input line color.
    /// </summary>
    public Color InputForeground { get; init; } = Color.Parse("#CCCCCC");

    /// <summary>
    /// لون التحذير.
    /// Warning line color.
    /// </summary>
    public Color WarningForeground { get; init; } = Color.Parse("#F9F1A5");

    /// <summary>
    /// لون الأخطاء.
    /// Error line color.
    /// </summary>
    public Color ErrorForeground { get; init; } = Color.Parse("#E74856");

    /// <summary>
    /// لون الرسائل التشخيصية.
    /// Debug line color.
    /// </summary>
    public Color DebugForeground { get; init; } = Color.Parse("#8A8A8A");

    /// <summary>
    /// لون تحديد الإدخال.
    /// Input selection background color.
    /// </summary>
    public Color InputSelectionBackground { get; init; } = Color.FromArgb(96, 255, 255, 255);

    /// <summary>
    /// لون تحديد المخرجات.
    /// Output selection background color.
    /// </summary>
    public Color OutputSelectionBackground { get; init; } = Color.FromArgb(96, 255, 255, 255);

    /// <summary>
    /// ينشئ سمة Campbell الافتراضية في Windows Terminal.
    /// Creates Windows Terminal's default Campbell theme.
    /// </summary>
    /// <returns>سمة كاملة.</returns>
    public static TerminalTheme CreateCampbell()
    {
        return new TerminalTheme();
    }
}
