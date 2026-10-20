using ArbSh.Terminal.Input;
using ArbSh.Terminal.Models;

namespace ArbSh.Test;

public sealed class OutputSelectionBufferTests
{
    [Fact]
    public void GetSelectedText_ReturnsLinesInLogicalOrder()
    {
        var buffer = new OutputSelectionBuffer();
        var lines = new List<TerminalLine>
        {
            new("line-0", TerminalLineKind.Output, DateTimeOffset.UtcNow),
            new("line-1", TerminalLineKind.Output, DateTimeOffset.UtcNow),
            new("line-2", TerminalLineKind.Output, DateTimeOffset.UtcNow),
            new("line-3", TerminalLineKind.Output, DateTimeOffset.UtcNow)
        };

        buffer.BeginOrExtend(3, extendSelection: false);
        buffer.UpdateActive(1);

        string selected = buffer.GetSelectedText(lines);
        string expected = string.Join(Environment.NewLine, "line-1", "line-2", "line-3");

        Assert.Equal(expected, selected);
    }

    [Fact]
    public void GetSelectedText_ClampsRangeWhenHistoryShrinks()
    {
        var buffer = new OutputSelectionBuffer();
        var lines = new List<TerminalLine>
        {
            new("line-0", TerminalLineKind.Output, DateTimeOffset.UtcNow),
            new("line-1", TerminalLineKind.Output, DateTimeOffset.UtcNow)
        };

        buffer.BeginOrExtend(1, extendSelection: false);
        buffer.UpdateActive(100);

        string selected = buffer.GetSelectedText(lines);

        Assert.Equal("line-1", selected);
    }

    [Fact]
    public void GetSelectedText_RemovesDirectionalMarksFromPromptEcho()
    {
        var buffer = new OutputSelectionBuffer();
        var lines = new List<TerminalLine>
        {
            new("أربش \u2066C:\\Users\u2069>\u200F المسار", TerminalLineKind.Input, DateTimeOffset.UtcNow)
        };

        buffer.BeginOrExtend(0, extendSelection: false);

        Assert.Equal("أربش C:\\Users> المسار", buffer.GetSelectedText(lines));
    }
}
