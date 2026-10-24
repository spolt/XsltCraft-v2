using XsltCraft.Application.Validation.Safety;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.XsltWorker.Contracts.Messages;

namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// Motor/tarayıcı sonuçlarını tel hatasına çevirir. Tür adı = <see cref="XsltFailureKind"/> üye adı
/// (<see cref="WorkerFailureKinds"/> ile birebir); konum bilinmiyorsa (0) alan gönderilmez.
/// Mesajlar motorların temizlenmiş mesajlarıdır (Saxon: sınıf adı/yol/URI yok); beklenmeyen hatalar jenerik.
/// </summary>
internal static class ResponseFailures
{
    public static ResponseFailure FromDiagnostic(XsltDiagnostic diagnostic) => new()
    {
        Kind = diagnostic.Kind.ToString(),
        ErrorCode = diagnostic.ErrorCode,
        Message = diagnostic.Message,
        Line = Position(diagnostic.Line),
        Column = Position(diagnostic.Column),
    };

    /// <summary>Derinlemesine savunma taraması bulgusu (motora ulaşmadan red).</summary>
    public static ResponseFailure FromFinding(XsltSafetyFinding finding) => new()
    {
        Kind = finding.Severity == XsltSafetySeverity.Unsupported ? WorkerFailureKinds.Unsupported : WorkerFailureKinds.Security,
        ErrorCode = finding.Code,
        Message = finding.Message,
        Line = Position(finding.Line),
        Column = Position(finding.Column),
    };

    public static ResponseFailure Of(string kind, string errorCode, string message) =>
        new() { Kind = kind, ErrorCode = errorCode, Message = message };

    private static int? Position(int value) => value > 0 ? value : null;
}
