using CrownRank.Domain.Creators;

namespace CrownRank.Application.Creators;

public sealed record SocialProfileInput(SocialPlatform Platform, string Url);
public sealed record CreateCreatorCommand(string FirstName, string LastName, string Username, CreatorCategory Category,
    string? Location, IReadOnlyList<SocialProfileInput> SocialProfiles, long InitialAmountCents, Stream? Image,
    string? ImageFileName, string? ImageContentType, long ImageLength);
public sealed record SocialProfileDto(Guid Id, string Platform, string Url);
public sealed record CreatorDto(Guid Id, string Username, string FirstName, string LastName, string DisplayName,
    string Category, string? Location, string ImageUrl, IReadOnlyList<SocialProfileDto> SocialProfiles,
    long TotalContributedCents, long DailyContributedCents, DateTimeOffset JoinedAt);
