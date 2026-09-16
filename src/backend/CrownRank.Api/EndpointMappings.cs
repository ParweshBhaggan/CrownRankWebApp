using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrownRank.Application.Abstractions;
using CrownRank.Application.Contracts;
using CrownRank.Application.Payments;
using CrownRank.Application.Services;
using CrownRank.Domain.Models;
using CrownRank.Domain.ValueObjects;
using CrownRank.Infrastructure.Payments;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.RateLimiting;

namespace CrownRank.Api;

public static class EndpointMappings
{
    private static readonly JsonSerializerOptions FormJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static void MapCrownRankEndpoints(this WebApplication app, IHostEnvironment environment, ApiSettings settings)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

        var publicApi = app.MapGroup("/api");
        publicApi.MapGet("/categories", async (PublicReadService service, CancellationToken ct) =>
            Results.Ok((await service.CategoriesAsync(ct)).Select(c => new CategoryResponse(c.Id, c.Name, c.Description))));
        publicApi.MapGet("/leaderboards/global", async (Guid? categoryId, PublicReadService service, CancellationToken ct) =>
            Results.Ok(await service.GlobalAsync(categoryId, ct)));
        publicApi.MapGet("/leaderboards/daily", async (DateOnly date, Guid? categoryId, PublicReadService service, CancellationToken ct) =>
            Results.Ok(await service.DailyAsync(date, categoryId, ct)));
        publicApi.MapGet("/entries/{id:guid}", async (Guid id, PublicReadService service, CancellationToken ct) =>
        {
            var entry = await service.ProfileAsync(id, ct);
            return entry is null ? Results.NotFound() : Results.Ok(ToResponse(entry));
        });
        publicApi.MapPost("/entries", SubmitEntryAsync).DisableAntiforgery().RequireRateLimiting("writes");
        publicApi.MapPost("/entries/{id:guid}/boosts", async (Guid id, BoostRequest request, PaymentService payments, CancellationToken ct) =>
        {
            var started = await payments.StartBoostAsync(id, Money.Create(request.AmountInMinorUnits, request.Currency), ct);
            return Results.Accepted($"/api/payments/{started.AttemptId}", started);
        }).RequireRateLimiting("writes");
        publicApi.MapGet("/payments/{id:guid}", async (Guid id, PaymentService payments, CancellationToken ct) =>
            Results.Ok(ToResponse(await payments.GetStatusAsync(id, ct))));
        publicApi.MapPost("/payments/{id:guid}/confirm", async (Guid id, PaymentService payments, CancellationToken ct) =>
        {
            await payments.ConfirmAsync(id, ct);
            return Results.Ok(ToResponse(await payments.GetStatusAsync(id, ct)));
        }).RequireRateLimiting("writes");

        if (settings.EnableMockPayments && (environment.IsDevelopment() || environment.IsEnvironment("Testing")))
            publicApi.MapPost("/dev/payments/{id:guid}/outcome", (Guid id, MockOutcomeRequest request, MockPaymentGateway gateway) =>
            {
                if (!Enum.TryParse<MockPaymentGateway.MockOutcome>(request.Outcome, true, out var outcome))
                    return Results.BadRequest(new { error = "Outcome must be Processing, Succeeded, Failed or Cancelled." });
                gateway.SetOutcome(id, outcome);
                return Results.NoContent();
            }).RequireRateLimiting("writes");

