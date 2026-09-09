using CrownRank.Application.Abstractions;

namespace CrownRank.Infrastructure.Payments;

public sealed class DevelopmentPaymentGateway : IPaymentGateway
{
    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CheckoutSession($"mock-{request.ReferenceId:N}", true));
    }
}
