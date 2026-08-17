using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Quests
{
    /// <summary>
    /// Static facade over the game's quest service.
    /// </summary>
    /// <remarks>
    /// A convenience layer, not a second implementation: every member forwards to the
    /// installed <see cref="IQuestsService"/>. Call sites scattered through gameplay
    /// code get one entry point, while the service stays injectable and testable.
    /// </remarks>
    public static class UniQuests
    {
        private static IQuestsService _service;

        /// <summary>
        /// Gets the installed service, or null before initialization.
        /// </summary>
        public static IQuestsService Service => _service;

        /// <summary>
        /// Indicates whether a board and the player's progress are loaded.
        /// </summary>
        public static bool IsReady => _service != null && _service.IsReady;

        /// <summary>
        /// Gets the active quest set definition, or null.
        /// </summary>
        public static QuestSetData Set => _service?.Set;

        /// <summary>
        /// Gets everything a quests screen needs in one value.
        /// </summary>
        public static QuestsSnapshot Snapshot => _service?.Snapshot ?? default;

        /// <summary>
        /// Raised whenever the snapshot changes.
        /// </summary>
        public static event Action<QuestsSnapshot> OnChanged;

        /// <summary>
        /// Installs a service and loads content and progress.
        /// </summary>
        /// <param name="service">The service to install.</param>
        /// <param name="cToken">Token to cancel initialization.</param>
        /// <exception cref="ArgumentNullException">Thrown when the service is null.</exception>
        public static async UniTask InitializeAsync(IQuestsService service,
            CancellationToken cToken = default)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            // Detach the previous service first, or its change events keep driving a UI that
            // is now bound to a different board.
            if (_service != null) _service.OnChanged -= Forward;

            _service = service;
            _service.OnChanged += Forward;

            await _service.InitializeAsync(cToken);
        }

        /// <summary>
        /// Installs the component that delivers rewards into the game's economy.
        /// </summary>
        /// <param name="granter">The granter to use.</param>
        public static void SetRewardGranter(IQuestRewardGranter granter) =>
            _service?.SetRewardGranter(granter);

        /// <summary>
        /// Reports progress against every objective whose key matches.
        /// </summary>
        /// <param name="objectiveKey">The key gameplay reports, e.g. "win_match".</param>
        /// <param name="amount">How much progress was made.</param>
        /// <param name="cToken">Token to cancel the report.</param>
        /// <returns>How many objectives advanced (zero when nothing matched).</returns>
        public static UniTask<int> ReportProgressAsync(string objectiveKey, int amount,
            CancellationToken cToken = default) =>
            _service?.ReportProgressAsync(objectiveKey, amount, cToken) ?? UniTask.FromResult(0);

        /// <summary>
        /// Claims a completed quest's rewards.
        /// </summary>
        /// <param name="questId">The quest to claim.</param>
        /// <param name="cToken">Token to cancel the claim.</param>
        public static UniTask<QuestClaimResult> ClaimAsync(string questId,
            CancellationToken cToken = default) =>
            _service?.ClaimAsync(questId, cToken) ?? UniTask.FromResult(QuestClaimResult.NoSet);

        /// <summary>
        /// Re-evaluates the clock: board selection, period rollovers and retries of failed
        /// deliveries.
        /// </summary>
        /// <param name="cToken">Token to cancel the refresh.</param>
        public static UniTask RefreshAsync(CancellationToken cToken = default) =>
            _service?.RefreshAsync(cToken) ?? UniTask.CompletedTask;

        /// <summary>
        /// Detaches the service and clears cached state.
        /// </summary>
        /// <remarks>
        /// <see cref="OnChanged"/> is left intact, because its subscribers are typically
        /// long-lived screens that would silently stop updating after a re-initialization.
        /// </remarks>
        public static void Reset()
        {
            if (_service == null) return;

            _service.OnChanged -= Forward;
            _service.Reset();
            _service = null;
        }

        private static void Forward(QuestsSnapshot snapshot) => OnChanged.SafeInvoke(snapshot);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain reload can be disabled, in which case statics survive entering play mode
            // and the next session starts holding last session's service.
            if (_service != null) _service.OnChanged -= Forward;

            _service = null;
            OnChanged = null;
        }
    }
}
