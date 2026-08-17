using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniTx.Quests
{
    /// <summary>
    /// Delivers a claimed quest's rewards into the game's own economy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The quest system tracks <i>what</i> a player has earned; it never owns their
    /// inventory or currency balances. That split is what lets a board be retuned or
    /// replaced without touching anything the player keeps.
    /// </para>
    /// <para>
    /// The return value is load-bearing. A claim is recorded only after this reports
    /// success, so a granter that fails — a full inventory, a server that timed out —
    /// leaves the quest claimable and queued for retry rather than marked collected and
    /// gone. Return <c>false</c> instead of throwing for an expected refusal.
    /// </para>
    /// </remarks>
    public interface IQuestRewardGranter
    {
        /// <summary>
        /// Delivers one reward of a claimed quest.
        /// </summary>
        /// <param name="quest">The quest the reward belongs to.</param>
        /// <param name="reward">What to grant.</param>
        /// <param name="reference">Which quest it came from, for logging and telemetry.</param>
        /// <param name="grantId">Idempotency id; a repeat of the same id is ignored.</param>
        /// <param name="cToken">Token to cancel the grant.</param>
        /// <returns><c>true</c> when the reward reached the player.</returns>
        UniTask<bool> GrantAsync(QuestData quest, QuestRewardData reward, QuestRef reference,
            string grantId, CancellationToken cToken = default);
    }
}
