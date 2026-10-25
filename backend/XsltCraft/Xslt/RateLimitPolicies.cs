using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;

namespace XsltCraft.Api.Xslt;

/// <summary>XSLT önizleme/doğrulama uçlarının hız sınırı politikaları (ADR 007 §7).</summary>
public static class RateLimitPolicies
{
    /// <summary>Preview, PreviewRaw, PreviewUserTemplate: 120 istek/dk/kullanıcı.</summary>
    public const string XsltPreview = "xslt-preview";

    /// <summary>ValidateXslt: 120 istek/dk/kullanıcı.</summary>
    public const string XsltValidate = "xslt-validate";

    /// <summary>PreviewTheme (anonim olabilir): 30 istek/dk/IP (G3).</summary>
    public const string ThemePreview = "theme-preview";

    private const int PermitPerMinute = 120;
    private const int ThemePreviewPerMinute = 30;

    /// <summary>Kullanıcı bölümlü (kimliksizse IP) sabit pencere politikalarını ekler.</summary>
    public static RateLimiterOptions AddXsltPolicies(this RateLimiterOptions options)
    {
        options.AddPolicy(XsltPreview, PerUserPerMinute);
        options.AddPolicy(XsltValidate, PerUserPerMinute);
        options.AddPolicy(ThemePreview, httpContext => PerIp(httpContext, ThemePreviewPerMinute, TimeSpan.FromMinutes(1)));
        return options;
    }

    /// <summary>
    /// 429 yanıtı: <c>Retry-After</c> (pencere metadata'sından) ve XSLT uçlarında editörlerin okuduğu
    /// hata gövdesi <c>{ error, line, column, kind, engine }</c>.
    /// </summary>
    public static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        if (context.HttpContext.Request.Path.StartsWithSegments("/api/preview"))
        {
            await response.WriteAsJsonAsync(new
            {
                error = "Çok fazla önizleme isteği gönderildi, biraz bekleyip tekrar deneyin.",
                line = 0,
                column = 0,
                kind = "RateLimited",
                engine = (string?)null,
            }, cancellationToken);
        }
    }

    /// <summary>
    /// İstemci IP'si bölümlü sabit pencere (kimlikten bağımsız). Not: <c>RemoteIpAddress</c> ters vekil arkasında
    /// vekilin adresidir — <c>ForwardedHeaders</c> yapılandırılmadan nginx arkasına alınırsa tek kovaya düşer.
    /// </summary>
    public static RateLimitPartition<string> PerIp(HttpContext httpContext, int permitLimit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });

    private static RateLimitPartition<string> PerUserPerMinute(HttpContext httpContext) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });
}
