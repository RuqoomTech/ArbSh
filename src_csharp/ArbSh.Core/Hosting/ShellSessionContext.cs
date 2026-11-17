using System;
using System.IO;
using System.Threading;

namespace ArbSh.Core
{
    /// <summary>
    /// يوفر سياقًا داخليًا لحالة الجلسة أثناء التنفيذ.
    /// </summary>
    internal static class ShellSessionContext
    {
        private static readonly AsyncLocal<ShellSessionState?> CurrentState = new();

        /// <summary>
        /// المجلد الحالي الفعلي للجلسة النشطة.
        /// </summary>
        internal static string CurrentDirectory
        {
            get => CurrentState.Value?.CurrentDirectory ?? Environment.CurrentDirectory;
            set
            {
                if (CurrentState.Value is null)
                {
                    Environment.CurrentDirectory = value;
                    return;
                }

                CurrentState.Value.CurrentDirectory = value;
            }
        }

        /// <summary>
        /// مهام الخلفية للجلسة النشطة، أو null خارج جلسة.
        /// </summary>
        internal static BackgroundJobs? Jobs => CurrentState.Value?.Jobs;

        /// <summary>
        /// رمز خروج آخر عبارة في الجلسة النشطة.
        /// </summary>
        internal static int LastExitCode
        {
            get => CurrentState.Value?.LastExitCode ?? 0;
            set
            {
                if (CurrentState.Value is not null)
                {
                    CurrentState.Value.LastExitCode = value;
                }
            }
        }

        /// <summary>
        /// يدخل حالة جلسة في نطاق التنفيذ الحالي.
        /// </summary>
        /// <param name="state">حالة الجلسة.</param>
        /// <returns>نطاق قابل للإلغاء يعيد الحالة السابقة.</returns>
        internal static IDisposable Push(ShellSessionState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            ShellSessionState? previous = CurrentState.Value;
            CurrentState.Value = state;
            return new Scope(() => CurrentState.Value = previous);
        }

        /// <summary>
        /// يحوّل مسارًا منطقيًا إلى مسار مطلق بالاعتماد على مجلد الجلسة الحالي.
        /// </summary>
        /// <param name="path">المسار المدخل.</param>
        /// <returns>مسار مطلق.</returns>
        internal static string ResolvePath(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            string trimmed = path.Trim();
            if (trimmed.Length == 0)
            {
                return CurrentDirectory;
            }

            string expanded = ExpandHomePrefix(trimmed);
            return Path.IsPathRooted(expanded)
                ? Path.GetFullPath(expanded)
                : Path.GetFullPath(Path.Combine(CurrentDirectory, expanded));
        }

        private static string ExpandHomePrefix(string path)
        {
            if (path == "~")
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string relative = path.Substring(2);
                return Path.Combine(home, relative);
            }

            return path;
        }

        private sealed class Scope : IDisposable
        {
            private readonly Action _onDispose;
            private bool _disposed;

            public Scope(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _onDispose();
            }
        }
    }
}
