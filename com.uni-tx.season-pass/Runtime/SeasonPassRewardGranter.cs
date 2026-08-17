using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Rewards;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// The default <see cref="ISeasonPassRewardGranter"/>: maps each season reward slot
    /// onto the generic <see cref="RewardData"/> and delivers it through
    /// <see cref="IRewardService"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replaces the old log-only default. When the kit's rewards package is installed and
    /// its service is registered, season rewards are delivered for real: currency rewards
    /// land in the entity-based currency system, item and cosmetic rewards land on the
    /// registered entity whose id matches the reward's item id. A game with its own
    /// economy can still bind its own <see cref="ISeasonPassRewardGranter"/>.
    /// </para>
    /// <para>
    /// The claim key is used as the idempotent grant id, so even a replay that slips past
    /// the season pass's own claim ledger cannot double-deliver into the economy.
    /// </para>
    /// </remarks>
    public sealed class SeasonPassRewardGranter : ISeasonPassRewardGranter
    {
        private readonly IRewardService _rewards;

        /// <summary>
        /// Creates the granter.
        /// </summary>
        /// <param name="rewards">The reward service to deliver through.</param>
        public SeasonPassRewardGranter(IRewardService rewards)
        {
            _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
        }

        /// <inheritdoc />
        public async UniTask<bool> GrantAsync(SeasonRewardData reward, SeasonRewardRef reference,
            CancellationToken cToken = default)
        {
            var mapped = new RewardData(reward.RewardId, Map(reward.Kind), reward.ItemId,
                reward.Amount, reward.IconAddress);

            var result = await _rewards.GrantAsync(mapped, reference.ToString(), cToken);

            return result == RewardGrantResult.Granted;
        }

        private static RewardKind Map(SeasonRewardKind kind) => kind switch
        {
            SeasonRewardKind.Currency => RewardKind.Currency,
            SeasonRewardKind.Item => RewardKind.Item,
            SeasonRewardKind.Cosmetic => RewardKind.Cosmetic,
            SeasonRewardKind.Booster => RewardKind.Booster,
            _ => RewardKind.Custom,
        };
    }
}
