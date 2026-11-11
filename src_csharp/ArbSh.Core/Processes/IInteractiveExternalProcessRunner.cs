namespace ArbSh.Core.Processes;

/// <summary>
/// منفذ يستطيع تشغيل عملية أمامية داخل طرفية زائفة (ConPTY/PTY) تستقبل إدخال المستخدم أثناء عملها.
/// </summary>
public interface IInteractiveExternalProcessRunner : IExternalProcessRunner
{
    /// <summary>
    /// هل يدعم المنفذ الطرفية الزائفة على هذه المنصة؟ عند false يبقى المسار غير التفاعلي هو المستخدم.
    /// </summary>
    bool SupportsInteractive { get; }

    /// <summary>
    /// يشغّل العملية متصلة بطرفية زائفة: ترى طرفية حقيقية فتُظهر موجهاتها فورًا،
    /// وتقرأ ما يرسله <paramref name="console"/>. يصل الخرج نصًا خاليًا من تسلسلات التحكم،
    /// وتندمج قناتا stdout وstderr كما في أي طرفية.
    /// </summary>
    Task<ExternalProcessResult> RunInteractiveAsync(
        ExternalProcessRequest request,
        InteractiveConsole console,
        IExternalProcessOutputSink outputSink,
        CancellationToken cancellationToken = default);
}
