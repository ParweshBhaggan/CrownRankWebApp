using System.Globalization;
using CrownRank.Api.Contracts;
using CrownRank.Api.Domain;
using CrownRank.Api.Infrastructure;

namespace CrownRank.Api.Endpoints;

public static class CreatorEndpoints
{
    public static void MapCreatorEndpoints(this WebApplication app)
    {
        app.MapGet("/api/creators", async (CreatorService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(null, cancellationToken)));

        app.MapGet("/api/creators/{id:guid}", async (Guid id, CreatorService service,
            CancellationToken cancellationToken) =>
        {
            var creator = await service.GetAsync(id, cancellationToken);
            return creator is null ? Results.NotFound() : Results.Ok(creator);
        });

        app.MapGet("/api/rankings/daily/{date}", async (string date, CreatorService service,
            CancellationToken cancellationToken) =>
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
                throw new ApiProblemException(StatusCodes.Status400BadRequest, "Invalid date",
                    "Use a ranking date in yyyy-MM-dd format.");
            return Results.Ok(await service.GetAllAsync(parsed, cancellationToken));
        });

        if (!app.Environment.IsDevelopment()) return;

        app.MapPost("/api/creators", async (HttpRequest request, CreatorService service,
            CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
                throw new ApiProblemException(StatusCodes.Status400BadRequest, "Invalid request",
                    "Creator entries must use multipart form data.");
            var form = await request.ReadFormAsync(cancellationToken);
            if (!Guid.TryParse(form["entryReference"], out var entryReference) ||
                !decimal.TryParse(form["initialAmount"], NumberStyles.Number, CultureInfo.InvariantCulture,
                    out var amount))
                throw new ApiProblemException(StatusCodes.Status400BadRequest, "Invalid request",
                    "A valid entry reference and amount are required.");

            var command = new CreateCreatorCommand(entryReference, form["name"].ToString(),
                form["username"].ToString(), form["category"].ToString(), amount,
                InputRules.SocialProfilesFromJson(form["socialProfilesJson"].ToString()),
                form.Files.GetFile("image"));
            var creator = await service.CreateAsync(command, cancellationToken);
            return Results.Created($"/api/creators/{creator.Id}",
                new EntryCheckoutResult(creator.Id,
                    new CheckoutSession($"mock-entry:{entryReference:N}", true)));
        }).DisableAntiforgery();

        app.MapPut("/api/creators/{id:guid}", async (Guid id, UpdateCreatorRequest request,
            CreatorService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.UpdateAsync(id, request, cancellationToken)));

        app.MapDelete("/api/creators/{id:guid}", async (Guid id, CreatorService service,
            CancellationToken cancellationToken) =>
            await service.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());
    }
}
