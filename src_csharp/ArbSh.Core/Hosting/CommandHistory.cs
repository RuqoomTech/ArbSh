using System.Text.Encodings.Web;
using System.Text.Json;

namespace ArbSh.Core.Hosting;

/// <summary>
/// سجل الأوامر المنفذة مع التنقل بالسهمين، محفوظًا بين الجلسات في ملف اختياري.
/// كل سطر في الملف أمر واحد بصيغة نص JSON، فيبقى الأمر متعدد الأسطر سطرًا واحدًا.
/// </summary>
public sealed class CommandHistory
{
    // يبقى النص العربي مقروءًا في الملف بدل تسلسلات الهروب.
    private static readonly JsonSerializerOptions FileFormat = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private readonly List<string> _entries = [];
    private readonly string? _filePath;
    private readonly int _capacity;
    private int _index;
    private string _draft = string.Empty;

    /// <summary>
    /// ينشئ سجلًا ويحمّل ملفه إن وُجد؛ بلا ملف يبقى السجل في الذاكرة فقط.
    /// </summary>
    public CommandHistory(string? filePath = null, int capacity = 1000)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _filePath = filePath;
        _capacity = capacity;
        Load();
        _index = _entries.Count;
    }

    /// <summary>
    /// ملف السجل الافتراضي للمستخدم الحالي.
    /// </summary>
    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ArbSh",
        "history");

    /// <summary>
    /// الأوامر من الأقدم إلى الأحدث.
    /// </summary>
    public IReadOnlyList<string> Entries => _entries;

    /// <summary>
    /// يضيف أمرًا منفذًا ويعيد التنقل إلى المسودة. يتجاهل الفارغ وتكرار آخر أمر.
    /// </summary>
    public void Add(string entry)
    {
        _index = _entries.Count;
        _draft = string.Empty;
        if (string.IsNullOrWhiteSpace(entry) || (_entries.Count > 0 && _entries[^1] == entry))
        {
            return;
        }

        _entries.Add(entry);
        if (_entries.Count > _capacity)
        {
            _entries.RemoveAt(0);
        }

        _index = _entries.Count;
        Append(entry);
    }

    /// <summary>
    /// يرجع الأمر الأقدم التالي، ويحفظ المسودة عند أول خطوة؛ null عند أول السجل.
    /// </summary>
    public string? Previous(string draft)
    {
        if (_index == 0)
        {
            return null;
        }

        if (_index == _entries.Count)
        {
            _draft = draft;
        }

        return _entries[--_index];
    }

    /// <summary>
    /// يرجع الأمر الأحدث التالي، ثم المسودة المحفوظة؛ null إذا لم يكن هناك تنقل.
    /// </summary>
    public string? Next()
    {
        if (_index >= _entries.Count)
        {
            return null;
        }

        _index++;
        return _index == _entries.Count ? _draft : _entries[_index];
    }

    private void Load()
    {
        if (_filePath is null || !File.Exists(_filePath))
        {
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(_filePath);
            foreach (string line in lines)
            {
                string? entry = TryDecode(line);
                if (!string.IsNullOrWhiteSpace(entry) && (_entries.Count == 0 || _entries[^1] != entry))
                {
                    _entries.Add(entry);
                }
            }

            if (_entries.Count > _capacity)
            {
                _entries.RemoveRange(0, _entries.Count - _capacity);
            }

            // يُضغط الملف حين يتضاعف كي لا ينمو بلا حد.
            if (lines.Length > 2 * _capacity)
            {
                File.WriteAllLines(_filePath, _entries.Select(entry => JsonSerializer.Serialize(entry, FileFormat)));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // السجل راحة لا شرط: يبقى في الذاكرة إذا تعذر الملف.
        }
    }

    private void Append(string entry)
    {
        if (_filePath is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
            File.AppendAllText(_filePath, JsonSerializer.Serialize(entry, FileFormat) + "\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string? TryDecode(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<string>(line);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
