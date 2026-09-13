using CrownRank.Domain.Models;

namespace CrownRank.Application.Contracts;

public sealed record SocialLinkInput(SocialMediaPlatform Platform, string Url, string? CustomPlatformName = null);
public sealed record SubmitEntryRequest(string Name, string Username, Guid CategoryId,
    Stream Image, string ImageFileName, IReadOnlyList<SocialLinkInput> Links,
    bool AcceptedAgreements, long AmountInMinorUnits, string Currency);
public sealed record PaymentStart(Guid AttemptId, Guid EntryId, string CheckoutUrl);
