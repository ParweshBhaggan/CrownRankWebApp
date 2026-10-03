using CrownRankApp.Application.Payments;
using CrownRankApp.Infrastructure.Payments;
using StripeClient = Stripe.StripeClient;
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

        public static IServiceCollection AddPayments(this IServiceCollection services, IConfiguration configuration, string webRoot)
        {
            var payments = configuration.GetSection("Payments").Get<PaymentSettings>() ?? new PaymentSettings();
            payments.Validate();
            var stripe = configuration.GetSection("Stripe").Get<StripeSettings>() ?? new StripeSettings();
            if (!Uri.TryCreate(stripe.FrontendUrl, UriKind.Absolute, out var url)
                || url.Scheme is not ("http" or "https") || url.UserInfo.Length != 0 || url.Query.Length != 0 || url.Fragment.Length != 0
                || (url.Scheme == "http" && !url.IsLoopback))
            {
                throw new InvalidOperationException("Stripe:FrontendUrl must be HTTPS (HTTP is allowed for localhost).");
            }
            // Empty keys allow tooling/migrations to run; actual checkout requires configured keys.
            if (!string.IsNullOrEmpty(stripe.SecretKey) && stripe.SecretKey.StartsWith("sk_live_") != stripe.LiveMode)
            {
                throw new InvalidOperationException("Stripe key and LiveMode do not match.");
            }
            services.AddSingleton(payments);
            services.AddSingleton(stripe);
            services.AddSingleton(new StripeClient(string.IsNullOrWhiteSpace(stripe.SecretKey) ? "sk_test_unconfigured" : stripe.SecretKey));
            services.AddSingleton(new ProfileImageStorage(webRoot));
            services.AddScoped<IPaymentGateway, StripePaymentGateway>();
            services.AddScoped<IPaymentStore, PaymentStore>();
            services.AddScoped<CheckoutService>();
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
