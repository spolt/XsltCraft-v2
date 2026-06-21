using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using XsltCraft.Application.Interfaces;

namespace XsltCraft.Controllers;

/// <summary>
/// Admin kullanım raporu: kullanıcı başına token/AI/indirme/kaydetme — geçmişe dönük ve anlık.
/// Tarih aralığı verilmezse son 30 gün (bugün dahil) varsayılır.
/// </summary>
[ApiController]
[Route("api/admin/usage")]
[Authorize(Roles = "Admin")]
public class AdminUsageController(IUsageReportService report) : ControllerBase
{
    [HttpGet("report")]
    public async Task<IActionResult> GetReport([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var (fromDate, toDate) = ResolveRange(from, to);
        var result = await report.GetReportAsync(fromDate, toDate, ct);
        return Ok(result);
    }

    [HttpGet("daily")]
    public async Task<IActionResult> GetDaily([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var (fromDate, toDate) = ResolveRange(from, to);
        var points = await report.GetDailyTotalsAsync(fromDate, toDate, ct);
        return Ok(new { from = fromDate, to = toDate, points });
    }

    private static (DateOnly From, DateOnly To) ResolveRange(string? from, string? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var toDate = to is not null && DateOnly.TryParse(to, out var t) ? t : today;
        var fromDate = from is not null && DateOnly.TryParse(from, out var f) ? f : toDate.AddDays(-29);
        if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);
        return (fromDate, toDate);
    }
}
