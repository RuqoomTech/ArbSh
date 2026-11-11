using System.Text;

namespace ArbSh.Core.Processes;

/// <summary>
/// يحوّل خرج الطرفية الزائفة إلى نص سطري: يحذف تسلسلات ANSI/VT ومحارف التحكم،
/// ويحفظ حالته بين القطع لأن التسلسل قد ينقسم بين قراءتين أو يقع وسط النص.
/// </summary>
internal sealed class TerminalOutputFilter
{
    private const char Escape = '\u001b';
    private const char Bell = '\u0007';
    private const int MaxCursorForward = 512;

    private State _state;
    private int _parameter;
    private bool _parameterClosed;

    private enum State
    {
        Text,
        Escape,
        EscapeIntermediate,
        ControlSequence,
        ControlString,
        ControlStringEscape
    }

    public string Filter(ReadOnlySpan<char> text)
    {
        var result = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            switch (_state)
            {
                case State.Text:
                    if (character == Escape)
                    {
                        _state = State.Escape;
                    }
                    else if ((character >= ' ' && character != '\u007f') || character is '\r' or '\n' or '\t')
                    {
                        result.Append(character);
                    }

                    break;

                case State.Escape:
                    if (character == '[')
                    {
                        _state = State.ControlSequence;
                        _parameter = 0;
                        _parameterClosed = false;
                    }
                    else if (character is ']' or 'P' or '_' or '^' or 'X')
                    {
                        _state = State.ControlString;
                    }
                    else
                    {
                        _state = character is >= ' ' and <= '/' ? State.EscapeIntermediate : State.Text;
                    }

                    break;

                case State.EscapeIntermediate:
                    if (character is >= '0' and <= '~')
                    {
                        _state = State.Text;
                    }

                    break;

                case State.ControlSequence:
                    if (character is >= '0' and <= '9' && !_parameterClosed)
                    {
                        _parameter = Math.Min((_parameter * 10) + (character - '0'), MaxCursorForward);
                    }
                    else if (character is >= '@' and <= '~')
                    {
                        // تختصر الطرفية الزائفة الفراغات المتتالية إلى «حرّك المؤشر يمينًا»؛ حذفها يلصق الكلمات.
                        if (character == 'C')
                        {
                            result.Append(' ', Math.Max(1, _parameter));
                        }

                        _state = State.Text;
                    }
                    else
                    {
                        _parameterClosed = true;
                    }

                    break;

                case State.ControlString:
                    if (character == Bell)
                    {
                        _state = State.Text;
                    }
                    else if (character == Escape)
                    {
                        _state = State.ControlStringEscape;
                    }

                    break;

                case State.ControlStringEscape:
                    _state = character == '\\' ? State.Text : State.ControlString;
                    break;
            }
        }

        return result.ToString();
    }
}
