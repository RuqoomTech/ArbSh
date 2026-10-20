using Avalonia.Media;

namespace ArbSh.Terminal.Rendering;

/// <summary>
/// لوحة ألوان ANSI (16 لونًا أساسيًا + تحويل 256 لونًا).
/// ANSI palette (base 16 colors + 256-color mapping).
/// </summary>
public sealed class AnsiPalette
{
    private readonly Color[] _base16;

    /// <summary>
    /// ينشئ لوحة ANSI من 16 لونًا أساسيًا.
    /// Creates an ANSI palette from 16 base colors.
    /// </summary>
    /// <param name="base16">ألوان ANSI الأساسية.</param>
    public AnsiPalette(IReadOnlyList<Color> base16)
    {
        ArgumentNullException.ThrowIfNull(base16);
        if (base16.Count != 16)
        {
            throw new ArgumentException("ANSI base palette must contain exactly 16 colors.", nameof(base16));
        }

        _base16 = [.. base16];
    }

    /// <summary>
    /// يحول فهرس ANSI (16/256) إلى لون فعلي.
    /// Resolves ANSI indexed color (16/256) to concrete color.
    /// </summary>
    /// <param name="index">فهرس اللون.</param>
    /// <returns>اللون المقابل.</returns>
    public Color ResolveIndexed(int index)
    {
        int clamped = Math.Clamp(index, 0, 255);
        if (clamped < 16)
        {
            return _base16[clamped];
        }

        if (clamped <= 231)
        {
            int cube = clamped - 16;
            int r = cube / 36;
            int g = (cube / 6) % 6;
            int b = cube % 6;

            return Color.FromRgb(CubeToByte(r), CubeToByte(g), CubeToByte(b));
        }

        int gray = clamped - 232;
        byte level = (byte)(8 + (gray * 10));
        return Color.FromRgb(level, level, level);
    }

    /// <summary>
    /// لوحة Campbell الافتراضية في Windows Terminal.
    /// Windows Terminal's default Campbell ANSI palette.
    /// </summary>
    /// <returns>لوحة ANSI مهيأة.</returns>
    public static AnsiPalette CreateCampbell()
    {
        Color[] colors =
        [
            Color.Parse("#0C0C0C"), // black
            Color.Parse("#C50F1F"), // red
            Color.Parse("#13A10E"), // green
            Color.Parse("#C19C00"), // yellow
            Color.Parse("#0037DA"), // blue
            Color.Parse("#881798"), // magenta
            Color.Parse("#3A96DD"), // cyan
            Color.Parse("#CCCCCC"), // white
            Color.Parse("#767676"), // bright black
            Color.Parse("#E74856"), // bright red
            Color.Parse("#16C60C"), // bright green
            Color.Parse("#F9F1A5"), // bright yellow
            Color.Parse("#3B78FF"), // bright blue
            Color.Parse("#B4009E"), // bright magenta
            Color.Parse("#61D6D6"), // bright cyan
            Color.Parse("#F2F2F2")  // bright white
        ];

        return new AnsiPalette(colors);
    }

    private static byte CubeToByte(int value)
    {
        return value switch
        {
            <= 0 => 0,
            1 => 95,
            2 => 135,
            3 => 175,
            4 => 215,
            _ => 255
        };
    }
}
