using System;

namespace UniTx.Economy
{
    /// <summary>
    /// Pure economy rules: exchange output, rate bounds and purchase affordability.
    /// </summary>
    /// <remarks>
    /// No I/O, no state of its own — everything is derived from the definitions and the
    /// balances passed in, so the rules can be unit-tested without the Unity engine.
    /// </remarks>
    public static class EconomyCalculator
    {
        /// <summary>
        /// Computes how much target currency an exchange of <paramref name="amount"/>
        /// source units yields under <paramref name="rule"/>.
        /// </summary>
        /// <param name="rule">The exchange rule.</param>
        /// <param name="amount">How much source currency is handed in.</param>
        /// <returns>The target currency received.</returns>
        public static int ExchangeOutput(ExchangeRuleData rule, int amount)
        {
            if (rule == null || amount <= 0 || rule.Rate < 0) return 0;

            return checked(amount * rule.Rate);
        }

        /// <summary>
        /// Validates an exchange amount against a rule's minimum/maximum bounds.
        /// </summary>
        /// <param name="rule">The exchange rule.</param>
        /// <param name="amount">How much source currency is handed in.</param>
        /// <returns><c>true</c> when the amount is within bounds.</returns>
        public static bool IsAmountInRange(ExchangeRuleData rule, int amount)
        {
            if (rule == null || amount <= 0) return false;

            if (rule.MinAmount > 0 && amount < rule.MinAmount) return false;

            if (rule.MaxAmount > 0 && amount > rule.MaxAmount) return false;

            return true;
        }

        /// <summary>
        /// Indicates whether the player can afford every cost line of a purchase.
        /// </summary>
        /// <param name="purchase">The purchase.</param>
        /// <param name="balanceOf">How much the player holds of a currency; unknown
        /// currencies read as zero.</param>
        public static bool CanAfford(PurchaseData purchase, Func<string, int> balanceOf)
            => FirstUnaffordableCost(purchase, balanceOf) == null;

        /// <summary>
        /// Finds the balance shortfall, if any, of a purchase's costs.
        /// </summary>
        /// <param name="purchase">The purchase.</param>
        /// <param name="balanceOf">How much the player holds of a currency.</param>
        /// <returns>The first currency id the player cannot cover, or null when affordable.</returns>
        public static string FirstUnaffordableCost(PurchaseData purchase,
            Func<string, int> balanceOf)
        {
            if (purchase == null || balanceOf == null) return null;

            foreach (var cost in purchase.Costs)
            {
                if (cost == null || string.IsNullOrEmpty(cost.CurrencyId) || cost.Amount <= 0) continue;

                if (balanceOf(cost.CurrencyId) < cost.Amount) return cost.CurrencyId;
            }

            return null;
        }
    }
}
