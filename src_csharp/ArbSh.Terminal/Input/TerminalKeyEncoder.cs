using Avalonia.Input;

namespace ArbSh.Terminal.Input;

/// <summary>
/// يحول مفتاحًا مضغوطًا إلى البايتات التي ترسلها طرفية xterm إلى البرنامج.
/// النص المطبوع لا يمر من هنا؛ يصل عبر حدث إدخال النص كما هو.
/// </summary>
public static class TerminalKeyEncoder
{
    private const string Escape = "\u001b";

    /// <summary>
    /// تسلسل المفتاح، أو null إذا كان مفتاحًا يولد نصًا أو لا يعني شيئًا للبرنامج.
    /// </summary>
    /// <param name="applicationCursorKeys">طلب البرنامج صيغة التطبيق لمفاتيح المؤشر (DECCKM).</param>
    public static string? Encode(Key key, KeyModifiers modifiers, bool applicationCursorKeys)
    {
        bool shift = modifiers.HasFlag(KeyModifiers.Shift);
        bool alt = modifiers.HasFlag(KeyModifiers.Alt);
        bool control = modifiers.HasFlag(KeyModifiers.Control);
        // معامل المعدّل في xterm: 1 + Shift + 2×Alt + 4×Ctrl.
        int modifier = 1 + (shift ? 1 : 0) + (alt ? 2 : 0) + (control ? 4 : 0);

        string? cursor = key switch
        {
            Key.Up => "A",
            Key.Down => "B",
            Key.Right => "C",
            Key.Left => "D",
            Key.Home => "H",
            Key.End => "F",
            _ => null
        };
        if (cursor is not null)
        {
            if (modifier > 1)
            {
                return $"{Escape}[1;{modifier}{cursor}";
            }

            return applicationCursorKeys ? $"{Escape}O{cursor}" : $"{Escape}[{cursor}";
        }

        int? tilde = key switch
        {
            Key.Insert => 2,
            Key.Delete => 3,
            Key.PageUp => 5,
            Key.PageDown => 6,
            Key.F5 => 15,
            Key.F6 => 17,
            Key.F7 => 18,
            Key.F8 => 19,
            Key.F9 => 20,
            Key.F10 => 21,
            Key.F11 => 23,
            Key.F12 => 24,
            _ => null
        };
        if (tilde is not null)
        {
            return modifier > 1 ? $"{Escape}[{tilde};{modifier}~" : $"{Escape}[{tilde}~";
        }

        string? function = key switch
        {
            Key.F1 => "P",
            Key.F2 => "Q",
            Key.F3 => "R",
            Key.F4 => "S",
            _ => null
        };
        if (function is not null)
        {
            return modifier > 1 ? $"{Escape}[1;{modifier}{function}" : $"{Escape}O{function}";
        }

        string? plain = key switch
        {
            Key.Enter => "\r",
            Key.Back => alt ? Escape + "\u007f" : control ? "\b" : "\u007f",
            Key.Tab => shift ? $"{Escape}[Z" : "\t",
            Key.Escape => Escape,
            _ => null
        };
        if (plain is not null)
        {
            return plain;
        }

        if (control)
        {
            char? controlCharacter = key switch
            {
                >= Key.A and <= Key.Z => (char)(key - Key.A + 1),
                Key.Space or Key.D2 => '\0',
                Key.OemOpenBrackets or Key.D3 => '\u001b',
                Key.OemPipe or Key.OemBackslash or Key.D4 => '\u001c',
                Key.OemCloseBrackets or Key.D5 => '\u001d',
                Key.D6 => '\u001e',
                Key.OemMinus or Key.D7 => '\u001f',
                _ => null
            };
            if (controlCharacter is not null)
            {
                return alt ? Escape + controlCharacter : controlCharacter.ToString();
            }
        }

        // Alt مع حرف لاتيني يرسل ESC قبله؛ لا يولد Avalonia حدث نص لهذه التركيبة.
        if (alt && !control && key is >= Key.A and <= Key.Z)
        {
            char letter = (char)((shift ? 'A' : 'a') + (key - Key.A));
            return Escape + letter;
        }

        return null;
    }

    /// <summary>
    /// يحيط النص الملصق بعلامتي اللصق إن طلبهما البرنامج، ويوحّد نهايات الأسطر إلى CR كما يكتبها Enter.
    /// </summary>
    public static string EncodePaste(string text, bool bracketedPaste)
    {
        string normalized = text.Replace("\r\n", "\r", StringComparison.Ordinal).Replace('\n', '\r');
        if (!bracketedPaste)
        {
            return normalized;
        }

        // لا يُسمح للنص الملصق بإنهاء الإحاطة مبكرًا.
        normalized = normalized.Replace(Escape + "[201~", string.Empty, StringComparison.Ordinal);
        return $"{Escape}[200~{normalized}{Escape}[201~";
    }
}
