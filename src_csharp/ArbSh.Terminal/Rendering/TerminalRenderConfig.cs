using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using ArbSh.Terminal.Models;

namespace ArbSh.Terminal.Rendering;

/// <summary>
/// إعدادات العرض الخاصة بسطح الطرفية (الخطوط، الألوان، الهوامش).
/// Terminal rendering configuration (fonts, colors, and spacing).
/// </summary>
public sealed class TerminalRenderConfig
{
    /// <summary>
    /// مقاس الخط الافتراضي المقروء على شاشات سطح المكتب عالية الكثافة.
    /// </summary>
    public const double DefaultFontSize = 16;

    /// <summary>
    /// أصغر وأكبر مقاس مسموح بهما لتكبير الطرفية.
    /// </summary>
    public const double MinimumFontSize = 12;
    public const double MaximumFontSize = 32;

    /// <summary>
    /// سمة Campbell المستخدمة في ألوان الواجهة الافتراضية.
    /// Campbell theme used for default terminal colors.
    /// </summary>
    public TerminalTheme Theme { get; init; } = TerminalTheme.CreateCampbell();

    /// <summary>
    /// لوحة ألوان ANSI المستخدمة لحل الأكواد المفهرسة.
    /// ANSI palette used to resolve indexed color codes.
    /// </summary>
    public AnsiPalette Palette { get; init; } = AnsiPalette.CreateCampbell();

    /// <summary>
    /// عائلة الخط الأساسية: Cascadia Mono للاتينية، وCairo للعربية لأن الأول لا يحوي حروفًا عربية.
    /// Primary font family: Cascadia Mono for Latin text, falling back to Cairo for Arabic.
    /// </summary>
    public FontFamily FontFamily { get; init; } = new(
        "avares://ArbSh.Terminal/Assets/Fonts/CascadiaMono.ttf#Cascadia Mono, " +
        "avares://ArbSh.Terminal/Assets/Fonts/Cairo.ttf#Cairo, " +
        "Cascadia Mono, Consolas, Segoe UI, Noto Sans Arabic UI, Tahoma, Courier New");

    /// <summary>
    /// خط المقاطع العربية التي يحددها <see cref="ArabicTextRuns"/>.
    /// Font for the Arabic runs found by <see cref="ArabicTextRuns"/>.
    /// </summary>
    public FontFamily ArabicFontFamily { get; init; } = new(
        "avares://ArbSh.Terminal/Assets/Fonts/Cairo.ttf#Cairo, " +
        "Segoe UI, Noto Sans Arabic UI, Tahoma");

    /// <summary>
    /// مقاس الخط المستخدم في الرسم.
    /// Font size used for drawing.
    /// </summary>
    public double FontSize { get; private set; } = DefaultFontSize;

    private double _referenceBaseline;
    private double _referenceBaselineFontSize = double.NaN;

    /// <summary>
    /// ارتفاع السطر في مساحة الرسم.
    /// Line height for terminal rows.
    /// </summary>
    public double LineHeight { get; set; } = 26;

    /// <summary>
    /// الهوامش الداخلية لسطح الرسم.
    /// Surface padding.
    /// </summary>
    public Thickness Padding { get; init; } = new(12, 10, 12, 10);

    /// <summary>
    /// لون الخلفية.
    /// Surface background brush.
    /// </summary>
    public IBrush BackgroundBrush { get; init; } = new SolidColorBrush(Color.Parse("#0C0C0C"));

    /// <summary>
    /// لون سطر الموجّه (Prompt).
    /// Prompt line brush.
    /// </summary>
    public IBrush PromptBrush { get; init; } = new SolidColorBrush(Color.Parse("#CCCCCC"));

    /// <summary>
    /// لون خلفية التحديد داخل سطر الإدخال.
    /// Selection background brush for input line.
    /// </summary>
    public IBrush SelectionBrush { get; init; } = new SolidColorBrush(Color.FromArgb(96, 255, 255, 255));

    /// <summary>
    /// لون خلفية تحديد أسطر المخرجات.
    /// Selection background brush for output lines.
    /// </summary>
    public IBrush OutputSelectionBrush { get; init; } = new SolidColorBrush(Color.FromArgb(96, 255, 255, 255));

    /// <summary>
    /// عدد الأسطر التي يتم تمريرها لكل خطوة عجلة.
    /// Number of lines to scroll per wheel step.
    /// </summary>
    public int ScrollLinesPerWheelStep { get; init; } = 3;

    /// <summary>
    /// نوع الخط المستخدم في الرسم.
    /// Typeface used by Avalonia text layout.
    /// </summary>
    public Typeface Typeface => new(FontFamily, FontStyle.Normal, FontWeight.Normal);

