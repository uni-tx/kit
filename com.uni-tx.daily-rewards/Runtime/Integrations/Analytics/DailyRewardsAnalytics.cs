using System;
using System.Collections.Generic;
using UniTx.Analytics;
using UniTx.Events;

namespace UniTx.DailyRewards.Integrations
{
    /// <summary>
    /// Reports the daily rewards funnel to every registered analytics provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Subscribes to the kit's event bus rather than sitting on the call path, so
    /// instrumentation is opt-in, cannot slow a claim down, and cannot be forgotten at a new
    /// call site — every route into the system ends in the same events.
    /// </para>
    /// <para>
    /// The funnel these events answer: do players come back daily, how long do streaks run
    /// before breaking, and does a broken streak pull them back in. Claim is the retention
    /// signal; streak reset is the churn one.
    /// </para>
    /// </remarks>
    public sealed class DailyRewardsAnalytics : IDisposable
    {
        /// <summary>
        /// Event name reported when a daily reward is collected.
        /// </summary>
        public const string ClaimedEvent = "daily_rewards_claimed";

        /// <summary>
        /// Event name reported when a reward could not be delivered.
        /// </summary>
        public const string GrantFailedEvent = "daily_rewards_grant_failed";

        /// <summary>
        /// Event name reported when a missed day breaks the streak.
        /// </summary>
        public const string StreakResetEvent = "daily_rewards_streak_reset";

        private readonly Dictionary<string, object> _parameters = new();

        private bool _isDisposed;

        /// <summary>
        /// Starts reporting daily rewards events.
        /// </summary>
        public DailyRewardsAnalytics()
        {
            UniEvents.Subscribe<DailyRewardClaimed>(OnClaimed);
            UniEvents.Subscribe<DailyRewardGrantFailed>(OnGrantFailed);
            UniEvents.Subscribe<DailyStreakReset>(OnStreakReset);
        }

        /// <summary>
        /// Stops reporting.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;

            UniEvents.Unsubscribe<DailyRewardClaimed>(OnClaimed);
            UniEvents.Unsubscribe<DailyRewardGrantFailed>(OnGrantFailed);
            UniEvents.Unsubscribe<DailyStreakReset>(OnStreakReset);
        }

        private void OnClaimed(DailyRewardClaimed @event)
        {
            _parameters.Clear();
            _parameters["calendar_id"] = @event.CalendarId;
            _parameters["day"] = @event.Day;
            _parameters["slot_index"] = @event.SlotIndex;
            _parameters["reward_id"] = @event.RewardId;
            _parameters["item_id"] = @event.ItemId;
            _parameters["kind"] = @event.Kind.ToString();
            _parameters["amount"] = @event.Amount;
            _parameters["streak"] = @event.Streak;

            UniAnalytics.Track(ClaimedEvent, _parameters);
        }

        private void OnGrantFailed(DailyRewardGrantFailed @event)
        {
            _parameters.Clear();
            _parameters["calendar_id"] = @event.CalendarId;
            _parameters["slot_index"] = @event.SlotIndex;
            _parameters["reward_id"] = @event.RewardId;

            UniAnalytics.Track(GrantFailedEvent, _parameters);
        }

        private void OnStreakReset(DailyStreakReset @event)
        {
            _parameters.Clear();
            _parameters["calendar_id"] = @event.CalendarId;
            _parameters["previous_streak"] = @event.PreviousStreak;

            UniAnalytics.Track(StreakResetEvent, _parameters);
        }
    }
}
