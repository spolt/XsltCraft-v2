namespace XsltCraft.Application.Xslt.Engines;

/// <summary>Motor-bağımsız XSLT hata sınıfı; HTTP eşlemesi ve kullanıcı mesajı buna göre yapılır.</summary>
public enum XsltFailureKind
{
    /// <summary>Girdi XML'i okunamadı / XML sınırlarını aştı.</summary>
    Xml,

    /// <summary>Stylesheet derlenemedi.</summary>
    Compile,

    /// <summary>Dönüşüm sırasında çalışma hatası.</summary>
    Runtime,

    /// <summary>Güvenlik taraması/sandbox reddetti.</summary>
    Security,

    /// <summary>Desteklenmeyen sürüm veya yapı.</summary>
    Unsupported,

    /// <summary>Süre sınırı aşıldı.</summary>
    Timeout,

    /// <summary>Bellek sınırı aşıldı.</summary>
    MemoryLimit,

    /// <summary>Çıktı boyut sınırı aşıldı.</summary>
    OutputLimit,

    /// <summary>Diğer yapısal sınırlar (derinlik, ifade boyutu vb.) aşıldı.</summary>
    LimitExceeded,

    /// <summary>Motor süreci çöktü.</summary>
    EngineCrashed,

    /// <summary>Kuyruk dolu.</summary>
    Busy,

    /// <summary>Kullanıcının eşzamanlı istek kotası dolu.</summary>
    UserBusy,

    /// <summary>Kullanılabilir motor yok.</summary>
    EngineUnavailable,

    /// <summary>Motorun beklenmeyen iç hatası.</summary>
    EngineInternal,
}
