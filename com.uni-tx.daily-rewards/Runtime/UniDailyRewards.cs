using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.DailyRewards
{
    /// <summary>
    /// Static facade over the game's daily rewards service.
    /// </summary>
    /// <remarks>
    /// A convenience layer, not a second implementation: every member forwards to the
    /// installed <see cref="IDailyRewardsService"/>. Call sites scattered through gameplay
    /// code get one entry point, while the service stays injectable and testable.
    /// </remarks>
    public static class UniDailyRewards
    {
        private static IDailyRewardsService _service;

        /// <summary>
        /// Gets the installed service, or null before initialization.
        /// </summary>
        public static IDailyRewardsService Service => _service;

        /// <summary>
        /// Indicates whether a calendar and the player's progress are loaded.
        /// </summary>
        public static bool IsReady => _service != null && _service.IsReady;

        /// <summary>
        /// Gets the active calendar definition, or null.
        /// </summary>
        public static DailyRewardsData Calendar => _service?.Calendar;

        /// <summary>
        /// Gets everything a daily rewards screen needs in one value.
        /// </summary>
        public static DailyRewardsSnapshot Snapshot => _service?.Snapshot ?? default;

        /// <summary>
        /// Raised whenever the snapshot changes.
        /// </summary>
        public static event Action<DailyRewardsSnapshot> OnChanged;

        /// <summary>
        /// Installs a service and loads content and progress.
        /// </summary>
        /// <param name="service">The service to install.</param>
        /// <param name="cToken">Token to cancel initialization.</param>
        /// <exception cref="ArgumentNullException">Thrown when the service is null.</exception>
        public static async UniTask InitializeAsync(IDailyRewardsService service,
            CancellationToken cToken = default)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            // Detach the previous service first, or its change events keep driving a UI that
            // is now bound to a different calendar.
            if (_service != null) _service.OnChanged -= Forward;

            _service = service;
            _service.OnChanged += Forward;

            await _service.InitializeAsync(cToken);
        }

        /// <summary>
        /// Installs the component that delivers rewards into the game's economy.
        /// </summary>
        /// <param name="granter">The granter to use.</param>
        public static void SetRewardGranter(IDailyRewardsRewardGranter granter) =>
            _service?.SetRewardGranter(granter);

        /// <summary>
        /// Indicates whether a reward can be claimed right now.
        /// </summary>
        public static bool IsClaimable => _service != null && _service.IsClaimable;

        /// <summary>
        /// Claims today's reward.
        /// </summary>
        /// <param name="cToken">Token to cancel the claim.</param>
        public static UniTask<DailyClaimResult> ClaimAsync(CancellationToken cToken = default) =>
            _service?.ClaimAsync(cToken) ?? UniTask.FromResult(DailyClaimResult.NoCalendar);

        /// <summary>
        /// Re-evaluates the clock: calendar selection, day rollover, streak breaks and
        /// retries of failed deliveries.
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

        private static void Forward(DailyRewardsSnapshot snapshot) => OnChanged.SafeInvoke(snapshot);

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
