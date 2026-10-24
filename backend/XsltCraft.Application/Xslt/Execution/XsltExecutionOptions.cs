using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Application.Xslt.Execution;

/// <summary>
/// <c>Xslt</c> yapılandırma bölümü (POCO; Application <c>IOptions</c> almaz — DI fabrikayla verir).
/// </summary>
public sealed class XsltExecutionOptions
{
    public const string SectionName = "Xslt";

    public XsltEngineRoutingOptions Engine { get; set; } = new();

    public XsltRouteLimitsOptions Limits { get; set; } = new();

    public XsltGridOptions Grid { get; set; } = new();

    public XsltUserGateOptions UserGate { get; set; } = new();

    /// <summary>Tutarsız ayarları döndürür (boş → geçerli).</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(Engine.Raw) || !Enum.IsDefined(Engine.Grid))
            errors.Add("Engine:Raw/Grid geçersiz (Auto | DotNet | Saxon).");

        Limits.CollectErrors(errors);
        Grid.CollectErrors(errors);
        UserGate.CollectErrors(errors);
        return errors;
    }
}

/// <summary>Rota başı motor modu (ADR 007 §3). Validate Raw'ı, UserTemplate Grid'i izler.</summary>
public sealed class XsltEngineRoutingOptions
{
    public XsltEngineMode Raw { get; set; } = XsltEngineMode.Auto;

    /// <summary>Grid + UserTemplate. <c>DotNet</c> başlangıç ve 2.0.x boyunca acil anahtar.</summary>
    public XsltEngineMode Grid { get; set; } = XsltEngineMode.DotNet;
}

/// <summary>
/// Boyut/süre sınırları. Karakter sınırı ucuz ön denetimdir; UTF-8 bayt sınırı worker çerçeve
/// bütçesini (12 MB, <c>FrameLimits.MaxRequestFrameBytes</c>) garanti eder:
/// izole giden her işlemde XSLT+XML bayt sınırı toplamı ≤ <see cref="IsolatedMaxRequestBytes"/>.
/// </summary>
public sealed class XsltRouteLimitsOptions
{
    private const int MiB = 1024 * 1024;

    public XsltRouteLimits Raw { get; set; } = new()
    {
        MaxXsltChars = 2_000_000, MaxXsltBytes = 2 * MiB,
        MaxXmlChars = 5_000_000, MaxXmlBytes = 5 * MiB,
        TimeoutMs = 10_000,
    };

    /// <summary><c>validate-xslt</c>: yalnız XSLT.</summary>
    public XsltRouteLimits Validate { get; set; } = new()
    {
        MaxXsltChars = 1_000_000, MaxXsltBytes = 1 * MiB,
        MaxXmlChars = 0, MaxXmlBytes = 0,
        TimeoutMs = 5_000,
    };

    /// <summary>Kayıtlı şablon (generator çıktısı, worker'da).</summary>
    public XsltRouteLimits UserTemplate { get; set; } = new()
    {
        MaxXsltChars = 2_000_000, MaxXsltBytes = 2 * MiB,
        MaxXmlChars = 5_000_000, MaxXmlBytes = 5 * MiB,
        TimeoutMs = 8_000,
    };

    /// <summary>
    /// Grid (ADR 008 §7: girdi ≤ 5 MB / ≤ 6M karakter, derinlik 64, ≤ 200k düğüm). XSLT gömülü
    /// base64 görseller içerebildiği için daha geniştir.
    /// </summary>
    public XsltRouteLimits Grid { get; set; } = new()
    {
        MaxXsltChars = 6_000_000, MaxXsltBytes = 6 * MiB,
        MaxXmlChars = 6_000_000, MaxXmlBytes = 5 * MiB,
        TimeoutMs = 8_000,
        MaxInputDepth = 64, MaxInputNodes = 200_000,
    };

    /// <summary>Tüm rotalarda çıktı üst sınırı (karakter).</summary>
    public int MaxOutputChars { get; set; } = 8_000_000;

    /// <summary>Stylesheet eleman derinliği (XsltSafety ile aynı tavan).</summary>
    public int MaxStylesheetDepth { get; set; } = 256;

    /// <summary>İzole işlem gövde bütçesi; 12 MB çerçeveden başlık + çerçeve payı düşülmüş.</summary>
    public int IsolatedMaxRequestBytes { get; set; } = 11 * MiB;

