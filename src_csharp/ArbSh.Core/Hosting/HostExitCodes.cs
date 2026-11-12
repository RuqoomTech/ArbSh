namespace ArbSh.Core;

/// <summary>
/// رموز خروج أربش المجمدة في <c>arbsh-host-v1</c>. أي رمز آخر هو رمز خروج البرنامج الخارجي نفسه.
/// </summary>
public static class HostExitCodes
{
    /// <summary>نجح الأمر.</summary>
    public const int Success = 0;

    /// <summary>أبلغ أمر عربي داخلي عن خطأ.</summary>
    public const int CommandFailed = 1;

    /// <summary>وسائط أربش أو هدف «تشغيل» غير صالح.</summary>
    public const int Usage = 2;

    /// <summary>تعذر امتلاك شجرة العملية الخارجية.</summary>
    public const int OwnershipFailed = 125;

    /// <summary>البرنامج الخارجي غير موجود أو تعذر إطلاقه.</summary>
    public const int LaunchFailed = 127;

    /// <summary>أُلغي الأمر وأُنهيت شجرة عملياته.</summary>
    public const int Cancelled = 130;
}
