using CrownRank.Domain.Common;
using CrownRank.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.Tests
{
    public sealed class AgreementAcceptanceTests
    {
        private static readonly DateTimeOffset AcceptedAtUtc =
            new(
                2026,
                9,
                14,
                12,
                0,
                0,
                TimeSpan.Zero);

        [Fact]
        public void Create_WithValidValues_CreatesAcceptance()
        {
            var acceptance =
                AgreementAcceptance.Create(
                    " terms-v1 ",
                    " privacy-v1 ",
                    " rules-v1 ",
                    AcceptedAtUtc);

            Assert.Equal(
                "terms-v1",
                acceptance.TermsVersion);

            Assert.Equal(
                "privacy-v1",
                acceptance.PrivacyPolicyVersion);

            Assert.Equal(
                "rules-v1",
                acceptance.RulesVersion);

            Assert.Equal(
                AcceptedAtUtc,
                acceptance.AcceptedAtUtc);
        }

        [Theory]
        [InlineData("", "privacy-v1", "rules-v1")]
        [InlineData("terms-v1", "", "rules-v1")]
        [InlineData("terms-v1", "privacy-v1", "")]
        [InlineData(" ", "privacy-v1", "rules-v1")]
        [InlineData("terms-v1", " ", "rules-v1")]
        [InlineData("terms-v1", "privacy-v1", " ")]
        public void Create_WhenVersionIsMissing_Throws(
            string terms,
            string privacy,
            string rules)
        {
            Assert.Throws<DomainException>(
                () =>
                    AgreementAcceptance.Create(
                        terms,
                        privacy,
                        rules,
                        AcceptedAtUtc));
        }

        [Fact]
        public void Create_WhenTermsVersionTooLong_Throws()
        {
            var version =
                new string('A', 51);

            Assert.Throws<DomainException>(
                () =>
                    AgreementAcceptance.Create(
                        version,
                        "privacy-v1",
                        "rules-v1",
                        AcceptedAtUtc));
        }

        [Fact]
        public void Create_WhenPrivacyVersionTooLong_Throws()
        {
            var version =
                new string('A', 51);

            Assert.Throws<DomainException>(
                () =>
                    AgreementAcceptance.Create(
                        "terms-v1",
                        version,
                        "rules-v1",
                        AcceptedAtUtc));
        }

        [Fact]
        public void Create_WhenRulesVersionTooLong_Throws()
        {
            var version =
                new string('A', 51);

            Assert.Throws<DomainException>(
                () =>
                    AgreementAcceptance.Create(
                        "terms-v1",
                        "privacy-v1",
                        version,
                        AcceptedAtUtc));
        }
    }
}
