using net.sf.saxon.s9api;
using net.sf.saxon.trans;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon.Limits;
using XsltCraft.Xslt.Saxon.Sandbox;

namespace XsltCraft.Xslt.Saxon.Diagnostics;

/// <summary>
/// Saxon / IKVM hatalarını motor-bağımsız <see cref="XsltDiagnostic"/>'e çevirir.
/// Öncelik: sınır ihlali (bütçe kaydı veya cause zinciri) → sandbox ihlali (cause zinciri / mesaj öneki)
/// → Saxon hata kodu → beklenmeyen <c>Throwable</c> = <see cref="XsltFailureKind.EngineInternal"/>.
/// Mesajlar <see cref="SaxonMessageSanitizer"/> ile temizlenir (Java sınıf adı, URI, yol yok).
/// </summary>
internal static class SaxonDiagnostics
{
    public const string XPath3SyntaxMessage = "XPath 3.0 sözdizimi desteklenmiyor (XSLT 2.0 hedeflenir).";
    private const string Engine = "Saxon";
    private const int MaxChainDepth = 32;

    /// <summary>Derleme hatası: ilk kayıtlı hata (konumlu) esas alınır; yoksa istisnanın kendisi.</summary>
    public static XsltDiagnostic FromCompile(Exception ex, IReadOnlyList<SaxonErrorRecord> errors)
    {
        if (FindLimit(ex) is { } limit)
            return FromLimit(limit);

        // Kayıtlardan herhangi birinin nedeni sandbox ise (import/include/collation) Security.
        foreach (var e in errors)
        {
            if (FindSandbox(e.Cause) is { } sv)
                return Sandbox(sv.Code, sv.UserMessage, e.Line, e.Column);
            if (FromMarker(e.Message) is { } marked)
                return marked with { Line = e.Line, Column = e.Column };
        }

        if (FindSandbox(ex) is { } direct)
            return Sandbox(direct.Code, direct.UserMessage, 0, 0);

        if (errors.Count > 0)
        {
            var first = errors[0];
            return Classify(first.Code, first.Message, first.Line, first.Column, XsltFailureKind.Compile);
        }

        return FromUnexpected(ex, XsltFailureKind.Compile);
    }

    /// <summary>Çalışma hatası.</summary>
    public static XsltDiagnostic FromRuntime(Exception ex, SaxonLimitError? tripped)
    {
        // Bütçe kaydı en güvenilir kaynaktır: Saxon hatayı sarsa / mesajı değiştirse bile tür korunur.
        if ((tripped ?? FindLimit(ex)) is { } limit)
            return FromLimit(limit);

        if (FindSandbox(ex) is { } sv)
            return Sandbox(sv.Code, sv.UserMessage, LineOf(ex), ColumnOf(ex));

        if (FromMarker(MessageOf(ex)) is { } marked)
            return marked with { Line = LineOf(ex), Column = ColumnOf(ex) };

        if (ex is SaxonApiException or XPathException or UncheckedXPathException)
        {
            return Classify(CodeOf(ex), MessageOf(ex), LineOf(ex), ColumnOf(ex), XsltFailureKind.Runtime);
        }

        return FromUnexpected(ex, XsltFailureKind.Runtime);
    }

    public static XsltDiagnostic FromLimit(SaxonLimitError limit) =>
        new(limit.UserMessage, 0, 0, limit.Kind, XsltEngineKind.Saxon, limit.Code);

    /// <summary>Beklenmeyen Java/IKVM/.NET hatası (NoSuchMethodError, NPE …) — ayrıntı sızdırılmaz.</summary>
    public static XsltDiagnostic FromUnexpected(Exception ex, XsltFailureKind phase) =>
        new($"{Engine} motorunda beklenmeyen iç hata ({(phase == XsltFailureKind.Compile ? "derleme" : "dönüşüm")}).",
            0, 0, XsltFailureKind.EngineInternal, XsltEngineKind.Saxon, ex is java.lang.Throwable ? "JavaThrowable" : "DotNetException");

