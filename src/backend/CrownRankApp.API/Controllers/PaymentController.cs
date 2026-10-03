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
    public IActionResult Settings()
    {
        return Ok(new
            {
                settings.Currency,
                settings.MinimumAmount,
                settings.MaximumAmount,
                settings.TermsVersion,
                settings.PrivacyVersion
            });
    }

    [HttpPost("entry-checkout/{referenceId:guid}")]
    public Task<IActionResult> EntryCheckout(Guid referenceId, CheckoutRequest request, CancellationToken ct)
    {
        return ExecuteAsync(checkout.StartEntryAsync(referenceId, request, ct));
    }

    [HttpPost("entries/{entryId:guid}/boost-checkout/{referenceId:guid}")]
    public Task<IActionResult> BoostCheckout(Guid entryId, Guid referenceId, CheckoutRequest request, CancellationToken ct)
    {
        return ExecuteAsync(checkout.StartBoostCheckoutAsync(entryId, referenceId, request, ct));
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Status(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(checkout.StatusAsync(id, ct));
    }

    [HttpPost("{id:guid}/confirm")]
    public Task<IActionResult> Confirm(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(checkout.ConfirmAsync(id, ct));
    }

    [HttpPost("{id:guid}/resume")]
    public Task<IActionResult> Resume(Guid id, CancellationToken ct)
    {
        return ExecuteAsync(checkout.ResumeAsync(id, ct));
    }

    private async Task<IActionResult> ExecuteAsync<T>(Task<T> action)
    {
        try
        {
            var result = await action;
            if (result is null)
            {
                return NotFound();
            }
            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Problem(detail: exception.Message, statusCode: 400);
        }
        catch (InvalidOperationException exception)
        {
            return Problem(detail: exception.Message, statusCode: 409);
        }
        catch (PaymentUnavailableException exception)
        {
            return Problem(detail: exception.Message, statusCode: 503);
        }
    }
}
