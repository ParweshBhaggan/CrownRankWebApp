using CrownRank.Application.Payments;
using CrownRank.Domain.Models;
using CrownRank.Domain.ValueObjects;

namespace CrownRank.Application.Abstractions;

public interface IEntryRepository
{
    Task<Entry?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Entry entry, CancellationToken cancellationToken = default);
}

public interface ICategoryRepository
{
    Task<Category?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Category category, CancellationToken cancellationToken = default);
}

public interface IContributionRepository
{
    Task<RankingContribution?> FindByPaymentAsync(string provider, string reference, CancellationToken cancellationToken = default);
    Task AddAsync(RankingContribution contribution, CancellationToken cancellationToken = default);
}

public interface IPaymentAttemptRepository
{
    Task<PaymentAttempt?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default);
}

// Implementations must commit all changes atomically and protect the same attempt from concurrent confirmation.
// A unique database constraint on (provider, reference) is also required for attempts and contributions.
public interface IUnitOfWork
{
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}

public sealed record CheckoutSession(string Provider, string Reference, string CheckoutUrl);
public enum PaymentFailure { Failed, Cancelled }
public sealed record VerifiedPayment(string Provider, string Reference, Money Amount, bool Succeeded,
    PaymentFailure? Failure = null);

public interface IPaymentGateway
{
    Task<CheckoutSession> CreateCheckoutAsync(Guid attemptId, Money expected, CancellationToken cancellationToken = default);
    // Must independently obtain the final state and amount from the provider; never accept client assertions.
    Task<VerifiedPayment> VerifyAsync(string provider, string reference, CancellationToken cancellationToken = default);
}

public interface IProfileImageStorage
{
    // Implementations validate file type/content, bound size, resize to at most 300x250 and return an opaque key.
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

public interface ILegalDocumentVersions
{
    Task<LegalVersions> CurrentAsync(CancellationToken cancellationToken = default);
}
public sealed record LegalVersions(string Terms, string Privacy, string Rules);

public interface IAdminAuthorization
{
    Task<bool> IsAuthorizedAsync(string credential, CancellationToken cancellationToken = default);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed record LeaderboardRow(Guid EntryId, string Name, string Username, Guid CategoryId,
    long ScoreInMinorUnits, string Currency, DateTimeOffset ScoreReachedAtUtc, int Rank);
public sealed record BoostPreview(Guid EntryId, string Name, string Username, int CurrentRank,
    long CurrentScoreInMinorUnits, long BoostAmountInMinorUnits, long ProjectedScoreInMinorUnits, string Currency);
public interface ILeaderboardQueries
{
    // Only published entries and active contributions; sort score descending, score-reached time ascending, entry ID ascending.
    Task<IReadOnlyList<LeaderboardRow>> GetGlobalAsync(Guid? categoryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaderboardRow>> GetDailyAsync(DateOnly utcDay, Guid? categoryId, CancellationToken cancellationToken = default);
}

public sealed record AdminEntryRow(Guid Id, string Name, string Username, Guid CategoryId, EntryStatus Status,
    DateTimeOffset CreatedAtUtc);
public sealed record AdminPaymentRow(Guid Id, Guid EntryId, PaymentPurpose Purpose, PaymentState State,
    long AmountInMinorUnits, string Currency, string? Provider, string? Reference);
public interface IAdminQueries
{
    Task<IReadOnlyList<AdminEntryRow>> EntriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Category>> CategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminPaymentRow>> PaymentsAsync(CancellationToken ct = default);
}
