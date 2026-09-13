using CrownRank.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.Models
{
    public sealed class SocialMediaLink : Entity
    {
        public SocialMediaPlatform Platform { get; private set; }

        public string Url { get; private set; } =
            string.Empty;

        public string? CustomPlatformName { get; private set; }

        private SocialMediaLink()
        {
        }

        private SocialMediaLink(
            SocialMediaPlatform platform,
            string url,
            string? customPlatformName)
        {
            Platform = platform;
            Url = url;
            CustomPlatformName = customPlatformName;
        }

        public static SocialMediaLink Create(
            SocialMediaPlatform platform,
            string url,
            string? customPlatformName = null)
        {
            Validate(
                platform,
                url,
                customPlatformName);

            return new SocialMediaLink(
                platform,
                url.Trim(),
                customPlatformName?.Trim());
        }

        private static void Validate(
            SocialMediaPlatform platform,
            string url,
            string? customPlatformName)
        {
            if (!Enum.IsDefined(
                    typeof(SocialMediaPlatform),
                    platform))
            {
                throw new DomainException(
                    "The social media platform is invalid.");
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                throw new DomainException(
                    "A social media URL is required.");
            }

            var normalizedUrl = url.Trim();

            if (normalizedUrl.Length > 2048)
            {
                throw new DomainException(
                    "Social media URL cannot exceed 2048 characters.");
            }

            if (!Uri.TryCreate(
                    normalizedUrl,
                    UriKind.Absolute,
                    out var uri))
            {
                throw new DomainException(
                    "The social media URL is invalid.");
            }

            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new DomainException(
                    "Social media links must use HTTPS.");
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new DomainException(
                    "Social media links cannot contain credentials.");
            }

            if (string.IsNullOrWhiteSpace(uri.Host))
            {
                throw new DomainException(
                    "The social media URL must contain a valid host.");
            }

            if (platform == SocialMediaPlatform.Other)
            {
                if (string.IsNullOrWhiteSpace(
                        customPlatformName))
                {
                    throw new DomainException(
                        "A platform name is required when using 'Other'.");
                }

                if (customPlatformName.Trim().Length > 50)
                {
                    throw new DomainException(
                        "Custom platform name cannot exceed 50 characters.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(
                         customPlatformName))
            {
                throw new DomainException(
                    "A custom platform name can only be used with 'Other'.");
            }
        }
    }
}
