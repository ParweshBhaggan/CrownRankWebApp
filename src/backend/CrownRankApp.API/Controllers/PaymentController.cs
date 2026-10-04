using CrownRankApp.Application.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CrownRankApp.API.Controllers;

[ApiController]
[Route("api/payments")]
[PaymentErrorFilter]
[EnableRateLimiting("payments")]
public sealed class PaymentController(IPaymentService payments, PaymentSettings settings) : ControllerBase
{
    [HttpGet("settings")]
    public IActionResult Settings()
    {
        return Ok(settings);
    }

    [HttpPost("entry-checkout/{referenceId:guid}")]
    public async Task<IActionResult> EntryCheckout(Guid referenceId, CheckoutRequest request, CancellationToken ct)
    {
        return Ok(await payments.StartAsync(referenceId, request, null, ct));
    }

    [HttpPost("entries/{entryId:guid}/boost-checkout/{referenceId:guid}")]
    public async Task<IActionResult> BoostCheckout(Guid entryId, Guid referenceId, CheckoutRequest request, CancellationToken ct)
    {
        return Ok(await payments.StartAsync(referenceId, request, entryId, ct));
    }

    [HttpPost("{referenceId:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid referenceId, CancellationToken ct)
    {
        return Ok(await payments.ConfirmAsync(referenceId, ct));
    }

    [HttpPost("{referenceId:guid}/resume")]
    public async Task<IActionResult> Resume(Guid referenceId, CancellationToken ct)
    {
        return Ok(await payments.ResumeAsync(referenceId, ct));
    }
}
