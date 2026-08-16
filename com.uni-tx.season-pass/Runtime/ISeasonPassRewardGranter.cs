using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// Delivers a claimed reward into the game's own economy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The season pass tracks <i>what</i> a player has earned; it never owns their inventory
    /// or currency balances. That split is what lets a season roll over without touching
    /// anything the player keeps.
    /// </para>
    /// <para>
    /// The return value is load-bearing. A claim is recorded only after this reports success,
    /// so a granter that fails — a full inventory, a server that timed out — leaves the reward
    /// claimable and queued for retry rather than marked collected and gone. Return
    /// <c>false</c> instead of throwing for an expected refusal.
    /// </para>
    /// </remarks>
    public interface ISeasonPassRewardGranter
    {
        /// <summary>
        /// Delivers one reward.
        /// </summary>
        /// <param name="reward">What to grant.</param>
        /// <param name="reference">Which slot it came from, for logging and telemetry.</param>
        /// <param name="cToken">Token to cancel the grant.</param>
        /// <returns><c>true</c> when the reward reached the player.</returns>
        UniTask<bool> GrantAsync(SeasonRewardData reward, SeasonRewardRef reference,
            CancellationToken cToken = default);
    }
}
