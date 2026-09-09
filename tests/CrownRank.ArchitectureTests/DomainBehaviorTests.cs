using CrownRank.Domain.Creators;
using Xunit;

namespace CrownRank.ArchitectureTests;

public sealed class DomainBehaviorTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.001")]
    [InlineData("10000.01")]
    public void Money_rejects_invalid_amounts(string value) =>
        Assert.Throws<ArgumentException>(() => Money.Validate(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("1")]
    [InlineData("12.50")]
    [InlineData("10000")]
    public void Money_accepts_supported_decimal_dollar_amounts(string value) =>
        Money.Validate(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public void Creator_normalizes_names_and_username()
    {
        var creator = new Creator(Guid.NewGuid(), " Ada ", " Lovelace ", " @Ada.Dev ", CreatorCategory.Technology,
            "/avatar.svg", null, TestData.Now);

        Assert.Equal("Ada", creator.FirstName);
        Assert.Equal("Lovelace", creator.LastName);
        Assert.Equal("ada.dev", creator.Username);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("spaces are invalid")]
    [InlineData("emoji-👑")]
    public void Creator_rejects_invalid_usernames(string username) =>
        Assert.Throws<ArgumentException>(() => new Creator(Guid.NewGuid(), "Ada", "Lovelace", username,
            CreatorCategory.Technology, "/avatar.svg", null, TestData.Now));

    [Fact]
    public void Contribution_reference_is_idempotent()
    {
        var creator = TestData.Creator();
        creator.AddContribution(2.50m, ContributionKind.Boost, TestData.Now, "same-reference");
        creator.AddContribution(2.50m, ContributionKind.Boost, TestData.Now, "same-reference");

        Assert.Single(creator.Contributions);
    }

    [Theory]
    [InlineData("instagram.com/ada")]
    [InlineData("http://instagram.com/ada")]
    [InlineData("https://instagram.com.evil.example/ada")]
    [InlineData("https://user:password@instagram.com/ada")]
    public void Social_profile_rejects_unsafe_or_mismatched_urls(string url) =>
        Assert.Throws<ArgumentException>(() => TestData.Creator().AddSocialProfile(SocialPlatform.Instagram, url));

    [Fact]
    public void Social_profile_accepts_subdomains_and_prevents_duplicate_platforms()
    {
        var creator = TestData.Creator();
        creator.AddSocialProfile(SocialPlatform.Instagram, "https://www.instagram.com/ada");

        Assert.Throws<ArgumentException>(() =>
            creator.AddSocialProfile(SocialPlatform.Instagram, "https://instagram.com/another"));
    }

    [Fact]
    public void Website_accepts_any_https_host()
    {
        var creator = TestData.Creator();
        creator.AddSocialProfile(SocialPlatform.Website, "https://example.com/ada");
        Assert.Single(creator.SocialProfiles);
    }

    [Fact]
    public void Confirmed_entry_reference_cannot_be_reassigned()
    {
        var creator = TestData.Creator();
        creator.AddContribution(10m, ContributionKind.RankUp, TestData.Now, "opening");
        Assert.Throws<InvalidOperationException>(() => creator.RecoverEntry(Guid.NewGuid()));
    }
}
