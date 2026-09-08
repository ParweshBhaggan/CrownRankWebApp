using CrownRank.Api.Endpoints;
using CrownRank.Application;
using CrownRank.Infrastructure;
using CrownRank.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new() { Title = "CrownRank API", Version = "v1", Description = "Local-development API for creator rankings." }));
builder.Services.AddProblemDetails();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "CrownRank API v1"); options.RoutePrefix = "swagger"; });
    if (builder.Configuration.GetValue<bool>("SeedData:Enabled")) await app.Services.InitializeDatabaseAsync();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseStaticFiles();
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("HealthCheck");
app.MapCreatorEndpoints(app.Environment.IsDevelopment());
if (app.Environment.IsDevelopment()) app.MapPaymentEndpoints();

app.Run();

public partial class Program;

