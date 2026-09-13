using CrownRank.Domain.Common;
using CrownRank.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.Tests
{
    public sealed class MoneyTests
    {
        [Fact]
        public void Create_WithValidAmount_CreatesMoney()
        {
            var money =
                Money.Create(
                    2500,
                    "eur");

            Assert.Equal(
                2500,
                money.AmountInMinorUnits);

            Assert.Equal(
                "EUR",
                money.Currency);
        }

        [Fact]
        public void Create_TrimsAndNormalizesCurrency()
        {
            var money =
                Money.Create(
                    100,
                    " eur ");

            Assert.Equal(
                "EUR",
                money.Currency);
        }

        [Fact]
        public void Create_WithZero_IsAllowed()
        {
            var money =
                Money.Create(
                    0,
                    "EUR");

            Assert.Equal(
                0,
                money.AmountInMinorUnits);
        }

        [Fact]
        public void Create_WithNegativeAmount_Throws()
        {
            Assert.Throws<DomainException>(
                () =>
                    Money.Create(
                        -1,
                        "EUR"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("EU")]
        [InlineData("EURO")]
        [InlineData("E1R")]
        public void Create_WithInvalidCurrency_Throws(
            string currency)
        {
            Assert.Throws<DomainException>(
                () =>
                    Money.Create(
                        100,
                        currency));
        }

        [Fact]
        public void Zero_CreatesZeroMoney()
        {
            var money =
                Money.Zero("EUR");

            Assert.Equal(
                0,
                money.AmountInMinorUnits);

            Assert.Equal(
                "EUR",
                money.Currency);
        }

        [Fact]
        public void Add_WithSameCurrency_AddsAmounts()
        {
            var first =
                Money.Create(
                    2500,
                    "EUR");

            var second =
                Money.Create(
                    1000,
                    "eur");

            var result =
                first.Add(second);

            Assert.Equal(
                3500,
                result.AmountInMinorUnits);

            Assert.Equal(
                "EUR",
                result.Currency);
        }

        [Fact]
        public void Add_WithDifferentCurrency_Throws()
        {
            var euros =
                Money.Create(
                    100,
                    "EUR");

            var dollars =
                Money.Create(
                    100,
                    "USD");

            Assert.Throws<DomainException>(
                () =>
                    euros.Add(dollars));
        }

        [Fact]
        public void Add_WithNull_Throws()
        {
            var money =
                Money.Create(
                    100,
                    "EUR");

            Assert.Throws<ArgumentNullException>(
                () =>
                    money.Add(null!));
        }

        [Fact]
        public void Add_WhenAmountOverflows_Throws()
        {
            var first =
                Money.Create(
                    long.MaxValue,
                    "EUR");

            var second =
                Money.Create(
                    1,
                    "EUR");

            Assert.Throws<OverflowException>(
                () =>
                    first.Add(second));
        }
    }
}
