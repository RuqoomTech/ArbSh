using ArbSh.Core.Processes;

namespace ArbSh.Core.Eco;

/// <summary>
/// خطوة عملية واحدة في خطة <c>تشغيل</c>، مع الاسم الذي يظهر في رسائل الفشل.
/// </summary>
public sealed record EcoRunStep(ExternalProcessRequest Request, string DisplayName);

/// <summary>
/// خطة <c>تشغيل</c>: خطوات تُنفذ بالترتيب وتتوقف عند أول رمز خروج غير صفري،
/// أو رسالة خطأ إذا تعذر تحديد ما يُشغَّل. تحذف المجلد المؤقت عند التخلص منها.
/// </summary>
public sealed class EcoRunPlan : IDisposable
{
    private EcoRunPlan(IReadOnlyList<EcoRunStep> steps, string? temporaryDirectory, string? error)
    {
        Steps = steps;
        TemporaryDirectory = temporaryDirectory;
        Error = error;
    }

    /// <summary>
    /// الخطوات بالترتيب؛ فارغة عند وجود خطأ.
    /// </summary>
    public IReadOnlyList<EcoRunStep> Steps { get; }

    /// <summary>
    /// مجلد مؤقت تملكه الخطة لملف تنفيذي مترجم، أو null.
    /// </summary>
    public string? TemporaryDirectory { get; }

    /// <summary>
    /// سبب تعذر التخطيط، أو null عند النجاح.
    /// </summary>
    public string? Error { get; }

    internal static EcoRunPlan Failed(string error) => new([], null, error);

    internal static EcoRunPlan Of(IReadOnlyList<EcoRunStep> steps, string? temporaryDirectory = null) =>
        new(steps, temporaryDirectory, null);

    /// <inheritdoc />
    public void Dispose()
    {
        if (TemporaryDirectory is null)
        {
            return;
        }

        // قد يبقى الملف التنفيذي مقفلًا لحظات بعد إنهاء شجرته على Windows.
        for (int attempt = 0; attempt < 5 && Directory.Exists(TemporaryDirectory); attempt++)
        {
            try
            {
                Directory.Delete(TemporaryDirectory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100 * (attempt + 1));
            }
        }
    }
}

/// <summary>
/// يحوّل وسائط <c>تشغيل</c> إلى استدعاءات لواجهات باء وتكوين العامة.
/// لا يحلل أربش البيان أو القفل؛ يكتشف المشروع بوجود <c>مشروع.تكوين</c> فقط.
/// </summary>
public static class EcoRunPlanner
{
    /// <summary>
    /// اسم ملف بيان مشروع تكوين.
    /// </summary>
    public const string ProjectManifestName = "مشروع.تكوين";

    /// <summary>
    /// برنامج مصرّف باء على PATH.
    /// </summary>
    public const string BaaExecutable = "baa";

    /// <summary>
    /// برنامج تكوين على PATH.
    /// </summary>
    public const string TakweenExecutable = "تكوين";

    /// <summary>
    /// رمز الخروج عند تعذر تحديد ما يُشغَّل.
    /// </summary>
    public const int UsageExitCode = 2;

    private static readonly string[] BaaSourceExtensions = [".baa", ".باء"];

    /// <summary>
    /// يبني خطة التشغيل:
    /// ملف باء منفرد يُترجم بـ<c>baa</c> إلى مجلد مؤقت ثم يُشغَّل بباقي الوسائط؛
    /// ومجلد مشروع، أو أقرب مشروع يحوي مجلد العمل، يُشغَّل بـ<c>تكوين تشغيل</c> من جذره.
    /// </summary>
    /// <param name="arguments">وسائط الأمر كما كُتبت.</param>
    /// <param name="currentDirectory">مجلد عمل الجلسة.</param>
    /// <param name="standardInput">نص يُمرر إلى البرنامج المشغَّل، أو null لإغلاق الإدخال.</param>
    public static EcoRunPlan Plan(
        IReadOnlyList<string> arguments,
        string currentDirectory,
        string? standardInput = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        string? first = arguments.Count > 0 ? arguments[0] : null;
        string? firstPath = string.IsNullOrEmpty(first)
            ? null
            : Path.GetFullPath(Path.Combine(currentDirectory, first));

        if (firstPath is not null && IsBaaSource(first!))
        {
            return File.Exists(firstPath)
                ? PlanSingleFile(firstPath, arguments.Skip(1), currentDirectory, standardInput)
                : EcoRunPlan.Failed($"ملف باء غير موجود: {first}");
        }

        if (firstPath is not null && Directory.Exists(firstPath))
        {
            return File.Exists(Path.Combine(firstPath, ProjectManifestName))
                ? PlanProject(firstPath, arguments.Skip(1), standardInput)
                : EcoRunPlan.Failed($"المجلد ليس مشروع تكوين؛ لا يحوي {ProjectManifestName}: {first}");
        }

        string? projectRoot = FindProjectRoot(currentDirectory);
        if (projectRoot is not null)
        {
            return PlanProject(projectRoot, arguments, standardInput);
        }

        return EcoRunPlan.Failed(
            $"لا يوجد مشروع تكوين في هذا المجلد أو ما فوقه. استخدم «تشغيل ملف.baa» أو انتقل إلى مجلد فيه {ProjectManifestName}.");
    }

    /// <summary>
    /// أقرب مجلد يحوي <c>مشروع.تكوين</c> بدءًا من المجلد المعطى صعودًا، أو null.
    /// </summary>
    public static string? FindProjectRoot(string directory)
    {
        for (DirectoryInfo? current = new(Path.GetFullPath(directory)); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, ProjectManifestName)))
            {
                return current.FullName;
            }
        }

        return null;
    }

    private static bool IsBaaSource(string path) =>
        BaaSourceExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    private static EcoRunPlan PlanSingleFile(
        string sourcePath,
        IEnumerable<string> programArguments,
        string currentDirectory,
        string? standardInput)
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arbsh-run-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string program = Path.Combine(
            temporaryDirectory,
            Path.GetFileNameWithoutExtension(sourcePath) + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));

        var compile = new ExternalProcessRequest(
            BaaExecutable,
            [sourcePath, "-o", program],
            currentDirectory);
        var run = new ExternalProcessRequest(
            program,
            programArguments,
            currentDirectory,
            standardInputMode: standardInput is null ? ExternalProcessInputMode.Closed : ExternalProcessInputMode.Text,
            standardInputText: standardInput);

        return EcoRunPlan.Of(
            [new EcoRunStep(compile, BaaExecutable), new EcoRunStep(run, Path.GetFileName(sourcePath))],
            temporaryDirectory);
    }

    private static EcoRunPlan PlanProject(
        string projectRoot,
        IEnumerable<string> takweenArguments,
        string? standardInput)
    {
        var run = new ExternalProcessRequest(
            TakweenExecutable,
            ["تشغيل", .. takweenArguments],
            projectRoot,
            standardInputMode: standardInput is null ? ExternalProcessInputMode.Closed : ExternalProcessInputMode.Text,
            standardInputText: standardInput);

        return EcoRunPlan.Of([new EcoRunStep(run, TakweenExecutable)]);
    }
}
