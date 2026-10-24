namespace XsltCraft.Infrastructure.Xslt.Worker;

internal enum WorkerPoolEventKind
{
    Launched,
    Ready,
    StartFailed,
    RecycleScheduled,
    Retired,
    Exited,
    BreakerOpened,
}

/// <summary>Havuz yaşam döngüsü olayı (tanı/test). Havuz kilidi altında yükseltilebilir — dinleyici hızlı olmalı.</summary>
internal sealed record WorkerPoolEvent(WorkerPoolEventKind Kind, long Sequence, int? Pid, string? Reason);
