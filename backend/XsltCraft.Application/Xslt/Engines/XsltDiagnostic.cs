namespace XsltCraft.Application.Xslt.Engines;

/// <summary>
/// Motor-bağımsız XSLT hata tanımı. <see cref="Line"/>/<see cref="Column"/> bilinmiyorsa 0'dır.
/// </summary>
public sealed record XsltDiagnostic(
    string Message,
    int Line,
    int Column,
    XsltFailureKind Kind,
    XsltEngineKind? Engine = null,
    string? ErrorCode = null);
