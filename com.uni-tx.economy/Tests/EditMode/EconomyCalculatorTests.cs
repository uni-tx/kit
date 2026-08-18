using NUnit.Framework;

namespace UniTx.Economy.Tests
{
    /// <summary>
    /// The pure rules: exchange output, bounds and purchase affordability.
    /// </summary>
    public sealed class EconomyCalculatorTests
    {
        [Test]
        public void ExchangeOutput_MultipliesByRate()
        {
            var rule = EconomyJson.Core().GetExchangeRule("coins_to_gems");

            var received = EconomyCalculator.ExchangeOutput(rule, 7);

            Assert.AreEqual(70, received);
        }

        [Test]
        public void ExchangeOutput_NullRuleOrNonPositive_IsZero()
        {
            Assert.AreEqual(0, EconomyCalculator.ExchangeOutput(null, 5));
            Assert.AreEqual(0, EconomyCalculator.ExchangeOutput(
                EconomyJson.Core().GetExchangeRule("coins_to_gems"), 0));
        }

        [Test]
        public void IsAmountInRange_RespectsMinimum()
        {
            var rule = EconomyJson.Core().GetExchangeRule("coins_to_gems");

            Assert.IsFalse(EconomyCalculator.IsAmountInRange(rule, 4));
            Assert.IsTrue(EconomyCalculator.IsAmountInRange(rule, 5));
        }

        [Test]
        public void CanAfford_AllCostLinesCovered()
        {
            var purchase = EconomyJson.Core().GetPurchase("power_up");
            var balances = new System.Collections.Generic.Dictionary<string, int>
            {
                ["gems"] = 3,
            };

            var affordable = EconomyCalculator.CanAfford(purchase, id =>
                balances.TryGetValue(id, out var held) ? held : 0);

            Assert.IsTrue(affordable);
        }

        [Test]
        public void CanAfford_Shortfall_IsNotAffordable()
        {
            var purchase = EconomyJson.Core().GetPurchase("power_up");
            var balances = new System.Collections.Generic.Dictionary<string, int>
            {
                ["gems"] = 2,
            };

            var affordable = EconomyCalculator.CanAfford(purchase, id =>
                balances.TryGetValue(id, out var held) ? held : 0);

            Assert.IsFalse(affordable);
        }

        [Test]
        public void FirstUnaffordableCost_NamesTheShortfall()
        {
            var purchase = EconomyJson.Core().GetPurchase("power_up");

            var failing = EconomyCalculator.FirstUnaffordableCost(purchase,
                id => id == "gems" ? 2 : 0);

            Assert.AreEqual("gems", failing);
        }

        [Test]
        public void FirstUnaffordableCost_NullWhenAffordable()
        {
            var purchase = EconomyJson.Core().GetPurchase("power_up");

            var failing = EconomyCalculator.FirstUnaffordableCost(purchase,
                id => id == "gems" ? 3 : 0);

            Assert.IsNull(failing);
        }
    }
}
