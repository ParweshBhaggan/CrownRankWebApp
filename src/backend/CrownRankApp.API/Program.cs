using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;

using CrownRankApp.Infrastructure;
using Scalar.AspNetCore;

namespace CrownRankApp.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers(options => options.Filters.Add<AdminApiKeyFilter>());
            builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
                        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                            ?? ["http://localhost:5173", "https://localhost:5173"])
                        .AllowAnyHeader().AllowAnyMethod()));
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            builder.Services.AddInfrastructure();
            var frontendPublic = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../frontend/public"));
            var imageRoot = builder.Configuration["ProfileImages:StoragePath"];
            if (string.IsNullOrWhiteSpace(imageRoot))
            {
                imageRoot = builder.Environment.IsDevelopment() && Directory.Exists(frontendPublic)
                ? frontendPublic
                : Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
            }
            imageRoot = Path.GetFullPath(imageRoot, builder.Environment.ContentRootPath);
            var profileFolder = Path.Combine(imageRoot, "assets", "profiles");
            Directory.CreateDirectory(profileFolder);
            builder.Services.AddPayments(builder.Configuration, imageRoot);
            builder.Services.AddHostedService<PaymentRecoveryWorker>();
            builder.Services.AddProblemDetails();
            builder.Services.AddRateLimiter(options =>
                {
                    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                    options.AddPolicy("payments", http => RateLimitPartition.GetFixedWindowLimiter(
                            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 60,
                                Window = TimeSpan.FromMinutes(1),
                                QueueLimit = 0
                            }));
                });
            builder.Services.AddDatabaseService(builder.Configuration);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseExceptionHandler();
            app.UseHttpsRedirection();
            app.UseCors("Frontend");
            app.UseStaticFiles();
            app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(profileFolder),
                    RequestPath = "/assets/profiles"
                });
            app.UseAuthorization();
            app.UseRateLimiter();

            app.MapControllers();

            app.Run();
        }
    }
}