    /// <summary>
    /// يغيّر مقاس خط الطرفية ضمن حدود قابلة للقراءة ويضبط ارتفاع السطر معه.
    /// </summary>
    /// <param name="fontSize">المقاس المطلوب بوحدات Avalonia المنطقية.</param>
    /// <returns>true إذا تغير المقاس فعليًا.</returns>
    public bool SetFontSize(double fontSize)
    {
        double clamped = Math.Clamp(fontSize, MinimumFontSize, MaximumFontSize);
        if (Math.Abs(FontSize - clamped) < 0.01)
        {
            return false;
        }

        FontSize = clamped;
        LineHeight = Math.Ceiling(clamped * 1.6);
        return true;
    }

    /// <summary>
    /// يعيد مقاس الخط الافتراضي.
    /// </summary>
    public bool ResetFontSize()
    {
        return SetFontSize(DefaultFontSize);
    }

    /// <summary>
    /// ينشئ كائن نص جاهز للرسم والقياس.
    /// Creates a formatted text object for drawing and measurement.
    /// </summary>
    /// <param name="text">النص المراد رسمه.</param>
    /// <param name="brush">فرشاة الرسم.</param>
    /// <returns>كائن النص المنسق.</returns>
    public FormattedText CreateFormattedText(string text, IBrush brush, FlowDirection flowDirection = FlowDirection.LeftToRight)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            flowDirection,
            Typeface,
            FontSize,
            brush);

        foreach ((int start, int length) in ArabicTextRuns.Find(text))
        {
            formatted.SetFontFamily(ArabicFontFamily, start, length);
        }

        return formatted;
    }

    /// <summary>
    /// ينشئ تخطيط نص يدعم اختبارات موضع المؤشر بدقة مع BiDi.
    /// Creates a text layout that supports accurate BiDi-aware caret hit testing.
    /// </summary>
    /// <param name="text">النص المنطقي المراد رسمه.</param>
    /// <param name="brush">فرشاة الرسم.</param>
    /// <param name="flowDirection">اتجاه الفقرة الأساسي.</param>
    /// <returns>تخطيط النص.</returns>
    public TextLayout CreateTextLayout(string text, IBrush brush, FlowDirection flowDirection)
    {
        return new TextLayout(
            text: text,
            typeface: Typeface,
            fontSize: FontSize,
            foreground: brush,
            textAlignment: TextAlignment.Left,
            textWrapping: TextWrapping.NoWrap,
            textTrimming: TextTrimming.None,
            textDecorations: null,
            flowDirection: flowDirection,
            maxWidth: double.PositiveInfinity,
            maxHeight: double.PositiveInfinity,
            lineHeight: LineHeight,
            letterSpacing: 0,
            maxLines: 1,
            textStyleOverrides: CreateArabicOverrides(text, brush));
    }

    /// <summary>
    /// يعيد الإزاحة الرأسية التي تضع خط أساس السطر على خط أساس موحد لكل الأسطر.
    /// للخط العربي صعود أعلى من الخط اللاتيني، فبدون هذه الإزاحة يعلو السطر اللاتيني عن جاره العربي.
    /// </summary>
    /// <param name="baseline">خط أساس النص المنسق المراد رسمه.</param>
    public double GetBaselineOffset(double baseline)
    {
        if (_referenceBaselineFontSize != FontSize)
        {
            _referenceBaseline = CreateFormattedText("Aأ", Brushes.Transparent).Baseline;
            _referenceBaselineFontSize = FontSize;
        }

        return Math.Max(0, _referenceBaseline - baseline);
    }

    private List<ValueSpan<TextRunProperties>>? CreateArabicOverrides(string text, IBrush brush)
    {
        IReadOnlyList<(int Start, int Length)> runs = ArabicTextRuns.Find(text);
        if (runs.Count == 0)
        {
            return null;
        }

        var properties = new GenericTextRunProperties(
            new Typeface(ArabicFontFamily),
            FontSize,
            foregroundBrush: brush);
        return [.. runs.Select(run => new ValueSpan<TextRunProperties>(run.Start, run.Length, properties))];
    }

    /// <summary>
    /// يحدد لون السطر حسب نوعه المنطقي.
    /// Resolves line brush by line kind.
    /// </summary>
    /// <param name="kind">نوع السطر.</param>
    /// <returns>الفرشاة المناسبة.</returns>
    public IBrush ResolveBrush(TerminalLineKind kind)
    {
        return kind switch
        {
            TerminalLineKind.System => new SolidColorBrush(Theme.SystemForeground),
            TerminalLineKind.Input => new SolidColorBrush(Theme.InputForeground),
            TerminalLineKind.Warning => new SolidColorBrush(Theme.WarningForeground),
            TerminalLineKind.Error => new SolidColorBrush(Theme.ErrorForeground),
            TerminalLineKind.Debug => new SolidColorBrush(Theme.DebugForeground),
            _ => new SolidColorBrush(Theme.Foreground)
        };
    }

    /// <summary>
    /// يحل لون النص النهائي لنطاق ANSI مع قواعد العكس/الإخفاء/التعتيم.
    /// Resolves final foreground brush for an ANSI span with inverse/hidden/dim behavior.
    /// </summary>
    /// <param name="kind">نوع السطر.</param>
    /// <param name="style">حالة النمط.</param>
    /// <returns>فرشاة النص النهائية.</returns>
    public IBrush ResolveAnsiForegroundBrush(TerminalLineKind kind, AnsiStyleState style)
    {
        IBrush baseForeground = ResolveBrush(kind);
        IBrush baseBackground = BackgroundBrush;

        IBrush fg = ResolveAnsiColor(style.Foreground, baseForeground);
        IBrush bg = ResolveAnsiColor(style.Background, baseBackground);

        if (style.Inverse)
        {
            (fg, bg) = (bg, fg);
        }

        if (style.Hidden)
        {
            fg = bg;
        }

        if (style.Dim)
        {
            fg = DimBrush(fg);
        }

        return fg;
    }

    /// <summary>
    /// يحل خلفية النطاق وفق ANSI، أو يعيد null إذا لا توجد خلفية مطلوبة.
    /// Resolves ANSI background brush, or null when no explicit background is needed.
    /// </summary>
    /// <param name="kind">نوع السطر.</param>
    /// <param name="style">حالة النمط.</param>
    /// <returns>فرشاة الخلفية أو null.</returns>
    public IBrush? ResolveAnsiBackgroundBrush(TerminalLineKind kind, AnsiStyleState style)
    {
        bool needsBackground = style.Background.Mode != AnsiColorMode.Default || style.Inverse || style.Hidden;
        if (!needsBackground)
        {
            return null;
        }

        IBrush baseForeground = ResolveBrush(kind);
        IBrush baseBackground = BackgroundBrush;

        IBrush fg = ResolveAnsiColor(style.Foreground, baseForeground);
        IBrush bg = ResolveAnsiColor(style.Background, baseBackground);

        if (style.Inverse)
        {
            (fg, bg) = (bg, fg);
        }

        return bg;
    }

    /// <summary>
    /// يحل وزن الخط لنمط ANSI.
    /// Resolves font weight from ANSI style.
    /// </summary>
    /// <param name="style">حالة النمط.</param>
    /// <returns>وزن الخط.</returns>
    public FontWeight ResolveAnsiFontWeight(AnsiStyleState style)
    {
        return style.Bold ? FontWeight.Bold : FontWeight.Normal;
    }

    /// <summary>
    /// يحل نمط الخط (مائل/عادي) لنمط ANSI.
    /// Resolves font style from ANSI style.
    /// </summary>
    /// <param name="style">حالة النمط.</param>
    /// <returns>نمط الخط.</returns>
    public FontStyle ResolveAnsiFontStyle(AnsiStyleState style)
    {
        return style.Italic ? FontStyle.Italic : FontStyle.Normal;
    }

    /// <summary>
    /// يحل زخارف النص (تسطير/شطب) لنمط ANSI.
    /// Resolves text decorations from ANSI style.
    /// </summary>
    /// <param name="style">حالة النمط.</param>
    /// <returns>زخارف النص أو null.</returns>
    public TextDecorationCollection? ResolveAnsiTextDecorations(AnsiStyleState style)
    {
        if (!style.Underline && !style.Strikethrough)
        {
            return null;
        }

        if (style.Underline && style.Strikethrough)
        {
            var all = new TextDecorationCollection();
            all.Add(TextDecorations.Underline[0]);
            all.Add(TextDecorations.Strikethrough[0]);
            return all;
        }

        return style.Underline ? TextDecorations.Underline : TextDecorations.Strikethrough;
    }

    private IBrush ResolveAnsiColor(AnsiColorSpec spec, IBrush fallback)
    {
        return spec.Mode switch
        {
            AnsiColorMode.Default => fallback,
            AnsiColorMode.Indexed16 => new SolidColorBrush(Palette.ResolveIndexed(spec.Index)),
            AnsiColorMode.Indexed256 => new SolidColorBrush(Palette.ResolveIndexed(spec.Index)),
            AnsiColorMode.TrueColor => new SolidColorBrush(Color.FromRgb(spec.Red, spec.Green, spec.Blue)),
            _ => fallback
        };
    }

    private static IBrush DimBrush(IBrush brush)
    {
        if (brush is not ISolidColorBrush solid)
        {
            return brush;
        }

        byte alpha = (byte)Math.Clamp((int)(solid.Color.A * 0.65), 0, 255);
        Color c = solid.Color;
        return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
    }
}
