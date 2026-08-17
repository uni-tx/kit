using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;

namespace UniTx.Currency
{
    /// <summary>
    /// Reads and mutates player currency balances through the registered
    /// <see cref="Currency"/> entities.
    /// </summary>
    /// <remarks>
    /// Currencies are content-driven entities: load content first (the entity service builds
    /// one <see cref="Currency"/> per <see cref="CurrencyData"/>), then use this service to
    /// read balances, spend and grant. Balances persist through the entity's saved data.
    /// </remarks>
    public interface ICurrencyService : IInjectable, IInitializableAsync, IResettable
    {
        /// <summary>
        /// Indicates whether the service is initialized and currency entities are loaded.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Returns the player's balance of a currency.
        /// </summary>
        /// <param name="currencyId">The currency to read.</param>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// Thrown when no currency with that id is registered.
        /// </exception>
        int GetBalance(string currencyId);

        /// <summary>
        /// Returns the player's balance of a currency, without throwing.
        /// </summary>
        /// <param name="currencyId">The currency to read.</param>
        /// <param name="balance">The balance, or zero when the currency is unknown.</param>
        /// <returns><c>true</c> when the currency is registered.</returns>
        bool TryGetBalance(string currencyId, out int balance);

        /// <summary>
        /// Deducts a cost if the player can afford it.
        /// </summary>
        /// <param name="currencyId">The currency to charge.</param>
        /// <param name="amount">How much to deduct.</param>
        /// <returns><c>true</c> when the charge went through.</returns>
        /// <remarks>
        /// Must be atomic: returning <c>true</c> without deducting hands out free currency,
        /// and deducting while returning <c>false</c> charges for nothing.
        /// </remarks>
        bool TrySpend(string currencyId, int amount);

        /// <summary>
        /// Adds to a balance, honouring the currency's maximum, with idempotent delivery.
        /// </summary>
        /// <param name="currencyId">The currency to grant.</param>
        /// <param name="amount">How much to add.</param>
        /// <param name="grantId">Idempotency id; a repeat of the same id is ignored.</param>
        /// <param name="cToken">Token to cancel the grant.</param>
        /// <returns>What happened, including whether the cap trimmed the amount.</returns>
        UniTask<CurrencyGrantResult> GrantAsync(string currencyId, int amount, string grantId = null,
            CancellationToken cToken = default);
    }
}
