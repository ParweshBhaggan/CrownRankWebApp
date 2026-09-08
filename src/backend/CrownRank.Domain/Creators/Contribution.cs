namespace CrownRank.Domain.Creators;

public enum ContributionKind { RankUp, Boost }

public sealed class Contribution
{
    private Contribution() { }
    internal Contribution(Guid id, Guid creatorId, decimal amount, ContributionKind kind, DateTimeOffset confirmedAt, string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("Payment reference is required.", nameof(reference));
        Money.Validate(amount);
        Id = id; CreatorId = creatorId; Amount = amount; Kind = kind; ConfirmedAt = confirmedAt; PaymentReference = reference;
    }
    public Guid Id { get; private set; }
    public Guid CreatorId { get; private set; }
    public decimal Amount { get; private set; }
    public ContributionKind Kind { get; private set; }
    public DateTimeOffset ConfirmedAt { get; private set; }
    public string PaymentReference { get; private set; } = string.Empty;
}

