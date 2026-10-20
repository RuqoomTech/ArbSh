using Avalonia.Media;
using ArbSh.Terminal.Rendering;

namespace ArbSh.Test;

public sealed class AnsiPaletteTests
{
    [Fact]
    public void ResolveIndexed_MapsXtermCubeColor()
    {
        AnsiPalette palette = AnsiPalette.CreateCampbell();

        Color color = palette.ResolveIndexed(196);

        Assert.Equal((byte)255, color.R);
        Assert.Equal((byte)0, color.G);
        Assert.Equal((byte)0, color.B);
    }
}
