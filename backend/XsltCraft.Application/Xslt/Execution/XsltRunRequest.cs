using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Application.Xslt.Execution;

/// <summary>
/// <see cref="IXsltExecutionService"/> girdisi.
/// </summary>
/// <param name="Route">İsteğin geldiği hat (<see cref="XsltRoute.Theme"/> bu servisten geçmez).</param>
/// <param name="Xslt">Stylesheet metni.</param>
/// <param name="Xml">Girdi XML'i; <see cref="XsltOperation.Validate"/> için yok sayılır.</param>
/// <param name="Op">İşlem.</param>
/// <param name="UserId">Kullanıcı kapısı bölümü. Uçlar <c>[Authorize]</c> olmalıdır (T3.1);
/// <see cref="Guid.Empty"/> tek bir ortak (anonim) bölüm sayılır.</param>
public sealed record XsltRunRequest(XsltRoute Route, string Xslt, string? Xml, XsltOperation Op, Guid UserId)
{
    /// <summary>
    /// Grid ağacı kullanıcının yazdığı kod (v3 Faz 4 Kademe 2/3 script blokları) içeriyor mu.
    /// <c>true</c> → grid de izole worker'da çalışır (ADR 007 §2). Bugün hiçbir çağıran
    /// ayarlamaz (kanca); yalnız <see cref="XsltRoute.Grid"/> için anlamlıdır.
    /// </summary>
    public bool IsUserAuthoredGrid { get; init; }
}
