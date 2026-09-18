using CrownRank.Application.Contracts;

namespace CrownRank.Api;

public sealed record BoostRequest(long AmountInMinorUnits, string Currency);
public sealed record AdminLoginRequest(string Password);
public sealed record CategoryRequest(string Name, string? Description);
public sealed record EntryDetailsRequest(string Name, string Username);
public sealed record EntryCategoryRequest(Guid CategoryId);
public sealed record SocialLinksRequest(IReadOnlyList<SocialLinkInput> Links);
public sealed record MockOutcomeRequest(string Outcome);

public sealed record CategoryResponse(Guid Id, string Name, string? Description);
public sealed record SocialLinkResponse(string Platform, string Url, string? CustomPlatformName);
public sealed record EntryResponse(Guid Id, string Name, string Username, Guid CategoryId, string ImageUrl,
    IReadOnlyList<SocialLinkResponse> SocialLinks);
public sealed record PaymentResponse(Guid AttemptId, Guid EntryId, string Purpose, string State,
    long AmountInMinorUnits, string Currency, string? CheckoutUrl);
