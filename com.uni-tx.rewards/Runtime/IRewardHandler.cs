using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniTx.Rewards
{
    /// <summary>
    /// Delivers one reward of a particular kind into the game's economy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The service routes by <see cref="RewardData.Kind"/> to the matching handler. The
    /// return value is load-bearing: a caller records the reward as delivered only after
    /// this reports <c>true</c>, so a handler that fails leaves the reward claimable and
    /// retryable rather than marked collected and gone. Return <c>false</c> instead of
    /// throwing for an expected refusal.
    /// </para>
    /// <para>
    /// Built-in handlers: <see cref="CurrencyRewardHandler"/> (currency rewards into
    /// <c>ICurrencyService</c>) and <see cref="EntityRewardHandler"/> (item, cosmetic and
    /// booster rewards onto a registered <see cref="IRewardConsumer"/> entity). Install
    /// your own per kind for anything game-specific.
    /// </para>
    /// </remarks>
    public interface IRewardHandler
    {
        /// <summary>
        /// Delivers one reward.
        /// </summary>
        /// <param name="reward">What to grant.</param>
        /// <param name="grantId">Idempotency id; a repeat of the same id is ignored.</param>
        /// <param name="cToken">Token to cancel the delivery.</param>
        /// <returns><c>true</c> when the reward reached the player.</returns>
        UniTask<bool> GrantAsync(RewardData reward, string grantId = null,
            CancellationToken cToken = default);
    }

    /// <summary>
    /// Implemented by entities that can consume a reward directly.
    /// </summary>
    /// <remarks>
    /// The hook that makes entity-backed reward delivery work: an inventory, a booster
    /// manager or any other entity registered in the entity service can opt in by
    /// implementing this, and <see cref="EntityRewardHandler"/> will route rewards to it
    /// by the reward's item id.
    /// </remarks>
    public interface IRewardConsumer
    {
        /// <summary>
        /// Consumes one reward.
        /// </summary>
        /// <param name="reward">What to grant.</param>
        /// <param name="grantId">Idempotency id; a repeat of the same id is ignored.</param>
        /// <param name="cToken">Token to cancel the delivery.</param>
        /// <returns><c>true</c> when the reward reached the player.</returns>
        UniTask<bool> ConsumeAsync(RewardData reward, string grantId = null,
            CancellationToken cToken = default);
    }
}
