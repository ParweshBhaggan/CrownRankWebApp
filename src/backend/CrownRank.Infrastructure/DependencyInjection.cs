using CrownRank.Application.Abstractions;
using CrownRank.Infrastructure.Images;
using CrownRank.Infrastructure.Payments;
using CrownRank.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrownRank.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' is missing.");

        services.AddDbContext<CrownRankDbContext>((serviceProvider, options) =>
        {
            var connectionString = configuration.GetConnectionString("Database")
                ?? throw new InvalidOperationException(
                    "Connection string 'Database' was not found.");

            options.UseNpgsql(
                connectionString,
                postgresOptions =>
                {
                    postgresOptions.MigrationsHistoryTable(
                        "__EFMigrationsHistory",
                        "CrownrankSchema");
                });
        });
        services.Configure<ProfileImageOptions>(configuration.GetSection(ProfileImageOptions.SectionName));
        services.AddScoped<ICreatorRepository, CreatorRepository>();
        services.AddScoped<IProfileImageService, LocalProfileImageService>();
        services.AddScoped<IPaymentGateway, DevelopmentPaymentGateway>();
        return services;
    }
}
