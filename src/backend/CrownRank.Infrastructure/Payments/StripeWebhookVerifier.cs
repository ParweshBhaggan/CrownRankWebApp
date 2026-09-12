using System.Globalization;
using CrownRank.Application.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace CrownRank.Infrastructure.Payments;

public sealed class StripeWebhookVerifier(IOptions<StripeOptions> options) : IPaymentWebhookVerifier
{
    public PaymentConfirmation? Verify(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(options.Value.WebhookSecret))
            throw new InvalidOperationException("Stripe:WebhookSecret is not configured.");
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(payload, signature, options.Value.WebhookSecret);
            if (stripeEvent.Type is not ("checkout.session.completed" or "checkout.session.async_payment_succeeded"))
                return null;
            if (stripeEvent.Data.Object is not Session session || session.PaymentStatus != "paid")
                return null;

            var metadata = session.Metadata;
            if (!Guid.TryParse(Get(metadata, "creator_id"), out var creatorId) ||
                !Guid.TryParse(Get(metadata, "reference_id"), out var referenceId) ||
                !decimal.TryParse(Get(metadata, "amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                throw new PaymentWebhookException("Stripe Checkout metadata is incomplete.");
            var currency = Get(metadata, "currency");
            var purpose = Get(metadata, "purpose");
            if (session.AmountTotal is null || session.AmountTotal.Value != checked((long)(amount * 100m)) ||
                !string.Equals(session.Currency, currency, StringComparison.OrdinalIgnoreCase))
                throw new PaymentWebhookException("Stripe Checkout totals do not match their signed metadata.");

            return new PaymentConfirmation(
                creatorId, amount, currency, purpose, referenceId, $"stripe-{referenceId:N}");
        }
        catch (PaymentWebhookException)
        {
            throw;
        }
        catch (StripeException exception)
        {
            throw new PaymentWebhookException("The Stripe webhook signature or payload is invalid.", exception);
        }
    }

    private static string Get(Dictionary<string, string> metadata, string key) =>
        metadata.TryGetValue(key, out var value) ? value : string.Empty;
}
