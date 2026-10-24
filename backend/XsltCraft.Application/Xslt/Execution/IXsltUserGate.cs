using System.Diagnostics.CodeAnalysis;
using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Application.Xslt.Execution;

/// <summary>Kullanıcı başı eşzamanlılık şeridi; her şeridin kendi aynı-anda sınırı vardır.</summary>
public enum XsltGateLane
{
    /// <summary>Ham XSLT önizleme (<c>/api/preview/raw</c>).</summary>
    Raw,

    /// <summary>Ham XSLT doğrulama (<c>validate-xslt</c>) — editör önizlemeyle paralel çağırır, ayrı şerit.</summary>
    Validate,

    /// <summary>Kayıtlı kullanıcı şablonu önizlemesi.</summary>
    UserTemplate,

    /// <summary>Grid canvas önizlemesi.</summary>
    Grid,
}

/// <summary>Kapının reddi. <see cref="RetryAfter"/> yalnız kötüye kullanım bloğunda doludur.</summary>
public sealed record XsltGateDenial(string Message, string ErrorCode, TimeSpan? RetryAfter);

/// <summary>
/// Alınmış kapı izni. Yürütme bitince <see cref="ReportOutcome"/> (bir kez) ve ardından
/// <see cref="IDisposable.Dispose"/> çağrılır; Dispose izni bırakır ve idempotenttir.
/// </summary>
public interface IXsltGateLease : IDisposable
{
    /// <summary>Sonucu bildirir; kötüye kullanım sayacı yalnız sınır öldürmelerini sayar.</summary>
    void ReportOutcome(XsltFailureKind? failure);
}

/// <summary>
/// Kullanıcı başı kapı (ADR 008 §4, §7): şerit başı eşzamanlılık + kötüye kullanım kesicisi.
/// Kesici yürütme yerine göre ayrıdır: <see cref="XsltExecutionPlace.Isolated"/> (Timeout/EngineCrashed/MemoryLimit)
/// ve <see cref="XsltExecutionPlace.InProcess"/> (Timeout). Global worker kesicisinin kullanıcı kaynaklı
/// çökmelerle açılmasını sınırlayan katman budur (G1).
/// </summary>
public interface IXsltUserGate
{
    bool TryAcquire(
        Guid userId,
        XsltGateLane lane,
        XsltExecutionPlace place,
        [NotNullWhen(true)] out IXsltGateLease? lease,
        [NotNullWhen(false)] out XsltGateDenial? denial);
}
