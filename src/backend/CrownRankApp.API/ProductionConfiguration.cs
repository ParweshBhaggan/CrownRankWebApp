using CrownRankApp.Infrastructure.Payments;
using Microsoft.Extensions.Configuration.Json;

namespace CrownRankApp.API;

public static class ProductionConfiguration
{
    public static void Validate(IConfiguration configuration, bool production)
    {
        if (!production)
        {
            return;
        }
        foreach (var key in new[]
        {
            "Admin:ApiKey",
            "ConnectionStrings:DefaultConnection",
            "Stripe:SecretKey",
            "Stripe:WebhookSecret"
        })
        {
            if (string.IsNullOrWhiteSpace(configuration[key]) || !IsExternal(configuration, key))
            {
                throw new InvalidOperationException($"Production requires {key} from environment variables or a secrets configuration provider, not committed appsettings.");
            }
        }
        if (configuration["Admin:ApiKey"]!.Length is < 32 or > 256)
        {
            throw new InvalidOperationException("Production requires a random Admin:ApiKey between 32 and 256 characters.");
        }
        var stripe = configuration.GetSection("Stripe").Get<StripeSettings>()!;
        var prefix = stripe.LiveMode ? "_live_" : "_test_";
        if (!(stripe.SecretKey.StartsWith("sk" + prefix, StringComparison.Ordinal) || stripe.SecretKey.StartsWith("rk" + prefix, StringComparison.Ordinal))
            || !stripe.WebhookSecret.StartsWith("whsec_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Stripe credentials must match the configured test/live environment.");
        }
        if (!IsPublicHttpsOrigin(stripe.FrontendUrl))
        {
            throw new InvalidOperationException("Production requires a public HTTPS Stripe:FrontendUrl.");
        }
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (origins is null || origins.Length == 0 || origins.Any(origin => !IsPublicHttpsOrigin(origin)))
        {
            throw new InvalidOperationException("Production requires explicit public HTTPS Cors:AllowedOrigins.");
        }
    }

    private static bool IsExternal(IConfiguration configuration, string key)
    {
        if (configuration is not IConfigurationRoot root)
        {
            return false;
        }
        foreach (var provider in root.Providers.Reverse())
        {
            if (provider.TryGet(key, out _))
            {
                return provider is not JsonConfigurationProvider and not JsonStreamConfigurationProvider;
            }
        }
        return false;
    }

    private static bool IsPublicHttpsOrigin(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            && !uri.IsLoopback && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment) && uri.AbsolutePath == "/";
    }
}
