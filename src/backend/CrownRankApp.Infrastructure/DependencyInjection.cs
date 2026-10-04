using CrownRankApp.Application.Payments;
using CrownRankApp.Infrastructure.Payments;
using Stripe;
using CrownRankApp.Application.Services.Category;
using CrownRankApp.Application.Services.Entry;
using CrownRankApp.Application.Services.SocialMedia;
using CrownRankApp.Infrastructure.Data;
using CrownRankApp.Infrastructure.Services.Category;
using CrownRankApp.Infrastructure.Services.Entry;
using CrownRankApp.Infrastructure.Services.SocialMedia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrownRankApp.Infrastructure
{
    public static class DependencyInjection
    {

        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<ICategoryService, CategoryServices>();
            services.AddScoped<ISocialMediaPlatformServices, SocialMediaPlatformServices>();
            services.AddScoped<ISocialMediaDefaultService, SocialMediaDefaultServices>();
            services.AddScoped<IEntryServices, EntryServices>();

            return services;
        }

        public static IServiceCollection AddPayments(this IServiceCollection services, IConfiguration configuration)
        {
            var payment = configuration.GetSection("Payments").Get<PaymentSettings>() ?? new PaymentSettings();
            var stripe = configuration.GetSection("Stripe").Get<StripeSettings>() ?? new StripeSettings();
            payment.Currency = payment.Currency.ToLowerInvariant();
            if (payment.Currency is not ("usd" or "eur" or "gbp") || payment.MinimumAmount < 1 || payment.MaximumAmount < payment.MinimumAmount
                || payment.MaximumAmount > 9999999999999999.99m || decimal.Round(payment.MinimumAmount, 2) != payment.MinimumAmount
                || decimal.Round(payment.MaximumAmount, 2) != payment.MaximumAmount)
            {
                throw new InvalidOperationException("Configure valid payment currency and amount limits.");
            }
            if (!Uri.TryCreate(stripe.FrontendUrl, UriKind.Absolute, out var frontend)
                || (frontend.Scheme != Uri.UriSchemeHttps && !(frontend.IsLoopback && frontend.Scheme == Uri.UriSchemeHttp))
                || !string.IsNullOrEmpty(frontend.Query) || !string.IsNullOrEmpty(frontend.Fragment) || !string.IsNullOrEmpty(frontend.UserInfo))
            {
                throw new InvalidOperationException("Stripe:FrontendUrl must be an HTTPS URL or a local development HTTP URL without query parameters.");
            }
            services.AddSingleton(payment);
            services.AddSingleton(stripe);
            services.AddSingleton(new StripeClient(string.IsNullOrWhiteSpace(stripe.SecretKey) ? "sk_test_unconfigured" : stripe.SecretKey));
            services.AddScoped<IPaymentGateway, StripePaymentGateway>();
            services.AddScoped<IPaymentService, PaymentService>();
            var recovery = configuration.GetSection("PaymentRecovery").Get<PaymentRecoverySettings>() ?? new PaymentRecoverySettings();
            if (recovery.PollSeconds is < 30 or > 3600 || recovery.RetryMinutes is < 1 or > 60
                || recovery.StaleEntryMinutes is < 5 or > 1440 || recovery.BatchSize is < 1 or > 100)
            {
                throw new InvalidOperationException("Configure bounded PaymentRecovery polling intervals and batch size.");
            }
            services.AddSingleton(recovery);
            services.AddScoped<PaymentReconciler>();
            return services;
        }

        public static IServiceCollection AddDatabaseService(this IServiceCollection dbServices, IConfiguration configuration)
        {
            var dbProvider = configuration["Database:Provider"] ?? throw new InvalidOperationException("Database provider not configured");
            var connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Database connection string not configured");

            dbServices.AddDbContext<ApplicationDbContext>(options =>
                {
                    switch (dbProvider.ToLower())
                    {
                            case "postgresql":
                            options.UseNpgsql(connectionString);
                            break;
                            default:
                            throw new NotSupportedException($"Database provider '{dbProvider}' is not supported. Please configure a supported database provider.");
                    }

                });

            return dbServices;
        }
    }
}
