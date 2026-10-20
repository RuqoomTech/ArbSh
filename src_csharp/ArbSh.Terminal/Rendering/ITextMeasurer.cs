using Avalonia.Media;

namespace ArbSh.Terminal.Rendering;

/// <summary>
/// واجهة لقياس عرض النص بشكل قابل للاستبدال والاختبار.
/// Abstraction for text measurement to enable deterministic tests.
/// </summary>
public interface ITextMeasurer
{
    /// <summary>
    /// يقيس عرض النص المرئي وفق إعدادات الرسم.
    /// Measures visual text width for the supplied render configuration.
    /// </summary>
    /// <param name="visualText">النص المرئي.</param>
    /// <param name="config">إعدادات الرسم.</param>
    /// <returns>العرض المقاس.</returns>
    double MeasureWidth(string visualText, TerminalRenderConfig config);
}

/// <summary>
/// مقياس نص افتراضي يعتمد على محرك النص في Avalonia.
/// Default Avalonia-based text measurer.
/// </summary>
public sealed class AvaloniaTextMeasurer : ITextMeasurer
{
    /// <inheritdoc />
    public double MeasureWidth(string visualText, TerminalRenderConfig config)
    {
        if (string.IsNullOrEmpty(visualText))
        {
            return 0;
        }

        // تُحاذى الأسطر كلها إلى اليمين، فيُقاس السطر باتجاه رسمه نفسه، ويُحسب منه ما يظهر قبل الحافة اليمنى:
        // السطر العربي يُرسم من اليمين فتقع مسافته المنطقية الأخيرة (كمسافة الموجه) على يساره وتدخل في عرضه،
        // واللاتيني تقع مسافته الأخيرة خلف الحافة.
        bool rtl = TerminalSurface.IsTextRtl(visualText);
        FormattedText formatted = config.CreateFormattedText(
            visualText,
            Brushes.Transparent,
            rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight);
        return rtl ? formatted.WidthIncludingTrailingWhitespace : formatted.Width;
    }
}
