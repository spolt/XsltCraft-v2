namespace XsltCraft.XsltWorker.Tests.Support;

/// <summary>
/// Gerçek worker süreci başlatan test sınıfları bu koleksiyonda SIRAYLA koşar: paralel süreç başlatma/öldürme CPU'yu
/// doyurup süre iddialarını (zaman aşımı, bekçi) kararsızlaştırmasın; test sürecine konan sahte ortam değişkenleri
/// (secret testi) başka testin worker'ına sızmasın.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WorkerProcessesCollection
{
    public const string Name = "WorkerProcesses";
}
