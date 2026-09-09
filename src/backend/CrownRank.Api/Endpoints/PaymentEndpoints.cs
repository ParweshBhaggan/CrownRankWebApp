using CrownRank.Application.Abstractions;
using CrownRank.Application.Creators;

namespace CrownRank.Api.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/payments/checkout", async Task<IResult> (CheckoutRequest request, MockCheckoutService service, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await service.CheckoutAsync(request, cancellationToken)); }
            catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [ex.Message] }); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        }).WithTags("Payments").WithName("CreateMockCheckout").WithDescription("Development only: confirms a simulated payment. No money is charged.");
        return endpoints;
    }
}
