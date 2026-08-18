using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Economy
{
    /// <summary>
    /// Static facade over the game's economy service.
    /// </summary>
    /// <remarks>
    /// A convenience layer, not a second implementation: every member forwards to the
    /// installed <see cref="IEconomyService"/>. Call sites scattered through gameplay code
    /// get one entry point, while the service stays injectable and testable.
    /// </remarks>
    public static class UniEconomy
    {
        private static IEconomyService _service;

        /// <summary>
        /// Gets the installed service, or null before initialization.
        /// </summary>
        public static IEconomyService Service => _service;

        /// <summary>
        /// Indicates whether the service is initialized and economy content is loaded.
        /// </summary>
        public static bool IsReady => _service != null && _service.IsReady;

        /// <summary>
        /// Gets the ids of every economy defined in content.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<string> EconomyIds
            => _service?.GetEconomyIds() ?? Array.Empty<string>();

        /// <summary>
        /// Gets the currently selected economy's id, or null.
        /// </summary>
        public static string SelectedEconomyId => _service?.SelectedEconomyId;

        /// <summary>
        /// Gets a read-only view of the selected economy, or null when none is loaded.
        /// </summary>
        public static EconomySnapshot Snapshot => _service?.GetSnapshot() ?? default;

        /// <summary>
        /// Installs a service and loads content and progress.
        /// </summary>
        /// <param name="service">The service to install.</param>
        /// <param name="cToken">Token to cancel initialization.</param>
        /// <exception cref="ArgumentNullException">Thrown when the service is null.</exception>
        public static async UniTask InitializeAsync(IEconomyService service,
            CancellationToken cToken = default)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            _service = service;

            await _service.InitializeAsync(cToken);
        }

        /// <summary>
        /// Selects the active economy for the UI.
        /// </summary>
        /// <param name="economyId">The economy to select.</param>
        /// <returns><c>true</c> when the economy exists.</returns>
        public static bool SelectEconomy(string economyId)
            => _service != null && _service.SelectEconomy(economyId);

        /// <summary>
        /// Converts one currency into another at a rule's rate.
        /// </summary>
        /// <param name="economyId">The economy holding the rule; uses the selected
        /// economy when null.</param>
        /// <param name="ruleId">The exchange rule id.</param>
        /// <param name="amount">How much source currency to hand in.</param>
        /// <param name="exchangeId">Idempotency id; a repeat of the same id is a no-op.</param>
        /// <param name="cToken">Token to cancel the exchange.</param>
        public static UniTask<ExchangeResult> ExchangeAsync(string economyId, string ruleId,
            int amount, string exchangeId, CancellationToken cToken = default)
            => _service != null
                ? _service.ExchangeAsync(economyId, ruleId, amount, exchangeId, cToken)
                : UniTask.FromResult(ExchangeResult.Invalid);

        /// <summary>
        /// Charges a purchase's costs and grants its rewards.
        /// </summary>
        /// <param name="economyId">The economy holding the purchase; uses the selected
        /// economy when null.</param>
        /// <param name="purchaseId">The purchase id.</param>
        /// <param name="purchaseKey">Idempotency id; a repeat of the same key is a no-op.</param>
        /// <param name="cToken">Token to cancel the purchase.</param>
        public static UniTask<PurchaseResult> PurchaseAsync(string economyId, string purchaseId,
            string purchaseKey, CancellationToken cToken = default)
            => _service != null
                ? _service.PurchaseAsync(economyId, purchaseId, purchaseKey, cToken)
                : UniTask.FromResult(PurchaseResult.Invalid);

        /// <summary>
        /// Retries purchases whose rewards failed to deliver.
        /// </summary>
        /// <param name="cToken">Token to cancel the refresh.</param>
        public static UniTask RefreshAsync(CancellationToken cToken = default)
            => _service != null ? _service.RefreshAsync(cToken) : UniTask.CompletedTask;

        /// <summary>
        /// Detaches the installed service.
        /// </summary>
        public static void Reset()
        {
            _service?.Reset();
            _service = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain reloads keep static state unless it is explicitly cleared; without
            // this, an editor recompile or a play-mode exit leaves a stale service behind.
            _service = null;
        }
    }
}