        var admin = app.MapGroup("/api/admin");
        admin.MapPost("/login", AdminLoginAsync).AllowAnonymous().RequireRateLimiting("login");
        var protectedAdmin = admin.MapGroup(string.Empty).RequireAuthorization("Admin");
        protectedAdmin.MapGet("/entries", async (AdminInspectionService service, CancellationToken ct) =>
            Results.Ok(await service.EntriesAsync(ct)));
        protectedAdmin.MapGet("/categories", async (AdminInspectionService service, CancellationToken ct) =>
            Results.Ok(await service.CategoriesAsync(ct)));
        protectedAdmin.MapGet("/payments", async (AdminInspectionService service, CancellationToken ct) =>
            Results.Ok(await service.PaymentsAsync(ct)));
        protectedAdmin.MapPost("/categories", async (CategoryRequest request, AdminCategoryService service, CancellationToken ct) =>
        {
            var category = await service.CreateAsync(request.Name, request.Description, ct);
            return Results.Created($"/api/categories/{category.Id}", new CategoryResponse(category.Id, category.Name, category.Description));
        });
        protectedAdmin.MapPut("/categories/{id:guid}", async (Guid id, CategoryRequest request, AdminCategoryService service, CancellationToken ct) =>
        {
            await service.UpdateAsync(id, request.Name, request.Description, ct);
            return Results.NoContent();
        });
        protectedAdmin.MapDelete("/categories/{id:guid}", async (Guid id, AdminCategoryService service, CancellationToken ct) =>
        {
            await service.ArchiveAsync(id, ct);
            return Results.NoContent();
        });
        protectedAdmin.MapPut("/entries/{id:guid}/details", async (Guid id, EntryDetailsRequest request, AdminEntryService service, CancellationToken ct) =>
        {
            await service.UpdateDetailsAsync(id, request.Name, request.Username, ct);
            return Results.NoContent();
        });
        protectedAdmin.MapPut("/entries/{id:guid}/category", async (Guid id, EntryCategoryRequest request, AdminEntryService service, CancellationToken ct) =>
        {
            await service.ChangeCategoryAsync(id, request.CategoryId, ct);
            return Results.NoContent();
        });
        protectedAdmin.MapPut("/entries/{id:guid}/social-links", async (Guid id, SocialLinksRequest request, AdminEntryService service, CancellationToken ct) =>
        {
            await service.ReplaceSocialLinksAsync(id, request.Links, ct);
            return Results.NoContent();
        });
        protectedAdmin.MapPut("/entries/{id:guid}/image", ReplaceImageAsync).DisableAntiforgery();
        protectedAdmin.MapPost("/entries/{id:guid}/hide", async (Guid id, AdminEntryService service, CancellationToken ct) =>
        {
            await service.HideAsync(id, ct);
            return Results.NoContent();
        });
        protectedAdmin.MapPost("/entries/{id:guid}/restore", async (Guid id, AdminEntryService service, CancellationToken ct) =>
        {
            await service.RestoreAsync(id, ct);
            return Results.NoContent();
        });
        protectedAdmin.MapDelete("/entries/{id:guid}", async (Guid id, AdminEntryService service, CancellationToken ct) =>
        {
            await service.ArchiveAsync(id, ct);
            return Results.NoContent();
        });
    }

    private static async Task<IResult> SubmitEntryAsync(HttpRequest request, EntrySubmissionService service, CancellationToken ct)
    {
        if (!request.HasFormContentType) return Results.BadRequest(new { error = "multipart/form-data is required." });
        var form = await request.ReadFormAsync(ct);
        var image = form.Files.GetFile("image") ?? throw new ArgumentException("Profile image is required.");
        var links = JsonSerializer.Deserialize<List<SocialLinkInput>>(Required(form, "socialLinks"), FormJson)
                    ?? throw new ArgumentException("Social links are required.");
        await using var stream = image.OpenReadStream();
        var started = await service.SubmitAsync(new SubmitEntryRequest(
            Required(form, "name"), Required(form, "username"), Guid.Parse(Required(form, "categoryId")),
            stream, image.FileName, links, bool.Parse(Required(form, "acceptedAgreements")),
            long.Parse(Required(form, "amountInMinorUnits")), Required(form, "currency")), ct);
        return Results.Accepted($"/api/payments/{started.AttemptId}", started);
    }

    private static async Task<IResult> ReplaceImageAsync(Guid id, HttpRequest request, AdminEntryService service, CancellationToken ct)
    {
        var form = await request.ReadFormAsync(ct);
        var image = form.Files.GetFile("image") ?? throw new ArgumentException("Profile image is required.");
        await using var stream = image.OpenReadStream();
        await service.ReplaceImageAsync(id, stream, image.FileName, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AdminLoginAsync(AdminLoginRequest request, IAdminAuthorization authorization,
        HttpContext context, CancellationToken ct)
    {
        if (!await authorization.IsAuthorizedAsync(request.Password, ct)) return Results.Unauthorized();
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "crownrank-admin"),
            new Claim(ClaimTypes.Role, "Admin")
        ], BearerTokenDefaults.AuthenticationScheme);
        await context.SignInAsync(BearerTokenDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Results.Empty;
    }

    private static EntryResponse ToResponse(Entry entry) => new(entry.Id, entry.Name, entry.Username, entry.CategoryId,
        $"/assets/{Uri.EscapeDataString(entry.ProfileImageKey)}",
        entry.SocialMediaLinks.Select(x => new SocialLinkResponse(x.Platform.ToString(), x.Url, x.CustomPlatformName)).ToArray());

    private static PaymentResponse ToResponse(PaymentAttempt attempt) => new(attempt.Id, attempt.EntryId,
        attempt.Purpose.ToString(), attempt.State.ToString(), attempt.ExpectedAmount.AmountInMinorUnits,
        attempt.ExpectedAmount.Currency, attempt.CheckoutUrl);

    private static string Required(IFormCollection form, string key)
        => form.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : throw new ArgumentException($"{key} is required.");
}
