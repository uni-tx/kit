using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;

namespace UniTx.Ladder
{
    /// <summary>
    /// Runs the reward ladder: a cumulative climb fed by reported steps — typically quest
    /// completions — where each rung pays out once its threshold is crossed and the top
    /// rung is the grand prize.
    /// </summary>
    public interface ILadderService : IInjectable, IInitializableAsync, IResettable
    {
        /// <summary>
        /// Indicates whether content and saved progress are loaded.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Gets the active ladder definition, or null when none is registered.
        /// </summary>
        LadderData Ladder { get; }

        /// <summary>
        /// Gets everything a ladder screen needs in one value.
        /// </summary>
        LadderSnapshot Snapshot { get; }

        /// <summary>
        /// Raised whenever the snapshot changes, for UI that binds rather than polls.
        /// </summary>
        event Action<LadderSnapshot> OnChanged;

        /// <summary>
        /// Installs the component that delivers rewards into the game's economy.
        /// </summary>
        /// <param name="granter">The granter to use.</param>
        void SetRewardGranter(ILadderRewardGranter granter);

        /// <summary>
        /// Reports steps climbed. Every rung whose threshold the new total crosses becomes
        /// claimable.
        /// </summary>
        /// <param name="steps">How many steps were climbed. Negative amounts are ignored.</param>
        /// <param name="cToken">Token to cancel the report.</param>
        /// <returns>How many rungs were newly reached (zero when none crossed).</returns>
        /// <remarks>
        /// The seam between gameplay and content: call this from wherever a completion
        /// happens — a quest is claimed, a level is cleared, a purchase lands. The
        /// <c>QuestsLadderBridge</c> integration does exactly that for the quests package.
        /// </remarks>
        UniTask<int> ReportStepsAsync(int steps, CancellationToken cToken = default);

        /// <summary>
        /// Claims a reached rung's rewards.
        /// </summary>
        /// <param name="rungId">The rung to claim.</param>
        /// <param name="cToken">Token to cancel the claim.</param>
        UniTask<LadderClaimResult> ClaimAsync(string rungId, CancellationToken cToken = default);

        /// <summary>
        /// Re-evaluates the selection: ladder choice and retries of failed deliveries.
        /// </summary>
        /// <param name="cToken">Token to cancel the refresh.</param>
        /// <remarks>
        /// Call on app resume and when the ladder screen opens. Nothing else drives the
        /// selection, so a ladder replaced server-side only notices when this runs.
        /// </remarks>
        UniTask RefreshAsync(CancellationToken cToken = default);
    }
}
