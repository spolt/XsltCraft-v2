using Microsoft.AspNetCore.Mvc;

using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;

namespace XsltCraft.Api.Xslt;

/// <summary>
/// <see cref="XsltRunResult"/> → HTTP sözleşmesi (ADR 007 §7, 2.0.0).
/// <list type="bullet">
/// <item>Başarı: <c>200 { html, generationTimeMs, timings, engine, cacheHit }</c>.</item>
/// <item>Hata: <c>{ error, line, column, kind, engine }</c> — alan adları <c>error/line/column</c> korunur
///   (AI teşhisi ve editörler okur). İçerik hataları 400; <c>Busy</c>/<c>EngineUnavailable</c> 503 + <c>Retry-After</c>;
///   <c>UserBusy</c> 429; <c>EngineInternal</c> 500 (jenerik mesaj).</item>
/// <item>Doğrulama: içerik hatası <c>200 { valid:false, ... }</c>, operasyonel hata yukarıdaki durum kodlarıyla.</item>
/// </list>
/// </summary>
public static class XsltProblemResults
{
    public const string GenericServerError = "Sunucu hatası oluştu.";

    /// <summary>Retry-After (sn) — kuyruk beklemesi kısa (2 sn), motor yokluğu biraz daha uzun.</summary>
    private const int BusyRetrySeconds = 2;
    private const int UnavailableRetrySeconds = 5;

    /// <summary>Hata türünün HTTP durum kodu.</summary>
    public static int StatusFor(XsltFailureKind kind) => kind switch
    {
        XsltFailureKind.Busy or XsltFailureKind.EngineUnavailable => StatusCodes.Status503ServiceUnavailable,
        XsltFailureKind.UserBusy => StatusCodes.Status429TooManyRequests,
        XsltFailureKind.EngineInternal => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status400BadRequest,
    };

    /// <summary>Kullanıcının girdisinden kaynaklanan hata mı (validate-xslt'te <c>200 {valid:false}</c>).</summary>
    public static bool IsContentError(XsltFailureKind kind) => StatusFor(kind) == StatusCodes.Status400BadRequest;

    /// <summary>Kullanıcıya dönen Türkçe mesaj: içerik hatalarında motor teşhisi, operasyonel hatalarda sabit metin.</summary>
    public static string MessageFor(XsltDiagnostic diagnostic) => diagnostic.Kind switch
    {
        XsltFailureKind.Timeout => "Dönüşüm zaman sınırını aştı. Sonsuz döngü veya çok ağır bir ifade olabilir.",
        XsltFailureKind.MemoryLimit => "Dönüşüm bellek sınırını aştı.",
        XsltFailureKind.EngineCrashed => "XSLT motoru bu dönüşümde beklenmedik şekilde sonlandı (ör. çok derin özyineleme).",
        XsltFailureKind.Busy => "Önizleme motoru meşgul, birazdan tekrar deneyin.",
        XsltFailureKind.EngineUnavailable when diagnostic.ErrorCode == "WorkerDisabled" => diagnostic.Message,
        XsltFailureKind.EngineUnavailable => "XSLT motoru şu an kullanılamıyor, birazdan tekrar deneyin.",
        XsltFailureKind.EngineInternal => GenericServerError,
        XsltFailureKind.UserBusy when string.IsNullOrWhiteSpace(diagnostic.Message) => "Önceki önizleme isteğiniz sürüyor, birazdan tekrar deneyin.",
        XsltFailureKind.OutputLimit when string.IsNullOrWhiteSpace(diagnostic.Message) => "Dönüşüm çıktısı boyut sınırını aştı.",
        _ => string.IsNullOrWhiteSpace(diagnostic.Message) ? "XSLT dönüşümü başarısız oldu." : diagnostic.Message,
    };

    /// <summary>Render sonucu (önizleme uçları).</summary>
    public static IActionResult Render(XsltRunResult result, long elapsedMs, HttpResponse response)
    {
        if (result.Diagnostic is { } diagnostic)
            return Failure(diagnostic, result, response);

        return new OkObjectResult(new
        {
            html = result.Html ?? "",
            generationTimeMs = elapsedMs,
            timings = result.Timings is { } t
                ? new { parseMs = t.ParseMs, compileMs = t.CompileMs, transformMs = t.TransformMs, serializeMs = t.SerializeMs }
                : null,
            engine = EngineName(result.Engine),
            cacheHit = result.CacheHit,
        });
    }

    /// <summary>Doğrulama sonucu (<c>validate-xslt</c>).</summary>
    public static IActionResult Validate(XsltRunResult result, HttpResponse response)
    {
        if (result.Diagnostic is not { } diagnostic)
            return new OkObjectResult(new { valid = true, engine = EngineName(result.Engine) });

        if (!IsContentError(diagnostic.Kind))
            return Failure(diagnostic, result, response);

        return new OkObjectResult(new
        {
            valid = false,
            error = MessageFor(diagnostic),
            line = diagnostic.Line,
            column = diagnostic.Column,
            kind = diagnostic.Kind.ToString(),
            code = diagnostic.ErrorCode,
            engine = EngineName(diagnostic.Engine ?? result.Engine),
        });
    }

    /// <summary>Beklenmeyen istisna → 500, iç ayrıntı yok (çağıran loglar).</summary>
    public static IActionResult Unexpected() =>
        new ObjectResult(Body(GenericServerError, 0, 0, XsltFailureKind.EngineInternal, null, null))
        {
            StatusCode = StatusCodes.Status500InternalServerError,
        };

    private static ObjectResult Failure(XsltDiagnostic diagnostic, XsltRunResult result, HttpResponse response)
    {
        var status = StatusFor(diagnostic.Kind);
        if (RetryAfterSeconds(diagnostic.Kind, result.RetryAfter) is { } seconds)
            response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // 500'de motor iç satır/kolonu da dönmez.
        var internalError = status == StatusCodes.Status500InternalServerError;
        return new ObjectResult(Body(
            MessageFor(diagnostic),
            internalError ? 0 : diagnostic.Line,
            internalError ? 0 : diagnostic.Column,
            diagnostic.Kind,
            diagnostic.Engine ?? result.Engine,
            internalError ? null : diagnostic.ErrorCode))
        {
            StatusCode = status,
        };
    }

    private static int? RetryAfterSeconds(XsltFailureKind kind, TimeSpan? retryAfter) => kind switch
    {
        XsltFailureKind.Busy => Seconds(retryAfter) ?? BusyRetrySeconds,
        XsltFailureKind.EngineUnavailable => Seconds(retryAfter) ?? UnavailableRetrySeconds,
        XsltFailureKind.UserBusy => Seconds(retryAfter),
        _ => null,
    };

    private static int? Seconds(TimeSpan? value) =>
        value is { } v && v > TimeSpan.Zero ? (int)Math.Ceiling(v.TotalSeconds) : null;

    private static object Body(string error, int line, int column, XsltFailureKind kind, XsltEngineKind? engine, string? code) =>
        new { error, line, column, kind = kind.ToString(), code, engine = EngineName(engine) };

    private static string? EngineName(XsltEngineKind? engine) => engine?.ToString();
}
