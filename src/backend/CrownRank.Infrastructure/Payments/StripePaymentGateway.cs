using CrownRank.Application.Abstractions;
using CrownRank.Domain.Creators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace CrownRank.Infrastructure.Payments;

public sealed class StripePaymentGateway : IPaymentGateway
{
    private readonly SessionService sessions;
    private readonly string frontendOrigin;

    public StripePaymentGateway(IOptions<StripeOptions> options, IConfiguration configuration)
    {
        var secretKey = options.Value.SecretKey;
        if (string.IsNullOrWhiteSpace(secretKey) || !secretKey.StartsWith("sk_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Stripe:SecretKey must be a Stripe sandbox secret key (sk_test_...).");
        var origin = configuration["Frontend:Origin"] ?? "http://localhost:5173";
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not "http" and not "https")
            throw new InvalidOperationException("Frontend:Origin must be an absolute HTTP or HTTPS URL.");

        sessions = new SessionService(new StripeClient(secretKey));
        frontendOrigin = origin.TrimEnd('/');
    }

    public async Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        Money.Validate(request.Amount);
        if (request.ReferenceId == Guid.Empty) throw new ArgumentException("A checkout reference is required.");
        if (!string.Equals(request.Currency, "USD", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only USD is supported.");
        var description = request.Purpose switch
        {
            "ranking-entry" => "CrownRank ranking entry",
            "creator-boost" => "CrownRank creator Boost",
            _ => throw new ArgumentException("Unknown payment purpose.")
        };

        var returnQuery = $"referenceId={request.ReferenceId:D}&creatorId={request.CreatorId:D}&purpose={request.Purpose}";
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = request.ReferenceId.ToString("D"),
            SuccessUrl = $"{frontendOrigin}/payment/complete?{returnQuery}&session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{frontendOrigin}/payment/cancel?{returnQuery}",
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        UnitAmount = checked((long)(request.Amount * 100m)),
                        ProductData = new SessionLineItemPriceDataProductDataOptions { Name = description }
                    }
                }
            ],
            Metadata = new Dictionary<string, string>
            {
                ["creator_id"] = request.CreatorId.ToString("D"),
                ["reference_id"] = request.ReferenceId.ToString("D"),
                ["purpose"] = request.Purpose,
                ["amount"] = request.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                ["currency"] = "USD"
            }
        };
        var requestOptions = new RequestOptions
        {
            IdempotencyKey = $"crownrank-{request.Purpose}-{request.ReferenceId:N}"
        };
        var session = await sessions.CreateAsync(options, requestOptions, cancellationToken);
        if (string.IsNullOrWhiteSpace(session.Url))
            throw new InvalidOperationException("Stripe did not return a Checkout URL.");
        return new CheckoutSession(session.Id, false, session.Url);
    }
}
