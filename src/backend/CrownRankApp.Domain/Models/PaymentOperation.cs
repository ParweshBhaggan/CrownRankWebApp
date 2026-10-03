namespace CrownRankApp.Domain.Models;

public enum PaymentPurpose { Entry, Boost }
public enum PaymentStatus { Pending, Processing, Paid, Failed, Expired }

public sealed class PaymentOperation : Entity
{
    public PaymentOperation() { }
    public PaymentOperation(Guid id) : base(id) { }
    public PaymentPurpose Purpose { get; set; }
    public PaymentStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "usd";
    public string RequestHash { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? EntryId { get; set; }
    public string? EntryJson { get; set; }
    public string? ReservedUsername { get; set; }
    public string? SessionId { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? PaymentIntentId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? FulfilledAt { get; set; }
    public DateTime? AgreementsAcceptedAt { get; set; }
    public string? TermsVersion { get; set; }
    public string? PrivacyVersion { get; set; }
}
