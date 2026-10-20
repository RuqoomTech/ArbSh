using ArbSh.Terminal.Rendering;

namespace ArbSh.Test;

public sealed class ArabicTextRunsTests
{
    [Fact]
    public void Find_ReturnsNothingForLatinText()
    {
        Assert.Empty(ArabicTextRuns.Find("Get-ChildItem C:\\"));
        Assert.Empty(ArabicTextRuns.Find(string.Empty));
    }

    [Fact]
    public void Find_KeepsSpacesAndPunctuationBetweenArabicWordsInOneRun()
    {
        const string text = "اكتب «الأوامر» للقائمة أو «مساعدة» للدليل.";

        Assert.Equal([(0, text.Length)], ArabicTextRuns.Find(text));
    }

    [Fact]
    public void Find_AttachesLeadingNeutralsToFollowingArabic()
    {
        const string text = "— مرحبًا";

        Assert.Equal([(0, text.Length)], ArabicTextRuns.Find(text));
    }

    [Fact]
    public void Find_LeavesLatinPathAndItsNeighboursToTheMonospaceFont()
    {
        const string text = "أربش \u2066C:\\Users\u2069> المسار";
        int command = text.IndexOf("المسار", StringComparison.Ordinal);

        Assert.Equal([(0, 4), (command, "المسار".Length)], ArabicTextRuns.Find(text));
    }
}
