using System.Globalization;

namespace ArbSh.Core.Commands
{
    /// <summary>
    /// يعرض مهام الخلفية للجلسة وحالاتها.
    /// </summary>
    [ArabicName("المهام")]
    [ArabicDescription("يعرض مهام الخلفية التي بدأتها «&» وحالة كل منها.")]
    public sealed class ListJobsCmdlet : CmdletBase
    {
        /// <inheritdoc />
        public override void EndProcessing()
        {
            IReadOnlyList<BackgroundJob> jobs = ShellSessionContext.Jobs?.List() ?? [];
            if (jobs.Count == 0)
            {
                WriteObject("لا توجد مهام خلفية.");
                return;
            }

            foreach (BackgroundJob job in jobs)
            {
                string state = job.State switch
                {
                    BackgroundJobState.Running => "تعمل",
                    BackgroundJobState.Stopped => "أُوقفت",
                    _ => $"انتهت بالرمز {job.ExitCode}"
                };
                WriteObject($"[{job.Id}] {state}: {job.Command}");
            }
        }
    }

    /// <summary>
    /// يوقف مهمة خلفية وشجرة عملياتها.
    /// </summary>
    [ArabicName("أوقف")]
    [ArabicDescription("يوقف مهمة خلفية برقمها، مع كل العمليات التي بدأتها.")]
    public sealed class StopJobCmdlet : CmdletBase
    {
        /// <summary>
        /// رقم المهمة كما يعرضه «المهام»، بالأرقام العربية أو الغربية.
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, HelpMessage = "رقم المهمة المراد إيقافها.")]
        [ArabicName("الرقم")]
        public string? JobId { get; set; }

        /// <inheritdoc />
        public override void EndProcessing()
        {
            if (!TryParseId(JobId, out int id) || ShellSessionContext.Jobs?.Find(id) is not BackgroundJob job)
            {
                WriteObject(new PipelineObject($"لا توجد مهمة خلفية بالرقم: {JobId}", isError: true));
                return;
            }

            if (job.State != BackgroundJobState.Running)
            {
                WriteObject($"المهمة [{job.Id}] منتهية بالفعل.");
                return;
            }

            job.Stop();
            WriteObject($"أُرسل الإيقاف إلى المهمة [{job.Id}].");
        }

        private static bool TryParseId(string? text, out int id)
        {
            // تُقبل الأرقام العربية الهندية (٠-٩ و۰-۹) كما الغربية.
            string digits = string.Concat((text ?? string.Empty).Trim().Select(c =>
                char.IsDigit(c) ? (char)('0' + (int)char.GetNumericValue(c)) : c));
            return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out id);
        }
    }
}
