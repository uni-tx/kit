using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Rewards;

namespace UniTx.Store
{
    /// <summary>
    /// The default <see cref="IStoreRewardGranter"/>: maps each reward onto the generic
    /// <see cref="RewardData"/> and delivers it through <see cref="IRewardService"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the kit's rewards package is installed and its service is registered, store
    /// rewards are delivered for real: currency rewards land in the entity-based currency
    /// system, item and cosmetic rewards land on the registered entity whose id matches the
    /// reward's item id. A game with its own economy can still bind its own
    /// <see cref="IStoreRewardGranter"/>.
    /// </para>
    /// <para>
    /// The offer's grant id is passed through, so even a replay that slips past the store's
    /// own ledger cannot double-deliver into the economy.
    /// </para>
    /// </remarks>
    public sealed class StoreRewardGranter : IStoreRewardGranter
    {
        private readonly IRewardService _rewards;

        /// <summary>
        /// Creates the granter.
        /// </summary>
        /// <param name="rewards">The reward service to deliver through.</param>
        public StoreRewardGranter(IRewardService rewards)
        {
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
        }

        /// <inheritdoc />
        public async UniTask<bool> GrantAsync(StoreOfferData offer, StoreRewardData reward,
            StoreOfferRef reference, string grantId, CancellationToken cToken = default)
        {
            // The reward already speaks RewardKind, so the mapping is a straight copy.
            var mapped = new RewardData(reward.RewardId, reward.Kind, reward.ItemId,
                reward.Amount, reward.IconAddress);

            var result = await _rewards.GrantAsync(mapped, grantId, cToken);

            return result == RewardGrantResult.Granted;
        }
    }
}
