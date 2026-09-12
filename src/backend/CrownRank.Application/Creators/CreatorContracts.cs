using CrownRank.Application.Abstractions;
using CrownRank.Domain.Creators;

namespace CrownRank.Application.Creators;

public sealed record SocialProfileInput(SocialPlatform Platform, string Url);
public sealed record CreateCreatorCommand(string Name, string Username, CreatorCategory Category,
    Guid EntryReference, IReadOnlyList<SocialProfileInput> SocialProfiles, decimal InitialAmount, Stream? Image,
    string? ImageFileName, string? ImageContentType, long ImageLength);
public sealed record SocialProfileDto(Guid Id, string Platform, string Url);
public sealed record CreatorDto(Guid Id, string Username, string Name,
    string Category, string ImageUrl, IReadOnlyList<SocialProfileDto> SocialProfiles,
    decimal TotalContributed, decimal DailyContributed, DateTimeOffset JoinedAt, DateTimeOffset ScoreReachedAt);
public sealed record EntryCheckoutResult(Guid CreatorId, CheckoutSession Session);
public sealed record PaymentStatus(Guid CreatorId, bool Confirmed);
