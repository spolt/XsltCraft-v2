namespace XsltCraft.XsltWorker.Contracts.Messages;

/// <summary>Worker'ın açılışta stdout'a yazdığı ilk çerçevenin başlığı (el sıkışma).</summary>
public sealed record HelloHeader
{
    /// <summary>Worker'ın konuştuğu protokol; <see cref="ProtocolVersion.Current"/> ile eşleşmezse worker reddedilir.</summary>
    public int Protocol { get; init; }

    public string WorkerVersion { get; init; } = "";

    /// <summary>Saxon sürümü (W2'de dolar; motor yüklü değilse <c>null</c>).</summary>
    public string? SaxonVersion { get; init; }

    public int Pid { get; init; }
}
