using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.Infrastructure.Xslt.Worker;

/// <summary>İzole XSLT worker havuzu ayarları (<c>XsltWorker</c> bölümü).</summary>
public sealed class XsltWorkerOptions
{
    public const string SectionName = "XsltWorker";

    /// <summary><c>false</c> → havuz süreç başlatmaz, her istek <c>EngineUnavailable</c>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Açık worker yolu (apphost veya <c>.dll</c>). Boşsa <see cref="XsltWorkerLocator"/> varsayılan sırayı dener.</summary>
    public string? ExecutablePath { get; set; }

    public int PoolSize { get; set; } = 2;

    public int MaxRequestsPerWorker { get; set; } = 500;

    public int MaxWorkerAgeMinutes { get; set; } = 30;

    /// <summary>İstek sırasında WorkingSet bu değeri aşarsa worker öldürülür (<c>MemoryLimit</c>). 0 → kapalı.</summary>
    public int MaxWorkingSetMb { get; set; } = 768;

    /// <summary>İstek sonrası WorkingSet bu değeri aşarsa worker geri dönüştürülür. 0 → kapalı.</summary>
    public int RecycleWorkingSetMb { get; set; } = 512;

    /// <summary><c>DOTNET_GCHeapHardLimit</c> (MB). 0 → değişken verilmez.</summary>
    public int GcHeapHardLimitMb { get; set; } = 512;

    public int QueueWaitMs { get; set; } = 2000;

    public int MaxQueue { get; set; } = 16;

    public int RenderTimeoutMs { get; set; } = 10_000;

    public int ValidateTimeoutMs { get; set; } = 5_000;

    /// <summary>Süreç başlatma + Hello için üst süre.</summary>
    public int StartupTimeoutMs { get; set; } = 10_000;

    /// <summary>Kill/Shutdown sonrası çıkış bekleme süresi.</summary>
    public int ShutdownGraceMs { get; set; } = 2_000;

    public int WorkingSetPollMs { get; set; } = 200;

    public int RespawnBackoffInitialMs { get; set; } = 500;

    public int RespawnBackoffMaxMs { get; set; } = 30_000;

    public int BreakerFailureThreshold { get; set; } = 5;

    public int BreakerWindowSeconds { get; set; } = 60;

    public int BreakerOpenSeconds { get; set; } = 60;

    /// <summary>Yumuşak afinite: worker başına hatırlanan son XSLT özeti sayısı.</summary>
    public int AffinityHashesPerWorker { get; set; } = 16;

    /// <summary>Worker sürecini BelowNormal önceliğe indir.</summary>
    public bool LowerPriority { get; set; } = true;

    /// <summary>Op'a göre varsayılan sert zaman aşımı (istek özel değer vermediyse).</summary>
    public TimeSpan TimeoutFor(string op) => op switch
    {
        WorkerOps.Validate or WorkerOps.Ping => TimeSpan.FromMilliseconds(ValidateTimeoutMs),
        _ => TimeSpan.FromMilliseconds(RenderTimeoutMs),
    };

    /// <summary>Tutarsız ayarları döndürür (boş → geçerli).</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (PoolSize < 1) errors.Add($"{nameof(PoolSize)} ≥ 1 olmalı.");
        if (MaxRequestsPerWorker < 1) errors.Add($"{nameof(MaxRequestsPerWorker)} ≥ 1 olmalı.");
        if (MaxWorkerAgeMinutes < 1) errors.Add($"{nameof(MaxWorkerAgeMinutes)} ≥ 1 olmalı.");
        if (MaxWorkingSetMb < 0 || RecycleWorkingSetMb < 0 || GcHeapHardLimitMb < 0) errors.Add("Bellek sınırları negatif olamaz.");
        if (QueueWaitMs < 0 || MaxQueue < 0) errors.Add("Kuyruk ayarları negatif olamaz.");
        if (RenderTimeoutMs < 1 || ValidateTimeoutMs < 1 || StartupTimeoutMs < 1) errors.Add("Zaman aşımları ≥ 1 ms olmalı.");
        if (WorkingSetPollMs < 10) errors.Add($"{nameof(WorkingSetPollMs)} ≥ 10 olmalı.");
        if (RespawnBackoffInitialMs < 0 || RespawnBackoffMaxMs < RespawnBackoffInitialMs) errors.Add("Backoff aralığı geçersiz.");
        if (BreakerFailureThreshold < 1 || BreakerWindowSeconds < 1 || BreakerOpenSeconds < 0) errors.Add("Kesici ayarları geçersiz.");
        if (AffinityHashesPerWorker < 0) errors.Add($"{nameof(AffinityHashesPerWorker)} negatif olamaz.");
        return errors;
    }
}
