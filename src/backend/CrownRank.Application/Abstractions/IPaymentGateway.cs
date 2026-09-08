namespace CrownRank.Application.Abstractions;

public sealed record CheckoutRequest(Guid CreatorId, decimal Amount, string Currency, string Purpose, Guid ReferenceId);
public sealed record CheckoutSession(string Id, bool Confirmed);

public interface IPaymentGateway
{
    Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken);
}
