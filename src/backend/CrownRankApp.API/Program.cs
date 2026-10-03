using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

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
            builder.Services.AddPayments(builder.Configuration,
                builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
            builder.Services.AddHostedService<PaymentRecoveryWorker>();
            builder.Services.AddProblemDetails();
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("payments", http => RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
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
            app.UseStaticFiles();

            app.UseCors("Frontend");
            app.UseAuthorization();
            app.UseRateLimiter();


            app.MapControllers();

            app.Run();
        }
    }
}

