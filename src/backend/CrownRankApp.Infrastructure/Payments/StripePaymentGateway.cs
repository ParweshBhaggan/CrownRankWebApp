using CrownRankApp.Application.Payments;
using CrownRankApp.Domain.Models;
using Stripe;
using Stripe.Checkout;

namespace CrownRankApp.Infrastructure.Payments;

public sealed class StripeSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string FrontendUrl { get; set; } = "http://localhost:5173";
    public bool LiveMode { get; set; }
}

public sealed class StripePaymentGateway(StripeClient client, StripeSettings settings) : IPaymentGateway
{
    public async Task<VerifiedCheckout> CreateAsync(PaymentOperation operation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.SecretKey))
            throw new PaymentUnavailableException("Stripe checkout is not configured yet.");
        var root = settings.FrontendUrl.TrimEnd('/');
        var metadata = new Dictionary<string, string> { ["crownrank_payment_id"] = operation.Id.ToString() };
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            AdaptivePricing = new SessionAdaptivePricingOptions { Enabled = false },
            PaymentMethodTypes = ["card"],
            ClientReferenceId = operation.Id.ToString(),
            Metadata = metadata,
            PaymentIntentData = new SessionPaymentIntentDataOptions { Metadata = metadata },
            ExpiresAt = operation.ExpiresAt,
            SuccessUrl = $"{root}/payment/success?payment_id={operation.Id}&session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{root}/payment/cancel?payment_id={operation.Id}",
            LineItems = [new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = operation.Currency, UnitAmount = checked((long)(operation.Amount * 100)),
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = operation.Description }
                }
            }]
        };
        try
        {
            var session = await client.V1.Checkout.Sessions.CreateAsync(options,
                new RequestOptions { IdempotencyKey = $"crownrank-checkout-{operation.Id:N}" }, ct);
            return Map(session);
        }
        catch (StripeException exception)
        {
            throw new PaymentUnavailableException("Checkout could not be started. Retry with the same submission.", exception);
        }
    }

    public async Task<VerifiedCheckout> RetrieveAsync(string sessionId, CancellationToken ct)
    {
        try
        {
            var session = await client.V1.Checkout.Sessions.GetAsync(sessionId,
                new SessionGetOptions { Expand = ["payment_intent.latest_charge"] }, cancellationToken: ct);
            return Map(session);
        }
        catch (StripeException exception)
        {
            throw new PaymentUnavailableException("Payment confirmation is temporarily unavailable. Please retry.", exception);
        }
    }

    public VerifiedCheckout? VerifyWebhook(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(settings.WebhookSecret))
            throw new PaymentUnavailableException("Stripe webhooks are not configured yet.");
        Event stripeEvent;
        try { stripeEvent = EventUtility.ConstructEvent(payload, signature, settings.WebhookSecret); }
        catch (StripeException) { throw new InvalidWebhookException(); }
        if (stripeEvent.Livemode != settings.LiveMode) throw new InvalidWebhookException();
        if (stripeEvent.Type is not ("checkout.session.completed" or "checkout.session.async_payment_succeeded"
            or "checkout.session.async_payment_failed" or "checkout.session.expired")) return null;
        if (stripeEvent.Data.Object is not Session session) throw new InvalidWebhookException();
        // Ignore sessions owned by other integrations on the same Stripe account.
        if (!session.Metadata.ContainsKey("crownrank_payment_id")) return null;
        return Map(session);
    }

    private VerifiedCheckout Map(Session session)
    {
        if (session.Livemode != settings.LiveMode || session.Mode != "payment"
            || !session.Metadata.TryGetValue("crownrank_payment_id", out var id)
            || !Guid.TryParse(id, out var operationId) || session.ClientReferenceId != id)
            throw new InvalidOperationException("Stripe session does not belong to this CrownRank environment.");
        return new(session.Id, operationId, session.AmountTotal ?? -1, session.Currency, session.Status,
            session.PaymentStatus == "paid", session.PaymentIntentId,
            session.PaymentStatus == "paid" ? session.PaymentIntent?.LatestCharge?.Created : null,
            session.Url, session.Livemode);
    }
}
