using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace XsltCraft.Controllers;

/// <summary>
/// Pro üyelik satın alma akışının uç noktası. Faz 1'de STUB'dır: gerçek tahsilat (iyzico/PayTR) yoktur.
/// Faz 2'de checkout başlatma + webhook ile abonelik aktivasyonu buraya eklenecek.
/// Şimdilik Pro, admin panelinden elle (comp/grant) tanımlanır.
/// </summary>
[ApiController]
[Route("api/billing")]
[Authorize]
public class BillingController : ControllerBase
{
    [HttpPost("checkout")]
    public IActionResult Checkout([FromBody] CheckoutRequest? request)
    {
        return Ok(new
        {
            available = false,
            plan = string.IsNullOrWhiteSpace(request?.Plan) ? "Pro" : request!.Plan,
            message = "Online ödeme yakında. Pro üyelik için lütfen yöneticiyle iletişime geçin; size tanımlansın."
        });
    }
}

public record CheckoutRequest(string? Plan);
