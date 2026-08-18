using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Store
{
    /// <summary>
    /// Static facade over the game's store service.
    /// </summary>
    /// <remarks>
    /// A convenience layer, not a second implementation: every member forwards to the
    /// installed <see cref="IStoreService"/>. Call sites scattered through gameplay code
    /// get one entry point, while the service stays injectable and testable.
    /// </remarks>
    public static class UniStore
    {
        private static IStoreService _service;

        /// <summary>
        /// Gets the installed service, or null before initialization.
        /// </summary>
        public static IStoreService Service => _service;

        /// <summary>
        /// Indicates whether a store and the player's progress are loaded.
        /// </summary>
        public static bool IsReady => _service != null && _service.IsReady;

        /// <summary>
        /// Gets the active store definition, or null.
        /// </summary>
        public static StoreData Store => _service?.Store;

        /// <summary>
        /// Gets everything a shop screen needs in one value.
        /// </summary>
        public static StoreSnapshot Snapshot => _service?.Snapshot ?? default;

        /// <summary>
        /// Raised whenever the snapshot changes.
        /// </summary>
        public static event Action<StoreSnapshot> OnChanged;

        /// <summary>
        /// Installs a service and loads content and progress.
        /// </summary>
        /// <param name="service">The service to install.</param>
        /// <param name="cToken">Token to cancel initialization.</param>
        /// <exception cref="ArgumentNullException">Thrown when the service is null.</exception>
        public static async UniTask InitializeAsync(IStoreService service,
            CancellationToken cToken = default)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            // Detach the previous service first, or its change events keep driving a UI that
            // is now bound to a different store.
            if (_service != null) _service.OnChanged -= Forward;

            _service = service;
            _service.OnChanged += Forward;

            await _service.InitializeAsync(cToken);
        }

        /// <summary>
        /// Installs the component that delivers rewards into the game's economy.
        /// </summary>
        /// <param name="granter">The granter to use.</param>
        public static void SetRewardGranter(IStoreRewardGranter granter) =>
            _service?.SetRewardGranter(granter);

        /// <summary>
        /// Claims an offer: buys an IAP, claims a free offer, or watches a rewarded ad.
        /// </summary>
        /// <param name="offerId">The offer to claim.</param>
        /// <param name="cToken">Token to cancel the claim.</param>
        public static UniTask<StoreClaimResult> ClaimAsync(string offerId,
            CancellationToken cToken = default) =>
            _service?.ClaimAsync(offerId, cToken) ?? UniTask.FromResult(StoreClaimResult.NoStore);

        /// <summary>
        /// Re-evaluates the clock: cooldowns, claim limits and retries of failed deliveries.
        /// </summary>
        /// <param name="cToken">Token to cancel the refresh.</param>
        public static UniTask RefreshAsync(CancellationToken cToken = default) =>
            _service?.RefreshAsync(cToken) ?? UniTask.CompletedTask;

        /// <summary>
        /// Detaches the service and clears cached state.
        /// </summary>
        /// <remarks>
        /// <see cref="OnChanged"/> is left intact, because its subscribers are typically
        /// long-lived screens that would silently stop updating after a re-initialization.
        /// </remarks>
        public static void Reset()
        {
            if (_service == null) return;

            _service.OnChanged -= Forward;
            _service.Reset();
            _service = null;
        }

        private static void Forward(StoreSnapshot snapshot) => OnChanged.SafeInvoke(snapshot);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain reload can be disabled, in which case statics survive entering play mode
            // and the next session starts holding last session's service.
            if (_service != null) _service.OnChanged -= Forward;

            _service = null;
            OnChanged = null;
        }
    }
}
