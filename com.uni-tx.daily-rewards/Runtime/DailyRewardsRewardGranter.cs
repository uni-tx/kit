using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Rewards;

namespace UniTx.DailyRewards
{
    /// <summary>
    /// The default <see cref="IDailyRewardsRewardGranter"/>: maps each day's slot onto the
    /// generic <see cref="RewardData"/> and delivers it through <see cref="IRewardService"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the kit's rewards package is installed and its service is registered, daily
    /// rewards are delivered for real: currency rewards land in the entity-based currency
    /// system, item and cosmetic rewards land on the registered entity whose id matches the
    /// slot's item id. A game with its own economy can still bind its own
    /// <see cref="IDailyRewardsRewardGranter"/>.
    /// </para>
    /// <para>
    /// The claim's day-boundary grant id is passed through, so even a replay that slips past
    /// the daily rewards system's own ledger cannot double-deliver into the economy.
    /// </para>
    /// </remarks>
    public sealed class DailyRewardsRewardGranter : IDailyRewardsRewardGranter
    {
        private readonly IRewardService _rewards;

        /// <summary>
        /// Creates the granter.
        /// </summary>
        /// <param name="rewards">The reward service to deliver through.</param>
        public DailyRewardsRewardGranter(IRewardService rewards)
        {
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
        }

        /// <inheritdoc />
        public async UniTask<bool> GrantAsync(DailyRewardSlotData slot, DailyRewardRef reference,
            string grantId, CancellationToken cToken = default)
        {
            // The slot already speaks RewardKind, so the mapping is a straight copy.
            var mapped = new RewardData(slot.RewardId, slot.Kind, slot.ItemId, slot.Amount,
                slot.IconAddress);

            var result = await _rewards.GrantAsync(mapped, grantId, cToken);

            return result == RewardGrantResult.Granted;
        }
    }
}