    private static XsltDiagnostic Classify(string? code, string message, int line, int column, XsltFailureKind fallback)
    {
        if (code == "XPST0003" && IsXPath3Syntax(message))
            return new XsltDiagnostic(XPath3SyntaxMessage, line, column, XsltFailureKind.Unsupported, XsltEngineKind.Saxon, code);

        if (code == "FORX0006" || message.Contains("backtracking", StringComparison.OrdinalIgnoreCase))
        {
            return new XsltDiagnostic("Düzenli ifade çok karmaşık (geri izleme sınırı aşıldı).", line, column,
                XsltFailureKind.LimitExceeded, XsltEngineKind.Saxon, LimitCodes.Regex);
        }

        if (IsSaxonPolicyRejection(message))
        {
            return new XsltDiagnostic(SaxonMessageSanitizer.Clean(message), line, column,
                XsltFailureKind.Security, XsltEngineKind.Saxon, SandboxCodes.SaxonPolicy);
        }

        var text = SaxonMessageSanitizer.Clean(message);
        if (!string.IsNullOrEmpty(code) && !text.StartsWith(code, StringComparison.Ordinal))
            text = code + ": " + text;
        return new XsltDiagnostic(text, line, column, fallback, XsltEngineKind.Saxon, code);
    }

    // Saxon 12: "... requires XPath 3.0/3.1 ..." / "... not available in XPath 2.0 ..." / "XPath 3.0 ... is not enabled".
    private static bool IsXPath3Syntax(string message) =>
        message.Contains("XPath 3", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("XPath 4", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("XPath 2.0", StringComparison.OrdinalIgnoreCase);

    // Saxon'un kendi sandbox kararları (ALLOWED_PROTOCOLS, dinamik değerlendirme, extension fonksiyonları).
    private static bool IsSaxonPolicyRejection(string message) =>
        message.Contains("has been disallowed", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("has been prohibited", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("is not allowed", StringComparison.OrdinalIgnoreCase) && message.Contains("protocol", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("xsl:evaluate", StringComparison.OrdinalIgnoreCase) && message.Contains("disabled", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("extension functions are disabled", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Dynamic evaluation", StringComparison.OrdinalIgnoreCase) && message.Contains("disabled", StringComparison.OrdinalIgnoreCase);

    private static XsltDiagnostic Sandbox(string code, string message, int line, int column) =>
        new(message, line, column, XsltFailureKind.Security, XsltEngineKind.Saxon, code);

    private static XsltDiagnostic? FromMarker(string? message)
    {
        if (message is null)
            return null;
        var at = message.IndexOf(SandboxViolationException.Marker, StringComparison.Ordinal);
        if (at < 0)
            return null;

        var codeStart = at + SandboxViolationException.Marker.Length;
        var end = message.IndexOf(']', codeStart);
        if (end < 0)
            return Sandbox(SandboxCodes.ResourceDenied, "Sandbox ihlali.", 0, 0);

        var code = message[codeStart..end];
        var rest = message[(end + 1)..];
        var cut = rest.IndexOfAny(['\r', '\n']);
        var text = SaxonMessageSanitizer.Clean(cut >= 0 ? rest[..cut] : rest);
        return Sandbox(code, text, 0, 0);
    }

    // ---- zincir gezme ----------------------------------------------------------------------------

    internal static IEnumerable<Exception> Chain(Exception? ex)
    {
        var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        for (var cur = ex; cur is not null && seen.Count < MaxChainDepth && seen.Add(cur); cur = Next(cur))
            yield return cur;
    }

    private static Exception? Next(Exception ex)
    {
        if (ex is java.lang.Throwable t && t.getCause() is Exception cause && !ReferenceEquals(cause, ex))
            return cause;
        return ex.InnerException;
    }

    internal static SaxonLimitError? FindLimit(Exception? ex) => Chain(ex).OfType<SaxonLimitError>().FirstOrDefault();

    internal static SandboxViolationException? FindSandbox(Exception? ex) =>
        Chain(ex).OfType<SandboxViolationException>().FirstOrDefault();

    private static string MessageOf(Exception ex) => ex is java.lang.Throwable t ? t.getMessage() ?? string.Empty : ex.Message;

    private static string? CodeOf(Exception ex)
    {
        foreach (var e in Chain(ex))
        {
            var code = e switch
            {
                SaxonApiException s => s.getErrorCode()?.getLocalName(),
                XPathException x => x.getErrorCodeQName()?.getLocalPart(),
                _ => null,
            };
            if (!string.IsNullOrEmpty(code))
                return code;
        }

        return null;
    }

    private static int LineOf(Exception ex)
    {
        foreach (var e in Chain(ex))
        {
            var line = e switch
            {
                SaxonApiException s => s.getLineNumber(),
                XPathException x => x.getLocator()?.getLineNumber() ?? -1,
                _ => -1,
            };
            if (line > 0)
                return line;
        }

        return 0;
    }

    private static int ColumnOf(Exception ex)
    {
        foreach (var e in Chain(ex))
        {
            if (e is XPathException x && x.getLocator()?.getColumnNumber() is > 0 and var col)
                return col;
        }

        return 0;
    }
}
