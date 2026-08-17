using System;
using UniTx.Currency;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// The default <see cref="ISeasonPassWallet"/>: spends through the entity-based
    /// currency system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replaces the old refuse-everything default. When the kit's currency package is
    /// installed and its service is registered, the season pass charges the configured
    /// currency ids through it automatically — no game code required. A game that already
    /// owns a different economy can still bind its own <see cref="ISeasonPassWallet"/>.
    /// </para>
    /// </remarks>
    public sealed class SeasonPassCurrencyWallet : ISeasonPassWallet
    {
        private readonly ICurrencyService _currency;

        /// <summary>
        /// Creates the wallet.
        /// </summary>
        /// <param name="currency">The currency service to charge.</param>
        public SeasonPassCurrencyWallet(ICurrencyService currency)
        {
            _currency = currency ?? throw new ArgumentNullException(nameof(currency));
        }

        /// <inheritdoc />
        /// <remarks>
        /// Reads through <see cref="ICurrencyService.TryGetBalance"/>, not
        /// <c>GetBalance</c>: an unknown currency is a zero balance here, never an
        /// exception. UI reads a tier-skip price before content has loaded, and the wallet
        /// this replaced answered that with zero.
        /// </remarks>
        public int GetBalance(string currencyId) =>
            _currency.TryGetBalance(currencyId, out var balance) ? balance : 0;

        /// <inheritdoc />
        public bool TrySpend(string currencyId, int amount) =>
            _currency.TrySpend(currencyId, amount);
    }
}
