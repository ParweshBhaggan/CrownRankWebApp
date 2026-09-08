using CrownRank.Application.Abstractions;

namespace CrownRank.Infrastructure.Payments;

public sealed class DevelopmentPaymentGateway : IPaymentGateway
{
    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var id = $"dev_{Guid.NewGuid():N}";
        return Task.FromResult(new CheckoutSession(id, new Uri($"http://localhost:5173/checkout/simulated?session={id}")));
    }
}
