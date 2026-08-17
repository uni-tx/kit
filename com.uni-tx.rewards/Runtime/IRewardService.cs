using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;

namespace UniTx.Rewards
{
    /// <summary>
    /// Delivers rewards into the game's economy, routing by reward kind.
    /// </summary>
    public interface IRewardService : IInjectable, IInitializableAsync, IResettable
    {
        /// <summary>
        /// Delivers one reward.
        /// </summary>
        /// <param name="reward">What to grant.</param>
        /// <param name="grantId">Idempotency id; a repeat of the same id is ignored.</param>
        /// <param name="cToken">Token to cancel the delivery.</param>
        /// <returns>What happened.</returns>
        UniTask<RewardGrantResult> GrantAsync(RewardData reward, string grantId = null,
            CancellationToken cToken = default);

        /// <summary>
        /// Installs the handler for a reward kind, replacing any built-in default.
        /// </summary>
        /// <param name="kind">The reward kind to handle.</param>
        /// <param name="handler">The handler to use.</param>
        void SetHandler(RewardKind kind, IRewardHandler handler);
    }
}
