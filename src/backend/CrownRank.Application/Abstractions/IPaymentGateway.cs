namespace CrownRank.Application.Abstractions;

public sealed record CheckoutRequest(Guid CreatorId, decimal Amount, string Currency, string Purpose, Guid ReferenceId);
public sealed record CheckoutSession(string Id, bool Confirmed, string? Url = null);

public sealed record PaymentConfirmation(
    Guid CreatorId,
    decimal Amount,
    string Currency,
    string Purpose,
    Guid ReferenceId,
    string PaymentReference);

public sealed class PaymentWebhookException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public interface IPaymentGateway
{
    Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken);
}

public interface IPaymentWebhookVerifier
{
    PaymentConfirmation? Verify(string payload, string signature);
}
