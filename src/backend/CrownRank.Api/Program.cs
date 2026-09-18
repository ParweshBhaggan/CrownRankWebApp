using CrownRank.Api;
using CrownRank.Application.Abstractions;
using CrownRank.Application.Services;
using CrownRank.Infrastructure.Configuration;
using CrownRank.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var settings = builder.Configuration.GetSection("CrownRank").Get<ApiSettings>() ?? new ApiSettings();
var contentRoot = builder.Environment.ContentRootPath;
var imageDirectory = Path.GetFullPath(settings.ImageDirectory, contentRoot);
Directory.CreateDirectory(imageDirectory);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 6 * 1024 * 1024);

if (settings.DatabaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
{
    var connectionString = builder.Configuration.GetConnectionString("CrownRank");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("ConnectionStrings:CrownRank is required for PostgreSQL.");
    builder.Services.AddCrownRankPostgreSql(
        connectionString, imageDirectory, settings.EnableMockPayments, settings.ImagePublicBaseUrl);
}
else if (settings.DatabaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    var databasePath = Path.GetFullPath(settings.DatabasePath, contentRoot);
    Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
    builder.Services.AddCrownRankSqlite(
        $"Data Source={databasePath}", imageDirectory, settings.EnableMockPayments, settings.ImagePublicBaseUrl);
}
else
{
    throw new InvalidOperationException("CrownRank:DatabaseProvider must be PostgreSQL or Sqlite.");
}

builder.Services.AddSingleton<ILegalDocumentVersions>(new ConfiguredLegalVersions(
    new LegalVersions(settings.TermsVersion, settings.PrivacyVersion, settings.RulesVersion)));
builder.Services.AddSingleton<IAdminAuthorization>(new ConfiguredAdminAuthorization(settings.AdminPassword));
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<EntrySubmissionService>();
builder.Services.AddScoped<PublicReadService>();
builder.Services.AddScoped<AdminEntryService>();
builder.Services.AddScoped<AdminCategoryService>();
builder.Services.AddScoped<AdminInspectionService>();

builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken(options =>
{
    options.BearerTokenExpiration = TimeSpan.FromMinutes(30);
    options.RefreshTokenExpiration = TimeSpan.FromHours(8);
});
builder.Services.AddAuthorizationBuilder().AddPolicy("Admin", policy =>
    policy.RequireAuthenticatedUser().RequireRole("Admin"));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("writes", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
if (settings.AllowedOrigins.Length > 0)
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.WithOrigins(settings.AllowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
if (!app.Environment.IsEnvironment("Testing")) app.UseHttpsRedirection();
app.UseExceptionHandler();
if (settings.AllowedOrigins.Length > 0) app.UseCors();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imageDirectory),
    RequestPath = "/uploads/profiles"
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
await app.Services.MigrateCrownRankAsync();
if (app.Environment.IsDevelopment()) await app.Services.SeedDevelopmentDataAsync();
app.MapCrownRankEndpoints(builder.Environment, settings);
app.Run();

public partial class Program { }
