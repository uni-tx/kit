using System;
using System.Collections.Generic;
using UniTx.Content;
using UnityEngine;

namespace UniTx.Economy
{
    /// <summary>
    /// One named economy: the currencies in it, how they exchange, and what can be bought.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Static, content-defined, and one per economy — a game can ship any number of them:
    /// a core economy, a meta economy, a seasonal economy, one per game mode. Each is an
    /// isolated group of currencies with its own exchange rules and purchases, so the
    /// core loop can never be flooded by a seasonal event and vice versa.
    /// </para>
    /// <para>
    /// Currencies themselves are separate <c>com.uni-tx.currency</c> content items; this
    /// data groups them by id and adds the rules on top. The economy service creates one
    /// <see cref="EconomyEntity"/> per economy when it is first touched, so any number of
    /// economies can coexist and each keeps its own save.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class EconomyData : IData
    {
        [Tooltip("Unique economy id. Referenced by content, saves and telemetry.")]
        [SerializeField] private string _id;

        [Tooltip("Player-facing name, or a localization key.")]
        [SerializeField] private string _displayName;

        [Tooltip("The currency ids that belong to this economy, in display order.")]
        [SerializeField] private List<string> _currencyIds = new();

        [Tooltip("How currencies in this economy convert into each other.")]
        [SerializeField] private List<ExchangeRuleData> _exchangeRules = new();

        [Tooltip("What a player can buy with this economy's currencies.")]
        [SerializeField] private List<PurchaseData> _purchases = new();

        /// <inheritdoc />
        public string Id => _id;

        /// <summary>
        /// Gets the player-facing name, or localization key.
        /// </summary>
        public string Name => _displayName;

        /// <summary>
        /// Gets the currency ids that belong to this economy, in display order.
        /// </summary>
        public IReadOnlyList<string> CurrencyIds => _currencyIds;

        /// <summary>
        /// Gets this economy's exchange rules.
        /// </summary>
        public IReadOnlyList<ExchangeRuleData> ExchangeRules => _exchangeRules;

        /// <summary>
        /// Gets this economy's purchases.
        /// </summary>
        public IReadOnlyList<PurchaseData> Purchases => _purchases;

        /// <summary>
        /// Finds an exchange rule by id.
        /// </summary>
        /// <param name="ruleId">The rule id.</param>
        public ExchangeRuleData GetExchangeRule(string ruleId)
        {
            foreach (var rule in _exchangeRules)
            {
                if (rule != null && string.Equals(rule.Id, ruleId, StringComparison.Ordinal))
                {
                    return rule;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a purchase by id.
        /// </summary>
        /// <param name="purchaseId">The purchase id.</param>
        public PurchaseData GetPurchase(string purchaseId)
        {
            foreach (var purchase in _purchases)
            {
                if (purchase != null &&
                    string.Equals(purchase.Id, purchaseId, StringComparison.Ordinal))
                {
                    return purchase;
                }
            }

            return null;
        }

    }
}
