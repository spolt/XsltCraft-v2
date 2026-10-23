namespace XsltCraft.Application.Xslt.Engines;

/// <summary>
/// Faz süreleri (ms) — <see cref="XsltRenderResult"/> ile aynı alanlar, HTML'siz.
/// </summary>
public sealed record XsltTransformTimings(long ParseMs, long CompileMs, long TransformMs, long SerializeMs)
{
    public long TotalMs => ParseMs + CompileMs + TransformMs + SerializeMs;
}