    internal void CollectErrors(List<string> errors)
    {
        foreach (var (name, limits) in new[] { ("Raw", Raw), ("Validate", Validate), ("UserTemplate", UserTemplate), ("Grid", Grid) })
        {
            if (limits.MaxXsltChars < 1 || limits.MaxXsltBytes < 1)
                errors.Add($"Limits:{name} XSLT sınırları ≥ 1 olmalı.");
            if (limits.MaxXmlChars < 0 || limits.MaxXmlBytes < 0)
                errors.Add($"Limits:{name} XML sınırları negatif olamaz.");
            if (limits.TimeoutMs < 1)
                errors.Add($"Limits:{name}:TimeoutMs ≥ 1 olmalı.");
            if (limits.MaxInputDepth < 1 || limits.MaxInputNodes < 1)
                errors.Add($"Limits:{name} girdi derinlik/düğüm sınırları ≥ 1 olmalı.");

            // Grid de script'li ağaçta worker'a gider → bütçeye tabi.
            if ((long)limits.MaxXsltBytes + limits.MaxXmlBytes > IsolatedMaxRequestBytes)
                errors.Add($"Limits:{name} XSLT+XML bayt sınırı izole bütçeyi ({IsolatedMaxRequestBytes}) aşıyor.");
        }

        if (MaxOutputChars < 1 || MaxStylesheetDepth < 1)
            errors.Add("Limits:MaxOutputChars/MaxStylesheetDepth ≥ 1 olmalı.");
        if (IsolatedMaxRequestBytes < 1 || IsolatedMaxRequestBytes > 12 * MiB - 64 * 1024)
            errors.Add("Limits:IsolatedMaxRequestBytes 1 ile (12 MB − 64 KB başlık) arasında olmalı.");
    }
}

/// <summary>Tek rota/işlem için boyut ve süre sınırları. XML sınırı 0 → XML kabul edilmez (Validate).</summary>
public sealed class XsltRouteLimits
{
    public int MaxXsltChars { get; set; }

    public int MaxXsltBytes { get; set; }

    public int MaxXmlChars { get; set; }

    public int MaxXmlBytes { get; set; }

    /// <summary>Sert süre sınırı (worker'da kill, süreç içinde guard).</summary>
    public int TimeoutMs { get; set; }

    public int MaxInputDepth { get; set; } = 512;

    public int MaxInputNodes { get; set; } = 1_000_000;
}

/// <summary>
/// Grid süreç içi yürütme ayarları (ADR 008 §7). Tüketen: T2.4b <c>InProcessXsltHost</c> /
/// <c>XsltExecutionGuard</c>; burada yalnız bağlanır ve doğrulanır.
/// </summary>
public sealed class XsltGridOptions
{
    /// <summary>Süreç içi eşzamanlı grid dönüşümü (PROD VPS: 3, ölçümle gözden geçirilir).</summary>
    public int MaxConcurrent { get; set; } = 3;

    public int QueueWaitMs { get; set; } = 2_000;

    /// <summary>Kaçak thread bu sayıya ulaşınca grid worker'a yönlenir (bozulmuş mod).</summary>
    public int RunawayMax { get; set; } = 2;

    /// <summary>Takılı kaçakta Api'yi yeniden başlat (SAHİP KARARI: <c>false</c> — yalnız Critical log).</summary>
    public bool RestartOnStuckRunaway { get; set; }

    public int StuckRunawayMinutes { get; set; } = 15;

    internal void CollectErrors(List<string> errors)
    {
        if (MaxConcurrent < 1 || RunawayMax < 1 || StuckRunawayMinutes < 1 || QueueWaitMs < 0)
            errors.Add("Grid ayarları geçersiz (MaxConcurrent/RunawayMax/StuckRunawayMinutes ≥ 1, QueueWaitMs ≥ 0).");
    }
}

/// <summary>Kullanıcı başı kapı (Infrastructure <c>XsltUserGate</c>).</summary>
public sealed class XsltUserGateOptions
{
    public int RawMaxConcurrent { get; set; } = 1;

    public int ValidateMaxConcurrent { get; set; } = 1;

    /// <summary>ADR 008 açık soru 8 (1 mi 2 mi) — sahip netleştirene dek 1.</summary>
    public int UserTemplateMaxConcurrent { get; set; } = 1;

    public int GridMaxConcurrent { get; set; } = 1;

    /// <summary>İzole yürütmede pencere içinde bu kadar Timeout/EngineCrashed/MemoryLimit → blok.</summary>
    public int IsolatedAbuseThreshold { get; set; } = 3;

    /// <summary>Süreç içi yürütmede pencere içinde bu kadar Timeout → blok.</summary>
    public int InProcessAbuseThreshold { get; set; } = 2;

    public int AbuseWindowMinutes { get; set; } = 10;

    public int BlockMinutes { get; set; } = 10;

    public int MaxConcurrentFor(XsltGateLane lane) => lane switch
    {
        XsltGateLane.Raw => RawMaxConcurrent,
        XsltGateLane.Validate => ValidateMaxConcurrent,
        XsltGateLane.UserTemplate => UserTemplateMaxConcurrent,
        XsltGateLane.Grid => GridMaxConcurrent,
        _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, null),
    };

    internal void CollectErrors(List<string> errors)
    {
        if (RawMaxConcurrent < 1 || ValidateMaxConcurrent < 1 || UserTemplateMaxConcurrent < 1 || GridMaxConcurrent < 1)
            errors.Add("UserGate eşzamanlılık sınırları ≥ 1 olmalı.");
        if (IsolatedAbuseThreshold < 1 || InProcessAbuseThreshold < 1 || AbuseWindowMinutes < 1 || BlockMinutes < 1)
            errors.Add("UserGate kötüye kullanım ayarları ≥ 1 olmalı.");
    }
}
