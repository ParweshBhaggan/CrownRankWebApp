namespace CrownRank.Api.Contracts;

public sealed record SocialProfileInput(string Platform, string Url);

public sealed record CreateCreatorCommand(
    Guid EntryReference,
    string Name,
    string Username,
    string Category,
    decimal InitialAmount,
    IReadOnlyList<SocialProfileInput> SocialProfiles,
    IFormFile? Image);

public sealed record UpdateCreatorRequest(
    string Name,
    string Username,
    string Category,
    IReadOnlyList<SocialProfileInput> SocialProfiles);

public sealed record CheckoutRequest(
    Guid ReferenceId,
    Guid CreatorId,
    string Purpose,
    decimal Amount,
    string Currency);

public sealed record CheckoutSession(string Id, bool Confirmed);

public sealed record EntryCheckoutResult(Guid CreatorId, CheckoutSession Session);

public sealed record SocialProfileDto(Guid Id, string Platform, string Url);

public sealed record CreatorDto(
    Guid Id,
    string Username,
    string Name,
    string Category,
    string ImageUrl,
    IReadOnlyList<SocialProfileDto> SocialProfiles,
    decimal TotalContributed,
    decimal DailyContributed,
    DateTimeOffset ScoreReachedAt,
    DateTimeOffset JoinedAt);
