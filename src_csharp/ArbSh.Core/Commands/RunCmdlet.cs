namespace ArbSh.Core.Commands
{
    /// <summary>
    /// يشغّل ملف باء منفردًا أو مشروع تكوين عبر واجهتيهما العامتين.
    /// ينفذه المنفذ مسار عمليات خارجية بوسائطه الخام؛ الخصائص هنا للمساعدة فقط.
    /// </summary>
    [ArabicName("تشغيل")]
    [ArabicDescription("يترجم ملف باء ويشغّله، أو يشغّل مشروع تكوين الحالي، مع حفظ رمز الخروج والتشخيصات.")]
    public sealed class RunCmdlet : CmdletBase
    {
        /// <summary>
        /// ملف باء، أو مجلد مشروع، أو هدف في المشروع الحالي.
        /// </summary>
        [Parameter(Position = 0, HelpMessage = "ملف .baa أو مجلد مشروع تكوين أو اسم هدف؛ يُستخدم المشروع الحالي عند حذفه.")]
        [ArabicName("الهدف")]
        public string? Target { get; set; }

        /// <summary>
        /// وسائط تُمرر إلى البرنامج المترجم أو إلى «تكوين تشغيل».
        /// </summary>
        [Parameter(Position = 1, HelpMessage = "وسائط البرنامج المترجم، أو خيارات «تكوين تشغيل» مثل --نمط.")]
        [ArabicName("الوسائط")]
        public string[]? Arguments { get; set; }
    }
}
