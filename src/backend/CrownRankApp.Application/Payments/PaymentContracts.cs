using CrownRankApp.Domain.Models;

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

    public string TermsVersion
    {
        get;
        set;
    } = "2026-10-03";

    public string PrivacyVersion
    {
        get;
        set;
    } = "2026-10-03";

    public void Validate()
    {
        // These currencies all use two decimal places. Other currencies need an explicit minor-unit policy.
        if (Currency is not ("usd" or "eur" or "gbp") || MinimumAmount <= 0 || MaximumAmount < MinimumAmount
            || MaximumAmount > 999999.99m || decimal.Round(MinimumAmount, 2) != MinimumAmount
            || decimal.Round(MaximumAmount, 2) != MaximumAmount
            || string.IsNullOrWhiteSpace(TermsVersion) || string.IsNullOrWhiteSpace(PrivacyVersion))
        {
            throw new InvalidOperationException("Invalid Payments configuration.");
        }
    }

    public void ValidateAmount(decimal amount)
    {
        if (amount < MinimumAmount || amount > MaximumAmount || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException($"Choose {MinimumAmount:0.00}–{MaximumAmount:0.00} {Currency.ToUpperInvariant()} with at most two decimal places.");
        }
    }
}

public sealed record SocialProfileRequest(Guid PlatformId, string Url);
public sealed record EntrySubmissionRequest(Guid ReferenceId, decimal Amount, string Name, string Username, Guid CategoryId, List<SocialProfileRequest> SocialProfiles, string ImageDataUrl, bool AcceptedAgreements);
public sealed record BoostCheckoutRequest(Guid ReferenceId, Guid EntryId, decimal Amount);
public sealed record CheckoutResponse(Guid Id, string? Url, string Status);
public sealed record PaymentResponse(Guid Id, string Status, bool Fulfilled, Guid? EntryId, decimal Amount, string Currency);
public sealed record VerifiedCheckout(string SessionId, Guid OperationId, long AmountMinor, string Currency, string Status, bool Paid, string? PaymentIntentId, DateTime? PaidAt, string? Url, bool LiveMode, bool Failed = false);

// Only the display name and amount form the checkout payload.
public sealed record CheckoutRequest(string Name, decimal Amount);
public sealed record CheckoutPreparationResponse(Guid Id, string Name, decimal Amount);
// Server-owned context is separate from the public checkout DTO.
public sealed record CheckoutContext(Guid OperationId, string Currency, DateTime CreatedAt, DateTime ExpiresAt);

public interface IPaymentGateway
{
    Task<VerifiedCheckout> CreateAsync(CheckoutRequest request, CheckoutContext context, CancellationToken ct);

    Task<VerifiedCheckout?> FindAsync(CheckoutContext context, CancellationToken ct);

    Task<VerifiedCheckout> RetrieveAsync(string sessionId, CancellationToken ct);

    VerifiedCheckout? VerifyWebhook(string payload, string signature);
}

public interface IPaymentStore
{
    Task<PaymentOperation?> FindAsync(Guid id, CancellationToken ct);

    Task<PaymentOperation> ReserveAsync(PaymentOperation operation, EntrySubmissionRequest? entry, CancellationToken ct);

    Task AttachSessionAsync(Guid id, VerifiedCheckout checkout, CancellationToken ct);

    Task ApplyAsync(VerifiedCheckout checkout, CancellationToken ct);

    Task ExpireUncreatedAsync(Guid id, CancellationToken ct);

    Task<List<PaymentOperation>> GetPendingAsync(CancellationToken ct);
}

public sealed class PaymentUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class InvalidWebhookException : Exception
{
}
