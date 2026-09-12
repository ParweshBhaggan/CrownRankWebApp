using System.Text.Json;
using CrownRank.Application.Creators;
using CrownRank.Domain.Creators;
using Microsoft.AspNetCore.Mvc;

namespace CrownRank.Api.Endpoints;

public static class CreatorEndpoints
{
    public static IEndpointRouteBuilder MapCreatorEndpoints(this IEndpointRouteBuilder endpoints, bool development)
    {
        var group = endpoints.MapGroup("/api/creators").WithTags("Creators");
        group.MapGet("/", GetAll).WithName("GetCreators").WithSummary("Returns all creators with confirmed global and current UTC-day scores.");
        group.MapGet("/{id:guid}", GetById).WithName("GetCreator");
        if (development) group.MapPost("/", Create).WithName("CreateCreator").WithSummary("Creates a pending entry and starts its configured checkout.").DisableAntiforgery();
        if (development) group.MapDelete("/{id:guid}", Delete).WithName("DeleteCreator").WithDescription("Development only: hide the public profile while retaining its contribution ledger.");
        endpoints.MapGet("/api/rankings/daily/{date}", async (DateOnly date, CreatorService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetDailyAsync(date, cancellationToken))).WithTags("Rankings").WithName("GetDailyRanking");
        return endpoints;
    }

    private static async Task<IResult> GetAll(CreatorService service, CancellationToken cancellationToken) => Results.Ok(await service.GetAllAsync(cancellationToken));
    private static async Task<IResult> GetById(Guid id, CreatorService service, CancellationToken cancellationToken)
    { var creator = await service.GetAsync(id, cancellationToken); return creator is null ? Results.NotFound() : Results.Ok(creator); }

    private static async Task<IResult> Create([FromForm] CreateCreatorRequest request, CreatorService service, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Category) || string.IsNullOrWhiteSpace(request.SocialProfilesJson))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Name, username, category, and social profiles are required."] });
        try
        {
            var profiles = JsonSerializer.Deserialize<List<SocialProfileRequest>>(request.SocialProfilesJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
            if (profiles.Count is < 1 or > 5 || profiles.Any(x => x is null || string.IsNullOrWhiteSpace(x.Platform) || string.IsNullOrWhiteSpace(x.Url)))
                throw new ArgumentException("Provide 1–5 complete social profiles.");
            using var imageStream = request.Image?.OpenReadStream();
            var command = new CreateCreatorCommand(request.Name, request.Username, ParseCategory(request.Category), request.EntryReference,
                profiles.Select(x => new SocialProfileInput(ParsePlatform(x.Platform), x.Url)).ToList(), request.InitialAmount,
                imageStream, request.Image?.FileName, request.Image?.ContentType, request.Image?.Length ?? 0);
            var checkout = await service.StartEntryCheckoutAsync(command, cancellationToken);
            return Results.Created($"/api/creators/{checkout.CreatorId}", checkout);
        }
        catch (JsonException) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["socialProfiles"] = ["Social profiles must be valid JSON."] }); }
        catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [exception.Message] }); }
        catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
    }

    private static async Task<IResult> Delete(Guid id, CreatorService service, CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound();

    private static CreatorCategory ParseCategory(string value) => value.Replace("-", string.Empty).ToLowerInvariant() switch
    {
        "streamer" => CreatorCategory.Streamer, "gaming" => CreatorCategory.Gaming, "influencer" => CreatorCategory.Influencer,
        "adultentertainment" => CreatorCategory.AdultEntertainment, "beautyfashion" => CreatorCategory.BeautyFashion,
        "fitnesswellness" => CreatorCategory.FitnessWellness, "music" => CreatorCategory.Music, "podcasting" => CreatorCategory.Podcasting,
        "education" => CreatorCategory.Education, "comedy" => CreatorCategory.Comedy, "artdesign" => CreatorCategory.ArtDesign,
        "food" => CreatorCategory.Food, "travel" => CreatorCategory.Travel, "technology" => CreatorCategory.Technology,
        "business" => CreatorCategory.Business, "other" => CreatorCategory.Other, _ => throw new ArgumentException("Unknown creator category.")
    };
    private static SocialPlatform ParsePlatform(string value) => value.ToLowerInvariant() switch
    {
        "instagram" => SocialPlatform.Instagram, "tiktok" => SocialPlatform.TikTok, "youtube" => SocialPlatform.YouTube,
        "x" => SocialPlatform.X, "twitch" => SocialPlatform.Twitch, "onlyfans" => SocialPlatform.OnlyFans,
        "website" => SocialPlatform.Website, _ => throw new ArgumentException("Unknown social platform.")
    };
}

public sealed class CreateCreatorRequest
{
    public required string Name { get; init; }
    public required string Username { get; init; }
    public required string Category { get; init; }
    public Guid EntryReference { get; init; }
    public required string SocialProfilesJson { get; init; }
    public decimal InitialAmount { get; init; }
    public IFormFile? Image { get; init; }
}
public sealed record SocialProfileRequest(string Platform, string Url);
