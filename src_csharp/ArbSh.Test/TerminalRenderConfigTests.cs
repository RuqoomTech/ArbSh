using Avalonia.Media;
using ArbSh.Terminal.Rendering;

namespace ArbSh.Test;

public sealed class TerminalRenderConfigTests
{
    [Fact]
    public void Defaults_UseReadableDesktopTypography()
    {
        var config = new TerminalRenderConfig();

        Assert.Equal(16, config.FontSize);
        Assert.Equal(26, config.LineHeight);
        Assert.True(config.LineHeight >= config.FontSize * 1.5);
    }

    [Fact]
    public void SetFontSize_ClampsZoomAndKeepsComfortableLineSpacing()
    {
        var config = new TerminalRenderConfig();

        Assert.True(config.SetFontSize(100));
        Assert.Equal(TerminalRenderConfig.MaximumFontSize, config.FontSize);
        Assert.Equal(Math.Ceiling(config.FontSize * 1.6), config.LineHeight);

        Assert.True(config.SetFontSize(1));
        Assert.Equal(TerminalRenderConfig.MinimumFontSize, config.FontSize);
        Assert.Equal(Math.Ceiling(config.FontSize * 1.6), config.LineHeight);

        Assert.True(config.ResetFontSize());
        Assert.Equal(TerminalRenderConfig.DefaultFontSize, config.FontSize);
        Assert.False(config.ResetFontSize());
    }

    [Fact]
    public void DefaultThemeTextColors_MeetNormalTextContrast()
    {
        TerminalTheme theme = TerminalTheme.CreateCampbell();
        Color[] textColors =
        [
            theme.Foreground,
            theme.PromptForeground,
            theme.SystemForeground,
            theme.InputForeground,
            theme.WarningForeground,
            theme.ErrorForeground,
            theme.DebugForeground
        ];

        foreach (Color textColor in textColors)
        {
            Assert.True(
                ContrastRatio(textColor, theme.Background) >= 4.5,
                $"Insufficient text contrast for {textColor} on {theme.Background}.");
        }
    }

    private static double ContrastRatio(Color first, Color second)
    {
        double firstLuminance = RelativeLuminance(first);
        double secondLuminance = RelativeLuminance(second);
        double lighter = Math.Max(firstLuminance, secondLuminance);
        double darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Linearize(byte component)
        {
            double channel = component / 255.0;
            return channel <= 0.04045
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linearize(color.R))
            + (0.7152 * Linearize(color.G))
            + (0.0722 * Linearize(color.B));
    }
}
