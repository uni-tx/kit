using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniTx.DailyRewards
{
    /// <summary>
    /// Delivers a claimed daily reward into the game's own economy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The daily rewards system tracks <i>what</i> a player has collected; it never owns
    /// their inventory or currency balances. That split is what lets a calendar be retuned
    /// or replaced without touching anything the player keeps.
    /// </para>
    /// <para>
    /// The return value is load-bearing. A claim is recorded only after this reports
    /// success, so a granter that fails — a full inventory, a server that timed out —
    /// leaves the day's reward claimable and queued for retry rather than marked collected
    /// and gone. Return <c>false</c> instead of throwing for an expected refusal.
    /// </para>
    /// </remarks>
    public interface IDailyRewardsRewardGranter
    {
        /// <summary>
        /// Delivers one day's reward.
        /// </summary>
        /// <param name="slot">What to grant.</param>
        /// <param name="reference">Which slot it came from, for logging and telemetry.</param>
        /// <param name="grantId">Idempotency id; a repeat of the same id is ignored.</param>
        /// <param name="cToken">Token to cancel the grant.</param>
        /// <returns><c>true</c> when the reward reached the player.</returns>
        UniTask<bool> GrantAsync(DailyRewardSlotData slot, DailyRewardRef reference,
            string grantId, CancellationToken cToken = default);
    }
}
