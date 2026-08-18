using System;
using UnityEngine;

namespace UniTx.Economy
{
    /// <summary>
    /// One exchange rule: convert a source currency into a target currency at a fixed rate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Content-defined like everything else: rates are tuning, not code, so a patch can
    /// rebalance the economy without a rebuild. Rates are whole units — one source unit
    /// buys <see cref="Rate"/> target units. Fractional rates are a design smell in a
    /// wallet; games that need them can define a source amount instead.
    /// </para>
    /// <para>
    /// Bounds keep a rule honest: a minimum stops dust trades from spamming the ledger,
    /// and a maximum stops a player dumping a hoard in one click.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class ExchangeRuleData
    {
        [Tooltip("Unique rule id, referenced by the UI and telemetry.")]
        [SerializeField] private string _id;

        [Tooltip("The currency the player hands in.")]
        [SerializeField] private string _fromCurrencyId;

        [Tooltip("The currency the player receives.")]
        [SerializeField] private string _toCurrencyId;

        [Tooltip("How many target units one source unit buys. Must be at least 1.")]
        [SerializeField, Min(1)] private int _rate = 1;

        [Tooltip("Smallest amount a single exchange may convert. 0 means no minimum.")]
        [SerializeField, Min(0)] private int _minAmount;

        [Tooltip("Largest amount a single exchange may convert. 0 means no maximum.")]
        [SerializeField, Min(0)] private int _maxAmount;

        /// <summary>
        /// Gets the rule id.
        /// </summary>
        public string Id => _id;

        /// <summary>
        /// Gets the source currency id.
        /// </summary>
        public string FromCurrencyId => _fromCurrencyId;

        /// <summary>
        /// Gets the target currency id.
        /// </summary>
        public string ToCurrencyId => _toCurrencyId;

        /// <summary>
        /// Gets how many target units one source unit buys.
        /// </summary>
        public int Rate => _rate;

        /// <summary>
        /// Gets the smallest amount a single exchange may convert, or zero for no minimum.
        /// </summary>
        public int MinAmount => _minAmount;

        /// <summary>
        /// Gets the largest amount a single exchange may convert, or zero for no maximum.
        /// </summary>
        public int MaxAmount => _maxAmount;
    }
}
