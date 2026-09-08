namespace CrownRank.Domain.Creators;

public static class Money
{
    public const decimal MaximumContribution = 10_000m;
    public static void Validate(decimal amount)
    {
        if (amount < 1m || amount > MaximumContribution || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Choose an amount from $1.00 to $10,000.00 with at most two decimal places.", nameof(amount));
    }
}
