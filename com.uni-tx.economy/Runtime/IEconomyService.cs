using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;

namespace UniTx.Economy
{
    /// <summary>
    /// Reads and mutates any number of named economies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An economy is a content-defined group of currencies with exchange rules and
    /// purchases. Balances themselves live in <c>com.uni-tx.currency</c>; this service is
    /// the rules on top — which currencies belong together, how they convert, and what a
    /// player can buy with them.
    /// </para>
    /// <para>
    /// Every mutating operation is idempotent: pass an exchange id or purchase key and a
    /// replay of the same operation is a no-op, so a retried request cannot move currency
    /// twice.
    /// </para>
    /// </remarks>
    public interface IEconomyService : IInjectable, IInitializableAsync, IResettable
    {
        /// <summary>
        /// Indicates whether the service is initialized and economy content is loaded.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Gets the ids of every economy defined in content.
        /// </summary>
        IReadOnlyList<string> GetEconomyIds();

        /// <summary>
        /// Returns the currently selected economy's id, or null when none is selected.
        /// </summary>
        string SelectedEconomyId { get; }

        /// <summary>
        /// Selects the active economy for the facade and UI.
        /// </summary>
        /// <param name="economyId">The economy to select.</param>
        /// <returns><c>true</c> when the economy exists.</returns>
        bool SelectEconomy(string economyId);

        /// <summary>
        /// Returns a read-only view of one economy.
        /// </summary>
        /// <param name="economyId">The economy to snapshot. Uses the selected economy
        /// when null.</param>
        EconomySnapshot GetSnapshot(string economyId = null);

        /// <summary>
        /// Converts one currency into another at a rule's rate.
        /// </summary>
        /// <param name="economyId">The economy holding the rule. Uses the selected
        /// economy when null.</param>
        /// <param name="ruleId">The exchange rule id.</param>
        /// <param name="amount">How much source currency to hand in.</param>
        /// <param name="exchangeId">Idempotency id; a repeat of the same id is a no-op.</param>
        /// <param name="cToken">Token to cancel the exchange.</param>
        UniTask<ExchangeResult> ExchangeAsync(string economyId, string ruleId, int amount,
            string exchangeId, CancellationToken cToken = default);

        /// <summary>
        /// Charges a purchase's costs and grants its rewards.
        /// </summary>
        /// <param name="economyId">The economy holding the purchase. Uses the selected
        /// economy when null.</param>
        /// <param name="purchaseId">The purchase id.</param>
        /// <param name="purchaseKey">Idempotency id; a repeat of the same key is a no-op.</param>
        /// <param name="cToken">Token to cancel the purchase.</param>
        UniTask<PurchaseResult> PurchaseAsync(string economyId, string purchaseId,
            string purchaseKey, CancellationToken cToken = default);

        /// <summary>
        /// Retries purchases whose rewards failed to deliver.
        /// </summary>
        /// <param name="cToken">Token to cancel the refresh.</param>
        UniTask RefreshAsync(CancellationToken cToken = default);
    }
}
