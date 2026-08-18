using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;

namespace UniTx.Store
{
    /// <summary>
    /// Runs one shop: content-defined offers of three kinds — IAP, free-on-cooldown and
    /// rewarded — with idempotent claims and a save that only records delivered rewards.
    /// </summary>
    public interface IStoreService : IInjectable, IInitializableAsync, IResettable
    {
        /// <summary>
        /// Indicates whether content and saved progress are loaded.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Gets the active store definition, or null when none is registered.
        /// </summary>
        StoreData Store { get; }

        /// <summary>
        /// Gets everything a shop screen needs in one value.
        /// </summary>
        StoreSnapshot Snapshot { get; }

        /// <summary>
        /// Raised whenever the snapshot changes, for UI that binds rather than polls.
        /// </summary>
        event Action<StoreSnapshot> OnChanged;

        /// <summary>
        /// Installs the component that delivers rewards into the game's economy.
        /// </summary>
        /// <param name="granter">The granter to use.</param>
        void SetRewardGranter(IStoreRewardGranter granter);

        /// <summary>
        /// Claims an offer: buys an IAP, claims a free offer, or watches a rewarded ad.
        /// </summary>
        /// <param name="offerId">The offer to claim.</param>
        /// <param name="cToken">Token to cancel the claim.</param>
        /// <returns>The outcome. A claim is recorded only after delivery succeeds.</returns>
        UniTask<StoreClaimResult> ClaimAsync(string offerId, CancellationToken cToken = default);

        /// <summary>
        /// Re-evaluates the clock: cooldowns, claim limits and retries of failed deliveries.
        /// </summary>
        /// <param name="cToken">Token to cancel the refresh.</param>
        /// <remarks>
        /// Call on app resume and when the shop screen opens. Nothing else drives the
        /// passage of time, so a session left open across a cooldown boundary only notices
        /// when this runs.
        /// </remarks>
        UniTask RefreshAsync(CancellationToken cToken = default);
    }
}
