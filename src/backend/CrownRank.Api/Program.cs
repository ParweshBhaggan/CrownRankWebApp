using CrownRank.Api.Data;
using CrownRank.Api.Domain;
using CrownRank.Api.Endpoints;
using CrownRank.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Database") ?? string.Empty;

builder.Services.AddDbContext<CrownRankDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<CreatorService>();
builder.Services.AddSingleton<IProfileImageStore, LocalProfileImageStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:5173", "https://localhost:5173")
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors();
app.UseStaticFiles();
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));
app.MapCreatorEndpoints();
app.MapPaymentEndpoints();
app.Run();

public partial class Program;
