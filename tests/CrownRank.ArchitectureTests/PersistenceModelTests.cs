using CrownRank.Domain.Creators;
using CrownRank.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CrownRank.ArchitectureTests;

public sealed class PersistenceModelTests
{
    private static CrownRankDbContext Context() => new(new DbContextOptionsBuilder<CrownRankDbContext>()
        .UseNpgsql("Host=localhost;Database=model_tests;Username=test;Password=test")
        .Options);

    [Fact]
    public void Money_is_mapped_as_decimal_with_two_fractional_digits()
    {
        using var context = Context();
        var model = context.Model;
        var contributionAmount = model.FindEntityType(typeof(Contribution))!.FindProperty(nameof(Contribution.Amount))!;
        var openingAmount = model.FindEntityType(typeof(Creator))!.FindProperty(nameof(Creator.OpeningAmount))!;

        Assert.Equal(18, contributionAmount.GetPrecision());
        Assert.Equal(2, contributionAmount.GetScale());
        Assert.Equal(18, openingAmount.GetPrecision());
        Assert.Equal(2, openingAmount.GetScale());
    }

    [Fact]
    public void Creator_model_contains_no_location_data()
    {
        using var context = Context();
        var properties = context.Model.FindEntityType(typeof(Creator))!.GetProperties().Select(x => x.Name);
        Assert.DoesNotContain(properties, name => name.Contains("Location", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(nameof(Creator.Name), properties);
        Assert.DoesNotContain("FirstName", properties);
        Assert.DoesNotContain("LastName", properties);
    }

    [Fact]
    public void Idempotency_and_identity_columns_have_unique_indexes()
    {
        using var context = Context();
        var creator = context.Model.FindEntityType(typeof(Creator))!;
        var contribution = context.Model.FindEntityType(typeof(Contribution))!;

        Assert.Contains(creator.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(Creator.Username));
        Assert.Contains(creator.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(Creator.EntryReference));
        Assert.Contains(contribution.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(Contribution.PaymentReference));
    }
}
