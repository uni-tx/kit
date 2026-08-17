using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;

namespace UniTx.DailyRewards
{
    /// <summary>
    /// Runs one daily reward calendar: one idempotent claim per day, streak tracking, and a
    /// reset that the config decides the hour of.
    /// </summary>
    public interface IDailyRewardsService : IInjectable, IInitializableAsync, IResettable
    {
        /// <summary>
        /// Indicates whether content and saved progress are loaded.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Gets the active calendar definition, or null when none is registered.
        /// </summary>
        DailyRewardsData Calendar { get; }

        /// <summary>
        /// Gets everything a daily rewards screen needs in one value.
        /// </summary>
        DailyRewardsSnapshot Snapshot { get; }

        /// <summary>
        /// Raised whenever the snapshot changes, for UI that binds rather than polls.
        /// </summary>
        event Action<DailyRewardsSnapshot> OnChanged;

        /// <summary>
        /// Installs the component that delivers rewards into the game's economy.
        /// </summary>
        /// <param name="granter">The granter to use.</param>
        void SetRewardGranter(IDailyRewardsRewardGranter granter);

        /// <summary>
        /// Indicates whether a reward can be claimed right now.
        /// </summary>
        bool IsClaimable { get; }

        /// <summary>
        /// Claims today's reward.
        /// </summary>
        /// <param name="cToken">Token to cancel the claim.</param>
        UniTask<DailyClaimResult> ClaimAsync(CancellationToken cToken = default);

        /// <summary>
        /// Re-evaluates the clock: calendar selection, day rollover, streak breaks and
        /// retries of failed deliveries.
        /// </summary>
        /// <param name="cToken">Token to cancel the refresh.</param>
        /// <remarks>
        /// Call on app resume and when the rewards screen opens. Nothing else drives the
        /// passage of time, so a session left open across a reset boundary only notices when
        /// this runs.
        /// </remarks>
        UniTask RefreshAsync(CancellationToken cToken = default);
    }
}
