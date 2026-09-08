using CrownRank.Domain.Creators;

namespace CrownRank.Application.Creators;

public sealed record SocialProfileInput(SocialPlatform Platform, string Url);
public sealed record CreateCreatorCommand(string FirstName, string LastName, string Username, CreatorCategory Category,
    Guid EntryReference, IReadOnlyList<SocialProfileInput> SocialProfiles, decimal InitialAmount, Stream? Image,
    string? ImageFileName, string? ImageContentType, long ImageLength);
public sealed record SocialProfileDto(Guid Id, string Platform, string Url);
public sealed record CreatorDto(Guid Id, string Username, string FirstName, string LastName, string DisplayName,
    string Category, string ImageUrl, IReadOnlyList<SocialProfileDto> SocialProfiles,
    decimal TotalContributed, decimal DailyContributed, DateTimeOffset JoinedAt, DateTimeOffset ScoreReachedAt);

