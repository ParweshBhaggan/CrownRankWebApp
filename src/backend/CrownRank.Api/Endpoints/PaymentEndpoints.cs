using CrownRank.Api.Contracts;
using CrownRank.Api.Domain;

namespace CrownRank.Api.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;
        app.MapPost("/api/payments/checkout", async (CheckoutRequest request, CreatorService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.BoostAsync(request, cancellationToken)));
    }
}
