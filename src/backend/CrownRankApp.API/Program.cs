using CrownRankApp.API.Authentication;
using CrownRankApp.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;
using System.Threading.RateLimiting;

namespace CrownRankApp.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
                        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                            ?? ["http://localhost:5173", "https://localhost:5173"])
                        .AllowAnyHeader().AllowAnyMethod()));

            var adminAuth = builder.Configuration.GetSection(AdminAuthSettings.SectionName).Get<AdminAuthSettings>()
                ?? throw new InvalidOperationException("AdminAuth configuration is required.");
            adminAuth.Validate();
            builder.Services.AddSingleton(adminAuth);
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<AdminTokenService>();

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = adminAuth.Issuer,
                            ValidateAudience = true,
                            ValidAudience = adminAuth.Audience,
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(adminAuth.SigningKey)),
                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.FromSeconds(30)
                        };
                    });
            builder.Services.AddAuthorization();

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi(options =>
                {
                    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
                    options.AddOperationTransformer<AuthorizationOperationTransformer>();
                });

            builder.Services.AddInfrastructure();
            builder.Services.AddPayments(builder.Configuration);
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
                    options.AddPolicy("admin-auth", http => RateLimitPartition.GetFixedWindowLimiter(
                            http.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 10,
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
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();

            app.MapControllers();

            app.Run();
        }
    }
}
