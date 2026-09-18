using CrownRank.Application.Abstractions;
using CrownRank.Infrastructure.Payments;
using CrownRank.Infrastructure.Persistence;
using CrownRank.Infrastructure.Storage;
using CrownRank.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrownRank.Infrastructure.Configuration;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddCrownRankSqlite(this IServiceCollection services, string connectionString,
        string imageDirectory, bool developmentMockPayments, string imagePublicBaseUrl = "/uploads/profiles")
    {
        if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Connection string is required.");
        services.AddCrownRankPersistence(options => options.UseSqlite(connectionString), imageDirectory, imagePublicBaseUrl);
        return services.AddDevelopmentPayments(imageDirectory, developmentMockPayments);
    }

    public static IServiceCollection AddCrownRankPostgreSql(this IServiceCollection services, string connectionString,
        string imageDirectory, bool developmentMockPayments, string imagePublicBaseUrl = "/uploads/profiles")
    {
        if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("Connection string is required.");
        services.AddCrownRankPersistence(
            options => options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()),
            imageDirectory,
            imagePublicBaseUrl);
        return services.AddDevelopmentPayments(imageDirectory, developmentMockPayments);
    }

    public static IServiceCollection AddCrownRankPersistence(this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureProvider, string imageDirectory,
        string imagePublicBaseUrl = "/uploads/profiles")
    {
        services.AddDbContext<CrownRankDbContext>(configureProvider);
        services.AddScoped<IEntryRepository, EntryRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IContributionRepository, ContributionRepository>();
        services.AddScoped<IPaymentAttemptRepository, PaymentAttemptRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ILeaderboardQueries, EfLeaderboardQueries>();
        services.AddScoped<IAdminQueries, EfAdminQueries>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IProfileImageStorage>(
            new LocalProfileImageStorage(imageDirectory, imagePublicBaseUrl));
        return services;
    }

    private static IServiceCollection AddDevelopmentPayments(
        this IServiceCollection services, string imageDirectory, bool enabled)
    {
        if (enabled)
        {
            services.AddSingleton(new MockPaymentGateway(Path.Combine(imageDirectory, "mock-payments.json")));
            services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<MockPaymentGateway>());
        }
        return services;
    }

    public static IServiceCollection AddCrownRankPolicies(this IServiceCollection services, LegalVersions versions,
        string adminSaltBase64, string adminHashBase64)
    {
        services.AddSingleton<ILegalDocumentVersions>(new ConfiguredLegalVersions(versions));
        services.AddSingleton<IAdminAuthorization>(new PasswordAdminAuthorization(adminSaltBase64, adminHashBase64));
        return services;
    }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
