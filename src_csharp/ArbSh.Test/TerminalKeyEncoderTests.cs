using ArbSh.Terminal.Input;
using Avalonia.Input;
using Xunit;

namespace ArbSh.Test;

public class TerminalKeyEncoderTests
{
    [Theory]
    [InlineData(Key.Up, false, "\u001b[A")]
    [InlineData(Key.Up, true, "\u001bOA")]
    [InlineData(Key.Left, false, "\u001b[D")]
    [InlineData(Key.Home, true, "\u001bOH")]
    [InlineData(Key.End, false, "\u001b[F")]
    public void CursorKeys_FollowTheApplicationCursorMode(Key key, bool application, string expected)
    {
        Assert.Equal(expected, TerminalKeyEncoder.Encode(key, KeyModifiers.None, application));
    }

    [Theory]
    [InlineData(Key.Right, KeyModifiers.Control, "\u001b[1;5C")]
    [InlineData(Key.Up, KeyModifiers.Shift, "\u001b[1;2A")]
    [InlineData(Key.Delete, KeyModifiers.Alt, "\u001b[3;3~")]
    [InlineData(Key.F1, KeyModifiers.Control, "\u001b[1;5P")]
    public void Modifiers_UseTheXtermParameter(Key key, KeyModifiers modifiers, string expected)
    {
        // المعدّل يفرض الصيغة CSI حتى في وضع التطبيق.
        Assert.Equal(expected, TerminalKeyEncoder.Encode(key, modifiers, applicationCursorKeys: true));
    }

    [Theory]
    [InlineData(Key.Enter, "\r")]
    [InlineData(Key.Back, "\u007f")]
    [InlineData(Key.Tab, "\t")]
    [InlineData(Key.Escape, "\u001b")]
    [InlineData(Key.Insert, "\u001b[2~")]
    [InlineData(Key.PageUp, "\u001b[5~")]
    [InlineData(Key.PageDown, "\u001b[6~")]
    [InlineData(Key.F1, "\u001bOP")]
    [InlineData(Key.F4, "\u001bOS")]
    [InlineData(Key.F5, "\u001b[15~")]
    [InlineData(Key.F12, "\u001b[24~")]
    public void EditingAndFunctionKeys_HaveTheirXtermSequences(Key key, string expected)
    {
        Assert.Equal(expected, TerminalKeyEncoder.Encode(key, KeyModifiers.None, applicationCursorKeys: false));
    }

    [Theory]
    [InlineData(Key.C, KeyModifiers.Control, "\u0003")]
    [InlineData(Key.D, KeyModifiers.Control, "\u0004")]
    [InlineData(Key.Z, KeyModifiers.Control, "\u001a")]
    [InlineData(Key.Space, KeyModifiers.Control, "\0")]
    [InlineData(Key.OemOpenBrackets, KeyModifiers.Control, "\u001b")]
    [InlineData(Key.X, KeyModifiers.Alt, "\u001bx")]
    [InlineData(Key.X, KeyModifiers.Alt | KeyModifiers.Shift, "\u001bX")]
    [InlineData(Key.Tab, KeyModifiers.Shift, "\u001b[Z")]
    [InlineData(Key.Back, KeyModifiers.Control, "\b")]
    public void ControlAndAltCombinations_SendControlCharactersOrEscapePrefix(Key key, KeyModifiers modifiers, string expected)
    {
        Assert.Equal(expected, TerminalKeyEncoder.Encode(key, modifiers, applicationCursorKeys: false));
    }

    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.D1)]
    [InlineData(Key.LeftShift)]
    public void TextAndModifierKeys_AreLeftToTextInput(Key key)
    {
        Assert.Null(TerminalKeyEncoder.Encode(key, KeyModifiers.None, applicationCursorKeys: false));
    }

    [Fact]
    public void Paste_NormalizesLineEndingsAndBracketsWhenRequested()
    {
        Assert.Equal("أ\rب\rج", TerminalKeyEncoder.EncodePaste("أ\r\nب\nج", bracketedPaste: false));
        Assert.Equal(
            "\u001b[200~سطر\rآخر\u001b[201~",
            TerminalKeyEncoder.EncodePaste("سطر\n\u001b[201~آخر", bracketedPaste: true));
    }
}
