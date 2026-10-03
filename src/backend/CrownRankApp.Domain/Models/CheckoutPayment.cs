namespace CrownRankApp.Domain.Models;

// Contains payment information only; the existing entry form and image stay unchanged.
public sealed class CheckoutPayment
{
    public Guid Id
    {
        get;
        set;
    }

    public string Name
    {
        get;
        set;
    } = string.Empty;

    public decimal Amount
    {
        get;
        set;
    }

    public string Currency
    {
        get;
        set;
    } = "usd";

    public string Purpose
    {
        get;
        set;
    } = "entry";

    public Guid? BoostEntryId
    {
        get;
        set;
    }

    public string? SessionId
    {
        get;
        set;
    }

    public DateTime CreatedAt
    {
        get;
        set;
    }

    public DateTime ExpiresAt
    {
        get;
        set;
    }

    public string FrontendUrl
    {
        get;
        set;
    } = string.Empty;

    public bool LiveMode
    {
        get;
        set;
    }

    public DateTime? PaidAt
    {
        get;
        set;
    }

    public Guid? FulfilledEntryId
    {
        get;
        set;
    }
}
