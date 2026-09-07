using CrownRank.Application.Abstractions;

namespace CrownRank.Api.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/payments/checkout", async (CheckoutApiRequest request, IPaymentGateway gateway, CancellationToken cancellationToken) =>
        {
            if (request.AmountCents < 100) return Results.ValidationProblem(new Dictionary<string, string[]> { ["amountCents"] = ["Minimum amount is 100 cents."] });
            var session = await gateway.CreateCheckoutAsync(new(request.CreatorId, request.AmountCents, "USD", request.Purpose), cancellationToken);
            return Results.Ok(session);
        }).WithTags("Payments").WithName("CreateCheckout").WithDescription("Uses a development adapter today; replace it with Stripe without changing this API contract.");
        return endpoints;
    }
}
public sealed record CheckoutApiRequest(Guid CreatorId, long AmountCents, string Purpose);
