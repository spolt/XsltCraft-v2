namespace XsltCraft.Application.Validation.Safety;

/// <summary>Bir bulgunun ağırlığı. Mod, hangi ağırlığın engel olduğunu belirler.</summary>
public enum XsltSafetySeverity
{
    /// <summary>Güvenlik tehdidi (SSRF, dosya okuma, ortam sızıntısı, DoS vektörü). Her modda engel.</summary>
    Security,

    /// <summary>Desteklenmeyen (XSLT 2.0 üstü) yapı. Yalnız <see cref="XsltSafetyMode.Execute"/> modunda engel.</summary>
    Unsupported,
}

/// <summary>
/// Tek bir tarama bulgusu. <paramref name="Line"/>/<paramref name="Column"/> kaynaktaki konumdur
/// (<c>IXmlLineInfo</c>); konum bilinmiyorsa 0.
/// </summary>
public sealed record XsltSafetyFinding(
    string Code,
    XsltSafetySeverity Severity,
    string Message,
    int Line,
    int Column);

/// <summary>
/// <see cref="XsltSafetyScanner.Scan"/> sonucu. <see cref="Findings"/> yalnız istenen modda
/// <b>engel</b> olan bulguları içerir (Store → sadece Security).
/// </summary>
public sealed class XsltSafetyReport
{
    public static readonly XsltSafetyReport Clean = new([]);

    public XsltSafetyReport(IReadOnlyList<XsltSafetyFinding> findings)
    {
        Findings = findings;
    }

    public IReadOnlyList<XsltSafetyFinding> Findings { get; }

    public bool HasFindings => Findings.Count > 0;

    /// <summary>İlk bulgunun açıklayıcı Türkçe mesajı; bulgu yoksa <c>null</c>.</summary>
    public string? FirstMessage => Findings.Count > 0 ? Findings[0].Message : null;
}
