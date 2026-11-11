namespace ArbSh.Core.Processes;

/// <summary>
/// جانب المضيف من الطرفية الزائفة: يرسل المضيف إليه أسطر المستخدم، ويربطه المنفذ
/// بالعملية الأمامية التفاعلية ما دامت تعمل.
/// </summary>
public sealed class InteractiveConsole
{
    private readonly object _sync = new();
    private Action<string>? _writeInput;

    /// <summary>
    /// هل توجد عملية أمامية تفاعلية تستقبل الإدخال الآن؟
    /// </summary>
    public bool IsAttached
    {
        get
        {
            lock (_sync)
            {
                return _writeInput is not null;
            }
        }
    }

    /// <summary>
    /// يرسل سطرًا، ولو كان فارغًا، إلى العملية الأمامية التفاعلية كأن المستخدم ضغط Enter بعده.
    /// </summary>
    /// <returns>false إذا لم توجد عملية تفاعلية تستقبله.</returns>
    public bool TrySendLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        Action<string>? writeInput;
        lock (_sync)
        {
            writeInput = _writeInput;
        }

        if (writeInput is null)
        {
            return false;
        }

        // مفتاح Enter في الطرفية هو CR؛ وتحوّله الطرفية الزائفة إلى نهاية سطر للبرنامج.
        writeInput(line + "\r");
        return true;
    }

    internal IDisposable Attach(Action<string> writeInput)
    {
        lock (_sync)
        {
            if (_writeInput is not null)
            {
                throw new InvalidOperationException("توجد عملية تفاعلية أمامية أخرى على هذه الطرفية.");
            }

            _writeInput = writeInput;
        }

        return new Attachment(this);
    }

    private sealed class Attachment(InteractiveConsole owner) : IDisposable
    {
        public void Dispose()
        {
            lock (owner._sync)
            {
                owner._writeInput = null;
            }
        }
    }
}
