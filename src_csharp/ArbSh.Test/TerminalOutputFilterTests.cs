using ArbSh.Core.Processes;

namespace ArbSh.Test;

public sealed class TerminalOutputFilterTests
{
    private static readonly string Esc = ((char)27).ToString();
    private static readonly string Bell = ((char)7).ToString();

    [Fact]
    public void Filter_StripsSequencesInterleavedWithArabicText()
    {
        // خرج ConPTY الفعلي لبرنامج باء: تسلسل عنوان النافذة يقع وسط الكلمة.
        string captured =
            $"{Esc}[?25l{Esc}[2J{Esc}[m{Esc}[Hأ{Esc}]0;C:\\مسار\\ask.exe{Bell}{Esc}[?25hدخل رقماً\r\n";

        Assert.Equal("أدخل رقماً\r\n", new TerminalOutputFilter().Filter(captured));
    }

    [Fact]
    public void Filter_KeepsStateAcrossChunkBoundaries()
    {
        var filter = new TerminalOutputFilter();

        string result = filter.Filter($"ab{Esc}")
            + filter.Filter("[3")
            + filter.Filter($"1mcd{Esc}]0;عنو")
            + filter.Filter($"ان{Esc}")
            + filter.Filter("\\ef");

        Assert.Equal("abcdef", result);
    }

    [Fact]
    public void Filter_ExpandsCursorForwardToSpaces()
    {
        var filter = new TerminalOutputFilter();

        Assert.Equal("a   b c", filter.Filter($"a{Esc}[3Cb{Esc}[Cc"));
    }

    [Fact]
    public void Filter_DropsControlCharactersButKeepsLineStructure()
    {
        string text = $"a{(char)8}{Bell}b\tc\r\nd{Esc}(Be{(char)127}";

        Assert.Equal("ab\tc\r\nde", new TerminalOutputFilter().Filter(text));
    }
}
