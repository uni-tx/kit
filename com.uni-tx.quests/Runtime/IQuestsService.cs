using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;

namespace UniTx.Quests
{
    /// <summary>
    /// Runs the quest board: counter objectives fed from gameplay events, one-time, daily
    /// and weekly cadences on UTC resets, prerequisite chains, and idempotent claims that
    /// are recorded only after the rewards land.
    /// </summary>
    public interface IQuestsService : IInjectable, IInitializableAsync, IResettable
    {
        /// <summary>
        /// Indicates whether content and saved progress are loaded.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Gets the active quest set definition, or null when none is registered.
        /// </summary>
        QuestSetData Set { get; }

        /// <summary>
        /// Gets everything a quests screen needs in one value.
        /// </summary>
        QuestsSnapshot Snapshot { get; }

        /// <summary>
        /// Raised whenever the snapshot changes, for UI that binds rather than polls.
        /// </summary>
        event Action<QuestsSnapshot> OnChanged;

        /// <summary>
        /// Installs the component that delivers rewards into the game's economy.
        /// </summary>
        /// <param name="granter">The granter to use.</param>
        void SetRewardGranter(IQuestRewardGranter granter);

        /// <summary>
        /// Reports progress against every objective whose key matches.
        /// </summary>
        /// <param name="objectiveKey">The key gameplay reports, e.g. "win_match".</param>
        /// <param name="amount">How much progress was made. Negative amounts are ignored.</param>
        /// <param name="cToken">Token to cancel the report.</param>
        /// <returns>How many objectives advanced (zero when nothing matched).</returns>
        /// <remarks>
        /// The seam between gameplay and content: call this from wherever an event happens —
        /// a match ends, a level is reached, a currency is spent. Every quest whose objective
        /// key matches advances together, and quests that complete become claimable.
        /// </remarks>
        UniTask<int> ReportProgressAsync(string objectiveKey, int amount,
            CancellationToken cToken = default);

        /// <summary>
        /// Claims a completed quest's rewards.
        /// </summary>
        /// <param name="questId">The quest to claim.</param>
        /// <param name="cToken">Token to cancel the claim.</param>
        UniTask<QuestClaimResult> ClaimAsync(string questId, CancellationToken cToken = default);

        /// <summary>
        /// Re-evaluates the clock: board selection, period rollovers and retries of failed
        /// deliveries.
        /// </summary>
        /// <param name="cToken">Token to cancel the refresh.</param>
        /// <remarks>
        /// Call on app resume and when the quests screen opens. Nothing else drives the
        /// passage of time, so a session left open across a reset boundary only notices when
        /// this runs.
        /// </remarks>
        UniTask RefreshAsync(CancellationToken cToken = default);
    }
}
