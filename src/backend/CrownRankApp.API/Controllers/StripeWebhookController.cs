using CrownRankApp.Application.Payments;
using Microsoft.AspNetCore.Mvc;

namespace CrownRankApp.API.Controllers;

[ApiController]
[Route("api/payments/stripe-webhook")]
public sealed class StripeWebhookController(CheckoutService checkout, ILogger<StripeWebhookController> logger) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        try
        {
            await checkout.WebhookAsync(payload, Request.Headers["Stripe-Signature"].ToString(), ct);
            return Ok();
        }
        catch (InvalidWebhookException) { return BadRequest(); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Stripe webhook could not be processed; Stripe should retry");
            return StatusCode(503);
        }
    }
}
