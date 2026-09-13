using CrownRank.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.ValueObjects
{
    public sealed record Money
    {
        public long AmountInMinorUnits { get; private init; }

        public string Currency { get; private init; } = string.Empty;

        private Money()
        {
        }

        private Money(
            long amountInMinorUnits,
            string currency)
        {
            AmountInMinorUnits = amountInMinorUnits;
            Currency = currency;
        }

        public static Money Create(
            long amountInMinorUnits,
            string currency)
        {
            if (amountInMinorUnits < 0)
            {
                throw new DomainException(
                    "Money cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(currency))
            {
                throw new DomainException(
                    "Currency is required.");
            }

            var normalizedCurrency =
                currency.Trim().ToUpperInvariant();

            if (normalizedCurrency.Length != 3 ||
                !normalizedCurrency.All(char.IsLetter))
            {
                throw new DomainException(
                    "Currency must be a three-letter currency code.");
            }

            return new Money(
                amountInMinorUnits,
                normalizedCurrency);
        }

        public static Money Zero(string currency)
        {
            return Create(0, currency);
        }

        public Money Add(Money other)
        {
            ArgumentNullException.ThrowIfNull(other);

            EnsureSameCurrency(other);

            return Create(
                checked(
                    AmountInMinorUnits +
                    other.AmountInMinorUnits),
                Currency);
        }

        public void EnsureSameCurrency(Money other)
        {
            ArgumentNullException.ThrowIfNull(other);

            if (!string.Equals(
                    Currency,
                    other.Currency,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException(
                    "Money values must use the same currency.");
            }
        }
    }
}
