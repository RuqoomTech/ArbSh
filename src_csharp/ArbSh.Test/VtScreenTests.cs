using ArbSh.Core.Processes;
using Xunit;

namespace ArbSh.Test;

public class VtScreenTests
{
    private static (VtScreen Screen, List<string> Committed) Feed(string text, int columns = 20, int rows = 5)
    {
        var screen = new VtScreen(columns, rows);
        var committed = new List<string>();
        screen.Write(text, committed);
        return (screen, committed);
    }

    [Fact]
    public void LineFeed_CommitsTheFinishedLineAndKeepsThePromptLive()
    {
        var (screen, committed) = Feed("سطر أول\r\nاسمك؟ ");

        Assert.Equal(["سطر أول"], committed);
        Assert.Equal(["اسمك؟"], screen.GetLiveLines());
    }

    [Fact]
    public void CarriageReturn_OverwritesTheLiveLineLikeAProgressBar()
    {
        var (screen, committed) = Feed("10%\r50%\r100%\r\n");

        Assert.Equal(["100%"], committed);
        Assert.Empty(screen.GetLiveLines());
    }

    [Fact]
    public void EraseLineAndBackspace_EditTheCurrentLine()
    {
        var (_, committed) = Feed("abcdef\b\b\u001b[K!\r\n");

        Assert.Equal(["abcd!"], committed);
    }

    [Fact]
    public void Sequences_SplitAcrossWrites_AreStillApplied()
    {
        var screen = new VtScreen(20, 5);
        var committed = new List<string>();
        screen.Write("abc\u001b[", committed);
        screen.Write("2D", committed);
        screen.Write("Z\r\n", committed);

        Assert.Equal(["aZc"], committed);
    }

    [Fact]
    public void AutoWrap_KeepsOneLogicalLine()
    {
        var (_, committed) = Feed("0123456789abc\r\n", columns: 10);

        Assert.Equal(["0123456789abc"], committed);
    }

    [Fact]
    public void ScrollingPastTheBottom_CommitsEachLineOnce()
    {
        var (screen, committed) = Feed("1\r\n2\r\n3\r\n4\r\n5\r\n6\r\n7", rows: 3);

        Assert.Equal(["1", "2", "3", "4", "5", "6"], committed);
        Assert.Equal(["7"], screen.GetLiveLines());
    }

    [Fact]
    public void CursorPosition_DrawsAtTheRequestedCell()
    {
        var (screen, _) = Feed("\u001b[2J\u001b[2;3Hx\u001b[1;1Hy");

        Assert.Equal(["y", "  x"], screen.GetLiveLines());
    }

    [Fact]
    public void Colors_AreKeptAsSgrAndStripStylesRemovesThem()
    {
        var (_, committed) = Feed("\u001b[1;31mأحمر\u001b[0m عادي\r\n");

        string line = Assert.Single(committed);
        Assert.Equal("\u001b[0;1;31mأحمر\u001b[0m عادي", line);
        Assert.Equal("أحمر عادي", VtScreen.StripStyles(line));
    }

    [Fact]
    public void ExtendedColors_SurviveTheRoundTrip()
    {
        var (_, committed) = Feed("\u001b[38;5;208mo\u001b[48;2;1;2;3mb\u001b[m\r\n");

        Assert.Equal("\u001b[0;38;5;208mo\u001b[0;38;5;208;48;2;1;2;3mb\u001b[0m", Assert.Single(committed));
    }

    [Fact]
    public void ArabicMarks_AttachToTheirLetter()
    {
        var (screen, committed) = Feed("مَرْحَبًا", columns: 5);

        // خمسة أحرف أساسية تملأ خمس خلايا بلا لف رغم علامات التشكيل الأربع.
        Assert.Empty(committed);
        Assert.Equal(["مَرْحَبًا"], screen.GetLiveLines());
        Assert.Equal(0, screen.CursorRow);
    }

    [Fact]
    public void AlternateScreen_IsLiveWhileActiveAndNeverCommitted()
    {
        var screen = new VtScreen(20, 3);
        var committed = new List<string>();
        screen.Write("قبل\r\n\u001b[?1049h\u001b[Hملء الشاشة", committed);

        Assert.True(screen.IsAlternateScreen);
        Assert.Equal(["ملء الشاشة", string.Empty, string.Empty], screen.GetLiveLines());

        screen.Write("\nسطر\n\u001b[?1049lبعد\r\n", committed);
        screen.Finish(committed);

        Assert.False(screen.IsAlternateScreen);
        Assert.Equal(["قبل", "بعد"], committed);
    }

    [Fact]
    public void ClearScreen_MakesTheWholeScreenLiveAgain()
    {
        var (screen, committed) = Feed("قديم\r\n\u001b[H\u001b[2Jجديد");

        Assert.Equal(["قديم"], committed);
        Assert.Equal(["جديد"], screen.GetLiveLines());
    }

    [Fact]
    public void ScrollRegion_ScrollsOnlyInsideAndKeepsTheHeader()
    {
        var (screen, committed) = Feed("\u001b[?1049h\u001b[2;3r\u001b[1;1Hرأس\u001b[2;1Ha\r\nb\r\nc", rows: 4);

        Assert.Empty(committed);
        Assert.Equal(["رأس", "b", "c", string.Empty], screen.GetLiveLines());
    }

    [Fact]
    public void ScrollRegion_OnThePrimaryScreen_CommitsLinesAndKeepsNewOutputLive()
    {
        var (screen, committed) = Feed("\u001b[2J\u001b[2;3r\u001b[1;1Hرأس\u001b[2;1Ha\r\nb\r\nc", rows: 4);

        Assert.Equal(["رأس", "a", "b"], committed);
        Assert.Equal(["c"], screen.GetLiveLines());
    }

    [Fact]
    public void Finish_CommitsWhatIsLeftWithoutTrailingBlankLines()
    {
        var (screen, committed) = Feed("أ\r\nب");
        screen.Finish(committed);

        Assert.Equal(["أ", "ب"], committed);
        Assert.Empty(screen.GetLiveLines());
    }

    [Fact]
    public void CursorQueries_AreAnswered()
    {
        var screen = new VtScreen(20, 5);
        var replies = new List<string>();
        screen.Write("ab\u001b[6n\u001b[c", [], replies.Add);

        Assert.Equal(["\u001b[1;3R", "\u001b[?1;2c"], replies);
    }

    [Fact]
    public void OperatingSystemCommands_AreIgnored()
    {
        var (_, committed) = Feed("\u001b]0;عنوان\u0007نص\u001b]2;x\u001b\\\r\n");

        Assert.Equal(["نص"], committed);
    }

    [Fact]
    public void Resize_KeepsTheCursorVisibleAndCommitsRowsPushedOff()
    {
        var (screen, committed) = Feed("1\r\n2\r\n3\r\n4\r\n5", rows: 5);
        Assert.Equal(["1", "2", "3", "4"], committed);

        screen.Resize(20, 2, committed);
        screen.Write("6", committed);

        Assert.Equal(2, screen.Rows);
        Assert.Equal(1, screen.CursorRow);
        Assert.Equal(["56"], screen.GetLiveLines());
        Assert.Equal(["1", "2", "3", "4"], committed);
    }

    [Fact]
    public void Resize_ShrinkingAboveLiveContent_CommitsIt()
    {
        var (screen, committed) = Feed("\u001b[2J\u001b[1;1Hأ\u001b[3;1Hب", rows: 4);
        Assert.Empty(committed);

        screen.Resize(20, 1, committed);

        Assert.Equal(["أ", string.Empty], committed);
        Assert.Equal(["ب"], screen.GetLiveLines());
    }
}
