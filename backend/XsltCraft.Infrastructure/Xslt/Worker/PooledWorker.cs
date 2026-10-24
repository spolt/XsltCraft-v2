namespace XsltCraft.Infrastructure.Xslt.Worker;

internal enum PooledWorkerState
{
    /// <summary>Backoff gecikmesi bekleniyor; süreç yok.</summary>
    Scheduled,

    /// <summary>Süreç başlatılıyor / Hello bekleniyor.</summary>
    Starting,

    Idle,

    Busy,
}

/// <summary>Havuzdaki bir yuva. Tüm alanlar havuz kilidi altında okunur/yazılır.</summary>
internal sealed class PooledWorker
{
    public PooledWorker(long sequence, PooledWorkerState state, int affinityCapacity)
    {
        Sequence = sequence;
        State = state;
        Hashes = new HashAffinity(affinityCapacity);
    }

    public long Sequence { get; }

    public PooledWorkerState State { get; set; }

    public WorkerProcess? Process { get; set; }

    /// <summary>Geri dönüşüm kararı verildi; yedeği hazır olana dek hizmete devam eder (spawn-before-retire).</summary>
    public bool RetirePending { get; set; }

    /// <summary>Yedek hazır; mevcut istek bitince kapatılacak.</summary>
    public bool RetireAfterCurrent { get; set; }

    public HashAffinity Hashes { get; }

    public int Requests { get; set; }

    public long LastUsedTicks { get; set; }

    public bool IsServing => State is PooledWorkerState.Idle or PooledWorkerState.Busy;

    public int? Pid => Process?.Pid;
}
