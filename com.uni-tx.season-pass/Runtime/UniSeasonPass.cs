using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// Static facade over the game's season pass service.
    /// </summary>
    /// <remarks>
    /// A convenience layer, not a second implementation: every member forwards to the
    /// installed <see cref="ISeasonPassService"/>. Call sites scattered through gameplay code
    /// get one entry point, while the service stays injectable and testable.
    /// </remarks>
    public static class UniSeasonPass
    {
        private static ISeasonPassService _service;

        /// <summary>
        /// Gets the installed service, or null before initialization.
        /// </summary>
        public static ISeasonPassService Service => _service;

        /// <summary>
        /// Indicates whether a season and the player's progress are loaded.
        /// </summary>
        public static bool IsReady => _service != null && _service.IsReady;

        /// <summary>
        /// Gets the active season definition, or null.
        /// </summary>
        public static SeasonPassData Season => _service?.Season;

        /// <summary>
        /// Gets the current lifecycle phase.
        /// </summary>
        public static SeasonPhase Phase => _service?.Phase ?? SeasonPhase.None;

        /// <summary>
        /// Gets everything a season pass screen needs in one value.
        /// </summary>
        public static SeasonPassSnapshot Snapshot => _service?.Snapshot ?? default;

        /// <summary>
        /// Raised whenever the snapshot changes.
        /// </summary>
        public static event Action<SeasonPassSnapshot> OnChanged;

        /// <summary>
        /// Installs a service and loads content and progress.
        /// </summary>
        /// <param name="service">The service to install.</param>
        /// <param name="cToken">Token to cancel initialization.</param>
        /// <exception cref="ArgumentNullException">Thrown when the service is null.</exception>
        public static async UniTask InitializeAsync(ISeasonPassService service,
            CancellationToken cToken = default)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            // Detach the previous service first, or its change events keep driving a UI that
            // is now bound to a different season.
            if (_service != null) _service.OnChanged -= Forward;

            _service = service;
            _service.OnChanged += Forward;

            await _service.InitializeAsync(cToken);
        }

        /// <summary>
        /// Adds season XP from a whitelisted source.
        /// </summary>
        /// <param name="sourceId">The source id declared in the season definition.</param>
        /// <param name="amount">XP to add, or zero to use the source's configured amount.</param>
        /// <param name="grantId">Idempotency id; a repeat of the same id is ignored.</param>
        /// <param name="cToken">Token to cancel the grant.</param>
        public static UniTask<XpGrantResult> GrantXpAsync(string sourceId, int amount = 0,
            string grantId = null, CancellationToken cToken = default) =>
            _service?.GrantXpAsync(sourceId, amount, grantId, cToken)
            ?? UniTask.FromResult(XpGrantResult.Rejected);

        /// <summary>
        /// Claims one reward.
        /// </summary>
        /// <param name="reward">The reward slot to claim.</param>
        /// <param name="cToken">Token to cancel the claim.</param>
        public static UniTask<ClaimResult> ClaimAsync(SeasonRewardRef reward,
            CancellationToken cToken = default) =>
            _service?.ClaimAsync(reward, cToken) ?? UniTask.FromResult(ClaimResult.NoSeason);

        /// <summary>
        /// Claims every reward on one tier of one track.
        /// </summary>
        /// <param name="tier">The 1-based tier number.</param>
        /// <param name="track">The track to claim from.</param>
        /// <param name="cToken">Token to cancel the claim.</param>
        public static UniTask<int> ClaimTierAsync(int tier, SeasonTrack track,
            CancellationToken cToken = default) =>
            _service?.ClaimTierAsync(tier, track, cToken) ?? UniTask.FromResult(0);

        /// <summary>
        /// Claims everything currently claimable.
        /// </summary>
        /// <param name="cToken">Token to cancel the operation.</param>
        public static UniTask<int> ClaimAllAsync(CancellationToken cToken = default) =>
            _service?.ClaimAllAsync(cToken) ?? UniTask.FromResult(0);

        /// <summary>
        /// Unlocks a paid track and back-grants every tier already passed.
        /// </summary>
        /// <param name="track">The track to unlock.</param>
        /// <param name="payment">Charge the wallet, or record an unlock already paid for.</param>
        /// <param name="cToken">Token to cancel the unlock.</param>
        public static UniTask<TrackUnlockResult> UnlockTrackAsync(SeasonTrack track,
            SeasonPassPayment payment = SeasonPassPayment.Currency,
            CancellationToken cToken = default) =>
            _service?.UnlockTrackAsync(track, payment, cToken)
            ?? UniTask.FromResult(TrackUnlockResult.Rejected);

        /// <summary>
        /// Buys tier skips, banking any that fall past the end of the ladder.
        /// </summary>
        /// <param name="count">How many tiers to skip.</param>
        /// <param name="payment">Charge the wallet, or record skips already paid for.</param>
        /// <param name="cToken">Token to cancel the purchase.</param>
        public static UniTask<int> BuyTierSkipsAsync(int count,
            SeasonPassPayment payment = SeasonPassPayment.Currency,
            CancellationToken cToken = default) =>
            _service?.BuyTierSkipsAsync(count, payment, cToken) ?? UniTask.FromResult(0);

        /// <summary>
        /// Records progress against a season quest.
        /// </summary>
        /// <param name="questId">The quest id.</param>
        /// <param name="amount">How much progress to add.</param>
        /// <param name="cToken">Token to cancel the update.</param>
        public static UniTask<QuestProgressResult> ReportQuestProgressAsync(string questId,
            int amount = 1, CancellationToken cToken = default) =>
            _service?.ReportQuestProgressAsync(questId, amount, cToken)
            ?? UniTask.FromResult(QuestProgressResult.Rejected);

        /// <summary>
        /// Re-evaluates the clock: rollover, expiry, window resets, sync and retries.
        /// </summary>
        /// <param name="cToken">Token to cancel the refresh.</param>
        public static UniTask RefreshAsync(CancellationToken cToken = default) =>
            _service?.RefreshAsync(cToken) ?? UniTask.CompletedTask;

        /// <summary>
        /// Indicates whether the player owns a track this season.
        /// </summary>
        /// <param name="track">The track to test.</param>
        public static bool OwnsTrack(SeasonTrack track) => _service != null && _service.OwnsTrack(track);

        /// <summary>
        /// Fills a buffer with every reward the player can claim right now.
        /// </summary>
        /// <param name="buffer">Buffer to fill. Cleared first.</param>
        /// <returns>How many rewards were written.</returns>
        public static int GetClaimable(List<SeasonRewardRef> buffer) =>
            _service?.GetClaimable(buffer) ?? 0;

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

        private static void Forward(SeasonPassSnapshot snapshot) => OnChanged.SafeInvoke(snapshot);

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
