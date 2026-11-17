using System;
using System.IO;

namespace ArbSh.Core
{
    /// <summary>
    /// يمثل حالة جلسة أربش (مثل المجلد الحالي) عبر أوامر متعددة.
    /// </summary>
    public sealed class ShellSessionState
    {
        /// <summary>
        /// ينشئ حالة جلسة جديدة.
        /// </summary>
        /// <param name="initialDirectory">مجلد البداية للجلسة. إذا كان غير صالح يتم استخدام المجلد الحالي للنظام.</param>
        public ShellSessionState(string? initialDirectory = null)
        {
            CurrentDirectory = ResolveInitialDirectory(initialDirectory);
        }

        /// <summary>
        /// المجلد الحالي للجلسة.
        /// </summary>
        public string CurrentDirectory { get; set; }

        /// <summary>
        /// رمز خروج آخر عبارة نفذتها الجلسة. تحفظ العمليات الخارجية رمزها كما
        /// هو، ويستخدم أربش 127 لفشل الإطلاق و130 للإلغاء.
        /// </summary>
        public int LastExitCode { get; internal set; }

        /// <summary>
        /// مهام الخلفية التي بدأتها الجلسة بـ«&»؛ يوقفها المضيف قبل أن يغلق الجلسة.
        /// </summary>
        public BackgroundJobs Jobs { get; } = new();

        private static string ResolveInitialDirectory(string? initialDirectory)
        {
            if (!string.IsNullOrWhiteSpace(initialDirectory))
            {
                try
                {
                    string fullPath = Path.GetFullPath(initialDirectory);
                    if (Directory.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch
                {
                    // Fallback below.
                }
            }

            return Environment.CurrentDirectory;
        }
    }
}
