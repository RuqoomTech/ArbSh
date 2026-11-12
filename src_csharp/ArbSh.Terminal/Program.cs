using Avalonia;

namespace ArbSh.Terminal;

internal static class Program
{
    internal static string? InitialWorkingDirectory { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        InitialWorkingDirectory = ResolveInitialWorkingDirectory(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }

    private static string? ResolveInitialWorkingDirectory(string[] args)
    {
        if (args.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg.StartsWith("--working-dir=", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeWorkingDirectory(arg.Substring("--working-dir=".Length));
            }

            if (string.Equals(arg, "--working-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return NormalizeWorkingDirectory(args[i + 1]);
            }
        }

        return null;
    }

    private static string? NormalizeWorkingDirectory(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        // «~» مجلد المستخدم الذي يشغّل الطرفية؛ تستخدمه اختصارات المثبت لكل المستخدمين.
        if (candidate.Trim() == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        try
        {
            string fullPath = System.IO.Path.GetFullPath(candidate.Trim());
            return System.IO.Directory.Exists(fullPath) ? fullPath : null;
        }
        catch
        {
            return null;
        }
    }
}
