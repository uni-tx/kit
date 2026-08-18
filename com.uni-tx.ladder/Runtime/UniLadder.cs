using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Ladder
{
    /// <summary>
    /// Static facade over the game's ladder service.
    /// </summary>
    /// <remarks>
    /// A convenience layer, not a second implementation: every member forwards to the
    /// installed <see cref="ILadderService"/>. Call sites scattered through gameplay
    /// code get one entry point, while the service stays injectable and testable.
    /// </remarks>
    public static class UniLadder
    {
        private static ILadderService _service;

        /// <summary>
        /// Gets the installed service, or null before initialization.
        /// </summary>
        public static ILadderService Service => _service;

        /// <summary>
        /// Indicates whether a ladder and the player's progress are loaded.
        /// </summary>
        public static bool IsReady => _service != null && _service.IsReady;

        /// <summary>
        /// Gets the active ladder definition, or null.
        /// </summary>
        public static LadderData Ladder => _service?.Ladder;

        /// <summary>
        /// Gets everything a ladder screen needs in one value.
        /// </summary>
        public static LadderSnapshot Snapshot => _service?.Snapshot ?? default;

        /// <summary>
        /// Raised whenever the snapshot changes.
        /// </summary>
        public static event Action<LadderSnapshot> OnChanged;

        /// <summary>
        /// Installs a service and loads content and progress.
        /// </summary>
        /// <param name="service">The service to install.</param>
        /// <param name="cToken">Token to cancel initialization.</param>
        /// <exception cref="ArgumentNullException">Thrown when the service is null.</exception>
        public static async UniTask InitializeAsync(ILadderService service,
            CancellationToken cToken = default)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            // Detach the previous service first, or its change events keep driving a UI that
            // is now bound to a different ladder.
            if (_service != null) _service.OnChanged -= Forward;

            _service = service;
            _service.OnChanged += Forward;

            await _service.InitializeAsync(cToken);
        }

        /// <summary>
        /// Installs the component that delivers rewards into the game's economy.
        /// </summary>
        /// <param name="granter">The granter to use.</param>
        public static void SetRewardGranter(ILadderRewardGranter granter) =>
            _service?.SetRewardGranter(granter);

        /// <summary>
        /// Reports steps climbed. Every rung whose threshold the new total crosses becomes
        /// claimable.
        /// </summary>
        /// <param name="steps">How many steps were climbed.</param>
        /// <param name="cToken">Token to cancel the report.</param>
        /// <returns>How many rungs were newly reached (zero when none crossed).</returns>
        public static UniTask<int> ReportStepsAsync(int steps,
            CancellationToken cToken = default) =>
            _service?.ReportStepsAsync(steps, cToken) ?? UniTask.FromResult(0);

        /// <summary>
        /// Claims a reached rung's rewards.
        /// </summary>
        /// <param name="rungId">The rung to claim.</param>
        /// <param name="cToken">Token to cancel the claim.</param>
        public static UniTask<LadderClaimResult> ClaimAsync(string rungId,
            CancellationToken cToken = default) =>
            _service?.ClaimAsync(rungId, cToken) ?? UniTask.FromResult(LadderClaimResult.NoLadder);

        /// <summary>
        /// Re-evaluates the selection: ladder choice and retries of failed deliveries.
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

        private static void Forward(LadderSnapshot snapshot) => OnChanged.SafeInvoke(snapshot);

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
