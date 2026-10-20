namespace ArbSh.Terminal.Rendering;

/// <summary>
/// يحدد مقاطع النص التي تُرسم بالخط العربي.
/// لا يكفي بديل الخطوط وحده: المسافات وعلامات الترقيم موجودة في الخط اللاتيني الثابت العرض،
/// فتأخذ منه عرضًا واسعًا بين الكلمات العربية. لذا تُلحق المحايدات بالعربية حين تقع بين كلمات عربية.
/// </summary>
public static class ArabicTextRuns
{
    private enum CharClass
    {
        Neutral,
        Arabic,
        Other
    }

    /// <summary>
    /// يعيد مقاطع (البداية، الطول) التي تُرسم بالخط العربي، بالترتيب المنطقي.
    /// </summary>
    /// <param name="text">النص المنطقي.</param>
    public static IReadOnlyList<(int Start, int Length)> Find(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var classes = new CharClass[text.Length];
        bool hasArabic = false;
        for (int i = 0; i < text.Length; i++)
        {
            classes[i] = Classify(text[i]);
            hasArabic |= classes[i] == CharClass.Arabic;
        }

        if (!hasArabic)
        {
            return [];
        }

        // يأخذ كل مقطع محايد صنف ما حوله: عربي إذا سبقه عربي ولم يتبعه لاتيني، أو بدأ النص وتبعه عربي.
        for (int i = 0; i < text.Length;)
        {
            if (classes[i] != CharClass.Neutral)
            {
                i++;
                continue;
            }

            int end = i;
            while (end < text.Length && classes[end] == CharClass.Neutral)
            {
                end++;
            }

            CharClass previous = i > 0 ? classes[i - 1] : CharClass.Neutral;
            CharClass next = end < text.Length ? classes[end] : CharClass.Neutral;
            bool arabic = (previous == CharClass.Arabic && next != CharClass.Other)
                || (previous == CharClass.Neutral && next == CharClass.Arabic);
            CharClass resolved = arabic ? CharClass.Arabic : CharClass.Other;
            for (int k = i; k < end; k++)
            {
                classes[k] = resolved;
            }

            i = end;
        }

        var runs = new List<(int Start, int Length)>();
        for (int i = 0; i < text.Length;)
        {
            if (classes[i] != CharClass.Arabic)
            {
                i++;
                continue;
            }

            int start = i;
            while (i < text.Length && classes[i] == CharClass.Arabic)
            {
                i++;
            }

            runs.Add((start, i - start));
        }

        return runs;
    }

    private static CharClass Classify(char c)
    {
        if (c is (>= '\u0600' and <= '\u06FF')
            or (>= '\u0750' and <= '\u077F')
            or (>= '\u08A0' and <= '\u08FF')
            or (>= '\uFB50' and <= '\uFDFF')
            or (>= '\uFE70' and <= '\uFEFF'))
        {
            return CharClass.Arabic;
        }

        return char.IsLetter(c) ? CharClass.Other : CharClass.Neutral;
    }
}
