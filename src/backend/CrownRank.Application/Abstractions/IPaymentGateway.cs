namespace CrownRank.Application.Abstractions;

public sealed record CheckoutRequest(Guid CreatorId, long AmountCents, string Currency, string Purpose);
public sealed record CheckoutSession(string Id, Uri CheckoutUrl);

public interface IPaymentGateway
{
    Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken);
}
