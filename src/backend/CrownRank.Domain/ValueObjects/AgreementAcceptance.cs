using CrownRank.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.ValueObjects
{
    public sealed record AgreementAcceptance
    {
        public string TermsVersion { get; private init; } =
            string.Empty;

        public string PrivacyPolicyVersion { get; private init; } =
            string.Empty;

        public string RulesVersion { get; private init; } =
            string.Empty;

        public DateTimeOffset AcceptedAtUtc { get; private init; }

        private AgreementAcceptance()
        {
        }

        private AgreementAcceptance(
            string termsVersion,
            string privacyPolicyVersion,
            string rulesVersion,
            DateTimeOffset acceptedAtUtc)
        {
            TermsVersion = termsVersion;
            PrivacyPolicyVersion = privacyPolicyVersion;
            RulesVersion = rulesVersion;
            AcceptedAtUtc = acceptedAtUtc;
        }

        public static AgreementAcceptance Create(
            string termsVersion,
            string privacyPolicyVersion,
            string rulesVersion,
            DateTimeOffset acceptedAtUtc)
        {
            ValidateVersion(
                termsVersion,
                "Terms of Service");

            ValidateVersion(
                privacyPolicyVersion,
                "Privacy Policy");

            ValidateVersion(
                rulesVersion,
                "Rules");

            return new AgreementAcceptance(
                termsVersion.Trim(),
                privacyPolicyVersion.Trim(),
                rulesVersion.Trim(),
                acceptedAtUtc);
        }

        private static void ValidateVersion(
            string version,
            string documentName)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new DomainException(
                    $"{documentName} version is required.");
            }

            if (version.Trim().Length > 50)
            {
                throw new DomainException(
                    $"{documentName} version cannot exceed 50 characters.");
            }
        }
    }
}
