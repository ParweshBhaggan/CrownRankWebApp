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
