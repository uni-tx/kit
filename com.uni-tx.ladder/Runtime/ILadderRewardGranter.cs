using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniTx.Ladder
{
    /// <summary>
    /// Delivers a claimed rung's rewards into the game's own economy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ladder tracks <i>what</i> a player has earned; it never owns their inventory or
    /// currency balances. That split is what lets a ladder be retuned or replaced without
    /// touching anything the player keeps.
    /// </para>
    /// <para>
    /// The return value is load-bearing. A claim is recorded only after this reports
    /// success, so a granter that fails — a full inventory, a server that timed out —
    /// leaves the rung claimable and queued for retry rather than marked collected and
    /// gone. Return <c>false</c> instead of throwing for an expected refusal.
    /// </para>
    /// </remarks>
    public interface ILadderRewardGranter
    {
        /// <summary>
        /// Delivers one reward of a claimed rung.
        /// </summary>
        /// <param name="rung">The rung the reward belongs to.</param>
        /// <param name="reward">What to grant.</param>
        /// <param name="reference">Which rung it came from, for logging and telemetry.</param>
        /// <param name="grantId">Idempotency id; a repeat of the same id is ignored.</param>
        /// <param name="cToken">Token to cancel the grant.</param>
        /// <returns><c>true</c> when the reward reached the player.</returns>
        UniTask<bool> GrantAsync(LadderRungData rung, LadderRewardData reward,
            LadderRungRef reference, string grantId, CancellationToken cToken = default);
    }
}
