using CrownRankApp.Application.Payments;
using Microsoft.AspNetCore.Mvc;

namespace CrownRankApp.API.Controllers;

[ApiController]
[Route("api/payments/stripe-webhook")]
[PaymentErrorFilter]
public sealed class StripeWebhookController(IPaymentService payments) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        await payments.HandleWebhookAsync(payload, Request.Headers["Stripe-Signature"].ToString(), ct);
        return Ok();
    }
}
