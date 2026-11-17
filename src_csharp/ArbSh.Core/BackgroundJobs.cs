namespace ArbSh.Core;

/// <summary>
/// حالة مهمة خلفية.
/// </summary>
public enum BackgroundJobState
{
    /// <summary>ما زالت تعمل.</summary>
    Running,

    /// <summary>انتهت من تلقاء نفسها.</summary>
    Completed,

    /// <summary>أوقفها المستخدم أو المضيف.</summary>
    Stopped
}

/// <summary>
/// أمر يعمل في الخلفية بنسخة من الجلسة، وتملك الجلسة شجرة عملياته حتى تنتهي أو تُوقف.
/// </summary>
public sealed class BackgroundJob
{
    private const int NotFinished = int.MinValue;
    private readonly CancellationTokenSource _cancellation = new();
    private int _exitCode = NotFinished;

    internal BackgroundJob(int id, string command)
    {
        Id = id;
        Command = command;
    }

    /// <summary>رقم المهمة في جلستها، يبدأ من 1.</summary>
    public int Id { get; }

    /// <summary>الأمر كما كُتب بلا «&».</summary>
    public string Command { get; }

    /// <summary>رمز خروج المهمة بعد انتهائها، أو null ما دامت تعمل.</summary>
    public int? ExitCode => Volatile.Read(ref _exitCode) is int code and not NotFinished ? code : null;

    /// <summary>رسالة الخطأ إذا فشل تنفيذ المهمة نفسه.</summary>
    public string? Error { get; private set; }

    /// <summary>حالة المهمة الآن.</summary>
    public BackgroundJobState State => ExitCode is null
        ? BackgroundJobState.Running
        : _cancellation.IsCancellationRequested ? BackgroundJobState.Stopped : BackgroundJobState.Completed;

    internal Task Completion { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// يطلب إيقاف المهمة؛ ينهي المنفذ الخارجي شجرة عملياتها كما في إلغاء أمر المقدمة.
    /// </summary>
    public void Stop()
    {
        if (ExitCode is null)
        {
            _cancellation.Cancel();
        }
    }

    internal void Start(Func<CancellationToken, int> body, Action<BackgroundJob> finished)
    {
        Completion = Task.Run(() =>
        {
            int exitCode;
            try
            {
                exitCode = body(_cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                exitCode = HostExitCodes.Cancelled;
            }
            catch (Exception exception)
            {
                Error = exception.Message;
                exitCode = 1;
            }

            Volatile.Write(ref _exitCode, exitCode);
            finished(this);
        });
    }
}

/// <summary>
/// مهام الخلفية لجلسة واحدة.
/// </summary>
public sealed class BackgroundJobs
{
    private readonly object _sync = new();
    private readonly List<BackgroundJob> _jobs = [];
    private int _nextId = 1;

    /// <summary>كل مهام الجلسة بترتيب بدئها.</summary>
    public IReadOnlyList<BackgroundJob> List()
    {
        lock (_sync)
        {
            return [.. _jobs];
        }
    }

    /// <summary>المهمة ذات الرقم، أو null.</summary>
    public BackgroundJob? Find(int id)
    {
        lock (_sync)
        {
            return _jobs.Find(job => job.Id == id);
        }
    }

    /// <summary>
    /// يوقف كل مهمة جارية وينتظر انتهاءها حتى المهلة. يعيد false إذا بقيت مهمة تعمل بعدها.
    /// </summary>
    public bool StopAll(TimeSpan timeout)
    {
        BackgroundJob[] running = Running();
        foreach (BackgroundJob job in running)
        {
            job.Stop();
        }

        return Task.WaitAll([.. running.Select(job => job.Completion)], timeout);
    }

    /// <summary>
    /// ينتظر انتهاء كل المهام الجارية؛ عند الإلغاء يوقفها ثم ينتظرها حتى المهلة.
    /// </summary>
    public void WaitAll(CancellationToken cancellationToken, TimeSpan stopTimeout)
    {
        try
        {
            Task.WaitAll([.. Running().Select(job => job.Completion)], cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StopAll(stopTimeout);
        }
    }

    internal BackgroundJob Add(string command)
    {
        lock (_sync)
        {
            var job = new BackgroundJob(_nextId++, command);
            _jobs.Add(job);
            return job;
        }
    }

    private BackgroundJob[] Running() => [.. List().Where(job => job.State == BackgroundJobState.Running)];
}
