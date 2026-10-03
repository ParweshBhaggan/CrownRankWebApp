using CrownRankApp.Application.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CrownRankApp.API.Controllers;

[ApiController]
[Route("api/payments")]
[EnableRateLimiting("payments")]
public sealed class PaymentController(CheckoutService checkout, PaymentSettings settings) : ControllerBase
{
    [HttpGet("settings")]
    public IActionResult Settings() => Ok(new
    {
        settings.Currency, settings.MinimumAmount, settings.MaximumAmount,
        settings.TermsVersion, settings.PrivacyVersion
    });

    [HttpPost("entry-checkout")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public Task<IActionResult> EntryCheckout(EntryCheckoutRequest request, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await checkout.StartEntryAsync(request, ct)));

    [HttpPost("boost-checkout")]
    public Task<IActionResult> BoostCheckout(BoostCheckoutRequest request, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await checkout.StartBoostAsync(request, ct)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Status(Guid id, CancellationToken ct) => ExecuteAsync(async () =>
        await checkout.StatusAsync(id, ct) is { } status ? Ok(status) : NotFound());

    [HttpPost("{id:guid}/confirm")]
    public Task<IActionResult> Confirm(Guid id, CancellationToken ct) => ExecuteAsync(async () =>
        await checkout.ConfirmAsync(id, ct) is { } status ? Ok(status) : NotFound());

    [HttpPost("{id:guid}/resume")]
    public Task<IActionResult> Resume(Guid id, CancellationToken ct) => ExecuteAsync(async () =>
        await checkout.ResumeAsync(id, ct) is { } session ? Ok(session) : NotFound());

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ArgumentException exception) { return Problem(detail: exception.Message, statusCode: 400); }
        catch (InvalidOperationException exception) { return Problem(detail: exception.Message, statusCode: 409); }
        catch (PaymentUnavailableException exception) { return Problem(detail: exception.Message, statusCode: 503); }
    }
}
