using CrownRank.Application.Abstractions;
using CrownRank.Application.Creators;

namespace CrownRank.Api.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder endpoints, bool stripeEnabled)
    {
        endpoints.MapPost("/api/payments/checkout", async Task<IResult> (CheckoutRequest request, PaymentCheckoutService service, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await service.CheckoutAsync(request, cancellationToken)); }
            catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [ex.Message] }); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        }).WithTags("Payments").WithName("CreateCheckout").WithDescription("Starts a checkout using the configured development payment provider.");
        endpoints.MapGet("/api/payments/status/{referenceId:guid}", async Task<IResult> (
            Guid referenceId,
            Guid creatorId,
            string purpose,
            PaymentStatusService service,
            CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await service.GetAsync(referenceId, creatorId, purpose, cancellationToken)); }
            catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [ex.Message] }); }
        }).WithTags("Payments").WithName("GetPaymentStatus");

        if (stripeEnabled)
        {
            endpoints.MapPost("/api/payments/webhook", async Task<IResult> (
                HttpRequest request,
                IPaymentWebhookVerifier verifier,
                PaymentConfirmationService confirmations,
                CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(request.Body);
                var payload = await reader.ReadToEndAsync(cancellationToken);
                var signature = request.Headers["Stripe-Signature"].ToString();
                try
                {
                    var confirmation = verifier.Verify(payload, signature);
                    if (confirmation is not null)
                        await confirmations.ConfirmAsync(confirmation, cancellationToken);
                    return Results.Ok();
                }
                catch (PaymentWebhookException ex) { return Results.BadRequest(new { error = ex.Message }); }
                catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
                catch (KeyNotFoundException) { return Results.NotFound(); }
                catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
            }).WithTags("Payments").WithName("StripeWebhook");
        }
        return endpoints;
    }
}
