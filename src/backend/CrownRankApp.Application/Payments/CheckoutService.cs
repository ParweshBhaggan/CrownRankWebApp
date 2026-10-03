using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CrownRankApp.Domain.Models;

namespace CrownRankApp.Application.Payments;

public sealed class CheckoutService(IPaymentStore store, IPaymentGateway gateway, PaymentSettings settings, TimeProvider clock)
{
    public async Task<CheckoutResponse> StartEntryAsync(EntryCheckoutRequest request, CancellationToken ct = default)
    {
        ValidateReference(request.ReferenceId);
        settings.ValidateAmount(request.Amount);
        if (!request.AcceptedAgreements) throw new ArgumentException("Accept the terms and privacy policy before paying.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100
            || !Regex.IsMatch(request.Username ?? "", "^[a-zA-Z0-9._-]{2,40}$")
            || request.CategoryId == Guid.Empty || request.SocialProfiles is null || request.SocialProfiles.Count is < 1 or > 5
            || request.SocialProfiles.Select(profile => profile.PlatformId).Distinct().Count() != request.SocialProfiles.Count)
            throw new ArgumentException("Provide a valid name, username, category, and one to five distinct social profiles.");
        foreach (var profile in request.SocialProfiles)
            if (profile.PlatformId == Guid.Empty || profile.Url is null || profile.Url.Length > 500
                || !Uri.TryCreate(profile.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0)
                throw new ArgumentException("Social profiles require valid HTTPS URLs.");
        var normalized = request with { Name = request.Name.Trim(), Username = request.Username.ToLowerInvariant() };
        var operation = NewOperation(request.ReferenceId, request.Amount, PaymentPurpose.Entry, Hash(normalized));
        operation.Description = $"CrownRank entry: {normalized.Name}";
        operation.ReservedUsername = normalized.Username;
        operation.AgreementsAcceptedAt = clock.GetUtcNow().UtcDateTime;
        operation.TermsVersion = settings.TermsVersion;
        operation.PrivacyVersion = settings.PrivacyVersion;
        return await StartAsync(operation, normalized, ct);
    }

    public async Task<CheckoutResponse> StartBoostAsync(BoostCheckoutRequest request, CancellationToken ct = default)
    {
        ValidateReference(request.ReferenceId);
        if (request.EntryId == Guid.Empty) throw new ArgumentException("Choose an entry to boost.");
        settings.ValidateAmount(request.Amount);
        var operation = NewOperation(request.ReferenceId, request.Amount, PaymentPurpose.Boost, Hash(request));
        operation.EntryId = request.EntryId;
        operation.Description = "CrownRank creator boost";
        return await StartAsync(operation, null, ct);
    }

    public async Task<PaymentResponse?> StatusAsync(Guid id, CancellationToken ct = default)
    {
        var operation = await store.FindAsync(id, ct);
        return operation is null ? null : Response(operation);
    }

    public async Task<PaymentResponse?> ConfirmAsync(Guid id, CancellationToken ct = default)
    {
        var operation = await store.FindAsync(id, ct);
        if (operation is null) return null;
        if (operation.FulfilledAt is null && operation.SessionId is not null)
            await ApplyAsync(await gateway.RetrieveAsync(operation.SessionId, ct), ct);
        return Response((await store.FindAsync(id, ct))!);
    }

    public async Task<CheckoutResponse?> ResumeAsync(Guid id, CancellationToken ct = default)
    {
        var operation = await store.FindAsync(id, ct);
        return operation is null ? null : await CheckoutAsync(operation, ct);
    }

    public async Task WebhookAsync(string payload, string signature, CancellationToken ct = default)
    {
        var checkout = gateway.VerifyWebhook(payload, signature);
        if (checkout is null) return;
        // Retrieve authoritative current state: old failure/expiry events cannot overwrite a paid session.
        await ApplyAsync(await gateway.RetrieveAsync(checkout.SessionId, ct), ct);
    }

    public async Task RecoverAsync(CancellationToken ct)
    {
        foreach (var operation in await store.GetPendingAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            await CheckoutAsync(operation, ct);
        }
    }

    private async Task<CheckoutResponse> StartAsync(PaymentOperation operation, EntryCheckoutRequest? entry, CancellationToken ct)
    {
        var existing = await store.FindAsync(operation.Id, ct);
        if (existing is not null)
        {
            if (existing.RequestHash != operation.RequestHash || existing.Purpose != operation.Purpose)
                throw new InvalidOperationException("This reference was already used with different details.");
            return await CheckoutAsync(existing, ct);
        }
        return await CheckoutAsync(await store.ReserveAsync(operation, entry, ct), ct);
    }

    private async Task<CheckoutResponse> CheckoutAsync(PaymentOperation operation, CancellationToken ct)
    {
        if (operation.FulfilledAt is not null) return new(operation.Id, null, "paid");
        if (operation.Status is PaymentStatus.Expired or PaymentStatus.Failed)
            return new(operation.Id, null, operation.Status.ToString().ToLowerInvariant());
        VerifiedCheckout checkout;
        if (operation.SessionId is not null)
            checkout = await gateway.RetrieveAsync(operation.SessionId, ct);
        else
        {
            // Do not recreate an uncertain session after Stripe's idempotency retention window.
            if (operation.ExpiresAt <= clock.GetUtcNow().UtcDateTime.AddMinutes(31))
                return new(operation.Id, null, "pending");
            checkout = await gateway.CreateAsync(operation, ct);
            await store.AttachSessionAsync(operation.Id, checkout, ct);
        }
        await ApplyAsync(checkout, ct);
        var current = (await store.FindAsync(operation.Id, ct))!;
        return new(operation.Id, checkout.Status == "open" ? checkout.Url : null, current.Status.ToString().ToLowerInvariant());
    }

    private async Task ApplyAsync(VerifiedCheckout checkout, CancellationToken ct)
    {
        var operation = await store.FindAsync(checkout.OperationId, ct)
            ?? throw new InvalidOperationException("Checkout has no matching CrownRank payment.");
        if (checkout.AmountMinor != checked((long)(operation.Amount * 100))
            || checkout.Currency != operation.Currency || (operation.SessionId is not null && operation.SessionId != checkout.SessionId))
            throw new InvalidOperationException("Checkout does not match the stored payment.");
        await store.ApplyAsync(checkout, ct);
    }

    private PaymentOperation NewOperation(Guid id, decimal amount, PaymentPurpose purpose, string hash) => new(id)
    {
        Amount = amount, Currency = settings.Currency, Purpose = purpose, RequestHash = hash,
        CreatedDate = clock.GetUtcNow().UtcDateTime, ExpiresAt = clock.GetUtcNow().UtcDateTime.AddHours(1)
    };
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static void ValidateReference(Guid reference)
    {
        if (reference == Guid.Empty) throw new ArgumentException("A nonempty request reference is required.");
    }
    private static PaymentResponse Response(PaymentOperation operation) => new(operation.Id,
        operation.Status.ToString().ToLowerInvariant(), operation.FulfilledAt is not null, operation.EntryId, operation.Amount, operation.Currency);
}
