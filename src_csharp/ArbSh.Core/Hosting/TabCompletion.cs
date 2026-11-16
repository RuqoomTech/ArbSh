namespace ArbSh.Core.Hosting;

/// <summary>
/// نتيجة الإكمال: النص بعد الإكمال وموضع المؤشر فيه، والمرشحون حين يتعدد الإكمال.
/// </summary>
public sealed record CompletionResult(string Text, int Caret, IReadOnlyList<string> Candidates);

/// <summary>
/// يكمل الكلمة عند المؤشر: اسم أمر عربي في أول الجملة، ومسار ملف أو مجلد في أي موضع.
/// </summary>
public static class TabCompletion
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// يكمل الكلمة المنتهية عند <paramref name="caret"/>. مرشح واحد يُكمل كاملًا ويتبعه فراغ
    /// (أو فاصل مسار للمجلد)؛ ومرشحون متعددون يُكمل جزؤهم المشترك ويُرجعون للعرض.
    /// </summary>
    public static CompletionResult Complete(string input, int caret, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(input);
        caret = Math.Clamp(caret, 0, input.Length);

        (int tokenStart, bool commandPosition) = FindToken(input, caret);
        string token = input[tokenStart..caret];
        char? quote = token.Length > 0 && token[0] is '\'' or '"' ? token[0] : null;
        string prefix = quote is null ? token : token[1..];

        var candidates = new List<(string Completion, string Display, bool IsDirectory)>();
        if (commandPosition && prefix.IndexOfAny(['/', '\\']) < 0)
        {
            candidates.AddRange(CommandDiscovery.GetArabicCatalog()
                .Select(command => command.Name)
                .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
                .Select(name => (name, name, false)));
        }

        candidates.AddRange(FindPaths(prefix, currentDirectory));
        if (candidates.Count == 0)
        {
            return new CompletionResult(input, caret, []);
        }

        string completion;
        bool finished = candidates.Count == 1;
        if (finished)
        {
            completion = candidates[0].Completion;
        }
        else
        {
            completion = CommonPrefix(candidates.Select(candidate => candidate.Completion).ToList());
            if (completion.Length <= prefix.Length)
            {
                return new CompletionResult(input, caret, candidates.Select(candidate => candidate.Display).ToList());
            }
        }

        // الاسم ذو الفراغ أو محارف الصدفة يُقتبس حرفيًا، إلا إذا فتح المستخدم اقتباسه.
        quote ??= NeedsQuoting(completion) ? (completion.Contains('\'') ? '"' : '\'') : null;
        string replacement = quote + completion;
        if (finished && !candidates[0].IsDirectory)
        {
            replacement += quote + " ";
        }

        string text = input[..tokenStart] + replacement + input[caret..];
        IReadOnlyList<string> shown = finished ? [] : candidates.Select(candidate => candidate.Display).ToList();
        return new CompletionResult(text, tokenStart + replacement.Length, shown);
    }

    private static (int TokenStart, bool CommandPosition) FindToken(string input, int caret)
    {
        int tokenStart = 0;
        bool commandPosition = true;
        char? quote = null;
        for (int i = 0; i < caret; i++)
        {
            char c = input[i];
            if (quote is not null)
            {
                quote = c == quote ? null : quote;
                continue;
            }

            if (c is '\'' or '"')
            {
                quote = c;
            }
            else if (c is ';' or '|' or '\n')
            {
                tokenStart = i + 1;
                commandPosition = true;
            }
            else if (char.IsWhiteSpace(c))
            {
                commandPosition &= i == tokenStart;
                tokenStart = i + 1;
            }
        }

        return (tokenStart, commandPosition);
    }

    private static IEnumerable<(string Completion, string Display, bool IsDirectory)> FindPaths(string prefix, string currentDirectory)
    {
        int split = prefix.LastIndexOfAny(['/', '\\']);
        string directoryPart = prefix[..(split + 1)];
        string namePart = prefix[(split + 1)..];
        char separator = split >= 0 ? prefix[split] : Path.DirectorySeparatorChar;

        string directory;
        try
        {
            directory = Path.GetFullPath(Path.Combine(currentDirectory, directoryPart.Length == 0 ? "." : directoryPart));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return [];
        }

        if (!Directory.Exists(directory))
        {
            return [];
        }

        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            // الأسماء المخفية تظهر فقط حين يبدأ المستخدم بنقطتها.
            AttributesToSkip = namePart.StartsWith('.') ? 0 : FileAttributes.Hidden | FileAttributes.System
        };

        try
        {
            return new DirectoryInfo(directory).EnumerateFileSystemInfos("*", options)
                .Where(entry => entry.Name.StartsWith(namePart, PathComparison))
                .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                .Select(entry =>
                {
                    bool isDirectory = entry is DirectoryInfo;
                    string name = isDirectory ? entry.Name + separator : entry.Name;
                    return (directoryPart + name, name, isDirectory);
                })
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string CommonPrefix(IReadOnlyList<string> values)
    {
        string first = values[0];
        int length = first.Length;
        foreach (string value in values.Skip(1))
        {
            int i = 0;
            while (i < length && i < value.Length
                && string.Compare(first, i, value, i, 1, PathComparison) == 0)
            {
                i++;
            }

            length = i;
        }

        return first[..length];
    }

    private static bool NeedsQuoting(string text) =>
        text.Any(c => char.IsWhiteSpace(c) || c is ';' or '|' or '<' or '>' or '(' or ')' or '#' or '$' or '\'' or '"');
}
