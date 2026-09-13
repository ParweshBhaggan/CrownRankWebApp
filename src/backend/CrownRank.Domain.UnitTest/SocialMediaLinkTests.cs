using CrownRank.Domain.Common;
using CrownRank.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.Tests
{
    public sealed class SocialMediaLinkTests
    {
        [Fact]
        public void Create_WithValidLink_CreatesLink()
        {
            var link =
                SocialMediaLink.Create(
                    SocialMediaPlatform.Instagram,
                    " https://instagram.com/alice ");

            Assert.Equal(
                SocialMediaPlatform.Instagram,
                link.Platform);

            Assert.Equal(
                "https://instagram.com/alice",
                link.Url);

            Assert.Null(
                link.CustomPlatformName);
        }

        [Fact]
        public void Create_OtherPlatform_WithCustomName_CreatesLink()
        {
            var link =
                SocialMediaLink.Create(
                    SocialMediaPlatform.Other,
                    "https://kick.com/alice",
                    " Kick ");

            Assert.Equal(
                SocialMediaPlatform.Other,
                link.Platform);

            Assert.Equal(
                "Kick",
                link.CustomPlatformName);
        }

        [Fact]
        public void Create_OtherPlatform_WithoutCustomName_Throws()
        {
            Assert.Throws<DomainException>(
                () =>
                    SocialMediaLink.Create(
                        SocialMediaPlatform.Other,
                        "https://example.com"));
        }

        [Fact]
        public void Create_NonOtherPlatform_WithCustomName_Throws()
        {
            Assert.Throws<DomainException>(
                () =>
                    SocialMediaLink.Create(
                        SocialMediaPlatform.Instagram,
                        "https://instagram.com/alice",
                        "Something"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("not-a-url")]
        public void Create_WithInvalidUrl_Throws(
            string url)
        {
            Assert.Throws<DomainException>(
                () =>
                    SocialMediaLink.Create(
                        SocialMediaPlatform.Instagram,
                        url));
        }

        [Fact]
        public void Create_WithHttpUrl_Throws()
        {
            Assert.Throws<DomainException>(
                () =>
                    SocialMediaLink.Create(
                        SocialMediaPlatform.Instagram,
                        "http://instagram.com/alice"));
        }

        [Fact]
        public void Create_WithHttpsUrl_IsAllowed()
        {
            var link =
                SocialMediaLink.Create(
                    SocialMediaPlatform.Instagram,
                    "https://instagram.com/alice");

            Assert.Equal(
                "https://instagram.com/alice",
                link.Url);
        }

        [Fact]
        public void Create_WithCredentialsInUrl_Throws()
        {
            Assert.Throws<DomainException>(
                () =>
                    SocialMediaLink.Create(
                        SocialMediaPlatform.Website,
                        "https://username:password@example.com"));
        }

        [Fact]
        public void Create_WithInvalidPlatform_Throws()
        {
            var invalidPlatform =
                (SocialMediaPlatform)999;

            Assert.Throws<DomainException>(
                () =>
                    SocialMediaLink.Create(
                        invalidPlatform,
                        "https://example.com"));
        }

        [Fact]
        public void Create_WithUrlLongerThan2048Characters_Throws()
        {
            var url =
                "https://example.com/" +
                new string('a', 2050);

            Assert.Throws<DomainException>(
                () =>
                    SocialMediaLink.Create(
                        SocialMediaPlatform.Website,
                        url));
        }
    }
}
