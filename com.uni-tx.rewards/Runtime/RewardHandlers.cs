using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.Currency;
using UniTx.Entity;
using UnityEngine;

namespace UniTx.Rewards
{
    /// <summary>
    /// Delivers currency rewards into the entity-based currency system.
    /// </summary>
    /// <remarks>
    /// The reward's <c>ItemId</c> is the currency id. Grant ids pass straight through, so
    /// the currency system's idempotent ledger deduplicates replayed deliveries.
    /// </remarks>
    public sealed class CurrencyRewardHandler : IRewardHandler
    {
        private readonly ICurrencyService _currency;

        /// <summary>
        /// Creates the handler.
        /// </summary>
        /// <param name="currency">The currency service to grant through.</param>
        public CurrencyRewardHandler(ICurrencyService currency)
        {
            _currency = currency ?? throw new ArgumentNullException(nameof(currency));
        }

        /// <inheritdoc />
        public async UniTask<bool> GrantAsync(RewardData reward, string grantId = null,
            CancellationToken cToken = default)
        {
            var result = await _currency.GrantAsync(reward.ItemId, reward.Amount, grantId, cToken);

            // Duplicate means it was already delivered, and Capped means it was delivered
            // up to the currency's maximum — both are the reward reaching the player.
            return result is CurrencyGrantResult.Granted
                or CurrencyGrantResult.Duplicate
                or CurrencyGrantResult.Capped;
        }
    }

    /// <summary>
    /// Delivers item, cosmetic and booster rewards onto a registered entity.
    /// </summary>
    /// <remarks>
    /// The reward's <c>ItemId</c> is an entity id. The entity must be registered in the
    /// entity service and implement <see cref="IRewardConsumer"/>; anything else is a
    /// content bug and is refused so the reward stays claimable for retry.
    /// </remarks>
    public sealed class EntityRewardHandler : IRewardHandler
    {
        private readonly IEntityService _entities;
        private bool _hasWarned;

        /// <summary>
        /// Creates the handler.
        /// </summary>
        /// <param name="entities">The entity service to look the target up in.</param>
        public EntityRewardHandler(IEntityService entities)
        {
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        }

        /// <inheritdoc />
        public UniTask<bool> GrantAsync(RewardData reward, string grantId = null,
            CancellationToken cToken = default)
        {
            if (_entities.TryGet<IEntity>(reward.ItemId, out var entity) &&
                entity is IRewardConsumer consumer)
            {
                return consumer.ConsumeAsync(reward, grantId, cToken);
            }

            if (!_hasWarned)
            {
                _hasWarned = true;
                UniStatics.LogWarning(
                    $"Reward '{reward.Id}' targets entity '{reward.ItemId}', which is not " +
                    "registered or does not implement IRewardConsumer. The reward was " +
                    "refused so it stays claimable.", this);
            }

            return UniTask.FromResult(false);
        }
    }

    /// <summary>
    /// A handler that logs what it would deliver and always succeeds.
    /// </summary>
    /// <remarks>
    /// The fallback for a reward kind with no handler installed, so the whole flow is
    /// playable and testable before an economy exists. Swap in a real handler before
    /// ship; a warning is logged once so this cannot go unnoticed in a build.
    /// </remarks>
    public sealed class LoggingRewardHandler : IRewardHandler
    {
        private static readonly LoggingRewardHandler Shared = new();

        /// <summary>
        /// Gets the shared logging handler.
        /// </summary>
        public static LoggingRewardHandler Instance => Shared;

        private bool _hasWarned;

        /// <inheritdoc />
        public UniTask<bool> GrantAsync(RewardData reward, string grantId = null,
            CancellationToken cToken = default)
        {
            if (!_hasWarned)
            {
                _hasWarned = true;
                UniStatics.LogWarning(
                    $"No IRewardHandler is installed for reward kind {reward.Kind}, so " +
                    $"rewards of that kind are recorded as granted but never delivered. " +
                    "Install a handler before shipping.", this);
            }

            UniStatics.LogInfo(
                $"Granting {reward.Amount}x {reward.ItemId} ({reward.Kind}) from reward '{reward.Id}'.", this);

            return UniTask.FromResult(true);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain reload can be disabled, in which case the shared instance survives
            // entering play mode and the warn-once flag with it — the "no handler
            // installed" warning would then never fire again after the first session.
            Shared._hasWarned = false;
        }
    }
}
