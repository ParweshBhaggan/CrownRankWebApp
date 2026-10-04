using CrownRankApp.Application.Dtos.Entry;

namespace CrownRankApp.Application.Payments;

public sealed class PaymentSettings
{
    public string Currency
    {
        get;
        set;
    } = "usd";

    public decimal MinimumAmount
    {
        get;
        set;
    } = 10m;

    public decimal MaximumAmount
    {
        get;
        set;
    } = 10000m;
}

public sealed record CheckoutRequest(string Name, decimal Amount);
public sealed record CheckoutResponse(Guid Id, string? Url, string Status);
public sealed record PaymentResponse(Guid Id, string Status, bool Fulfilled, Guid? EntryId, decimal Amount, string Currency, string Purpose, string? Name = null);
public sealed record CheckoutContext(Guid ReferenceId, string Currency, DateTime CreatedAt, DateTime ExpiresAt, string FrontendUrl);
public sealed record VerifiedCheckout(string SessionId, Guid ReferenceId, long AmountInCents, string Currency, string Status, bool Paid, bool LiveMode, string? Url, DateTime? PaidAt);

public interface IPaymentGateway
{
    Task<VerifiedCheckout> CreateAsync(CheckoutRequest request, CheckoutContext context, CancellationToken ct);

    Task<VerifiedCheckout?> FindAsync(CheckoutContext context, CancellationToken ct);

    Task<VerifiedCheckout> RetrieveAsync(string sessionId, CancellationToken ct);

    VerifiedCheckout? VerifyWebhook(string payload, string signature);
}

public interface IPaymentService
{
    Task<CheckoutResponse> StartAsync(Guid referenceId, CheckoutRequest request, Guid? boostEntryId, CancellationToken ct);

    Task<CheckoutResponse> ResumeAsync(Guid referenceId, CancellationToken ct);

    Task<PaymentResponse> ConfirmAsync(Guid referenceId, CancellationToken ct);

    Task<EntryResponseDto> RegisterEntryAsync(Guid referenceId, EntryResponseDto entry, CancellationToken ct);

    Task<EntryResponseDto> RegisterBoostAsync(Guid referenceId, Guid entryId, CancellationToken ct);

    Task HandleWebhookAsync(string payload, string signature, CancellationToken ct);
}

public sealed class PaymentUnavailableException(string message, Exception? inner = null) : Exception(message, inner)
{
}

public sealed class InvalidWebhookException() : Exception("The Stripe webhook signature or environment is invalid.")
{
}
