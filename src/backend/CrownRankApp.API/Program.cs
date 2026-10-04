using CrownRankApp.Infrastructure;
using CrownRankApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;

namespace CrownRankApp.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            ProductionConfiguration.Validate(builder.Configuration, builder.Environment.IsProduction());

            // Add services to the container.

            builder.Services.AddControllers(options => options.Filters.Add<AdminApiKeyFilter>());
            builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
                        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                            ?? ["http://localhost:5173", "https://localhost:5173"])
                        .AllowAnyHeader().AllowAnyMethod()));
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            builder.Services.AddInfrastructure();
            builder.Services.AddPayments(builder.Configuration);
            builder.Services.AddHostedService<PaymentRecoveryWorker>();
            builder.Services.AddProblemDetails();
            builder.Services.AddRateLimiter(options =>
                {
                    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                    options.AddPolicy("payments", http => RateLimitPartition.GetFixedWindowLimiter(
                            http.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
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
            if (app.Environment.IsProduction())
            {
                app.UseHsts();
            }
            app.Use(async (context, next) =>
                {
                    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                    context.Response.Headers["Referrer-Policy"] = "no-referrer";
                    if (context.Request.Path.StartsWithSegments("/api/payments") || context.Request.Path.StartsWithSegments("/health") || !HttpMethods.IsGet(context.Request.Method))
                    {
                        context.Response.Headers.CacheControl = "no-store";
                    }
                    await next();
                });
            app.UseHttpsRedirection();

            app.UseCors("Frontend");
            app.UseAuthorization();
            app.UseRateLimiter();

            app.MapControllers();
            app.MapGet("/health/live", () =>
                {
                    return Results.Ok(new
                        {
                            status = "alive"
                        });
                });
            app.MapGet("/health/ready", async (ApplicationDbContext database, CancellationToken ct) =>
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(TimeSpan.FromSeconds(5));
                    try
                    {
                        if (await database.Database.CanConnectAsync(timeout.Token))
                        {
                            await database.CheckoutPayments.AsNoTracking().Select(payment => payment.Id).Take(1).ToListAsync(timeout.Token);
                            return Results.Ok(new
                                {
                                    status = "ready"
                                });
                        }
                    }
                    catch (Exception) when (!ct.IsCancellationRequested)
                    {
                        // Health responses must not disclose database credentials or connection errors.
                    }
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                });

            app.Run();
        }
    }
}
