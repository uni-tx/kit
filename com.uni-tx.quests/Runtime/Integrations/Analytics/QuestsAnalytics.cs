using System;
using System.Collections.Generic;
using UniTx.Analytics;
using UniTx.Events;

namespace UniTx.Quests.Integrations
{
    /// <summary>
    /// Reports the quest funnel to every registered analytics provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Subscribes to the kit's event bus rather than sitting on the call path, so
    /// instrumentation is opt-in, cannot slow a report down, and cannot be forgotten at a
    /// new call site — every route into the system ends in the same events.
    /// </para>
    /// <para>
    /// The funnel these events answer: do players open the board and start working
    /// (started), how far does each quest get (progressed), do they finish and claim
    /// (completed, claimed), and do resets bring them back (period reset). Claim is the
    /// completion signal; period reset is the cadence one.
    /// </para>
    /// </remarks>
    public sealed class QuestsAnalytics : IDisposable
    {
        /// <summary>
        /// Event name reported when a quest receives its first progress.
        /// </summary>
        public const string StartedEvent = "quest_started";

        /// <summary>
        /// Event name reported when a quest gains progress.
        /// </summary>
        public const string ProgressedEvent = "quest_progressed";

        /// <summary>
        /// Event name reported when every objective of a quest is met.
        /// </summary>
        public const string CompletedEvent = "quest_completed";

        /// <summary>
        /// Event name reported when a quest's rewards are collected.
        /// </summary>
        public const string ClaimedEvent = "quest_claimed";

        /// <summary>
        /// Event name reported when a reward could not be delivered.
        /// </summary>
        public const string GrantFailedEvent = "quest_grant_failed";

        /// <summary>
        /// Event name reported when a period rollover wipes a quest.
        /// </summary>
        public const string PeriodResetEvent = "quest_period_reset";

        private readonly Dictionary<string, object> _parameters = new();

        private bool _isDisposed;

        /// <summary>
        /// Starts reporting quest events.
        /// </summary>
        public QuestsAnalytics()
        {
            UniEvents.Subscribe<QuestStarted>(OnStarted);
            UniEvents.Subscribe<QuestProgressed>(OnProgressed);
            UniEvents.Subscribe<QuestCompleted>(OnCompleted);
            UniEvents.Subscribe<QuestClaimed>(OnClaimed);
            UniEvents.Subscribe<QuestGrantFailed>(OnGrantFailed);
            UniEvents.Subscribe<QuestPeriodReset>(OnPeriodReset);
        }

        /// <summary>
        /// Stops reporting.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;

            UniEvents.Unsubscribe<QuestStarted>(OnStarted);
            UniEvents.Unsubscribe<QuestProgressed>(OnProgressed);
            UniEvents.Unsubscribe<QuestCompleted>(OnCompleted);
            UniEvents.Unsubscribe<QuestClaimed>(OnClaimed);
            UniEvents.Unsubscribe<QuestGrantFailed>(OnGrantFailed);
            UniEvents.Unsubscribe<QuestPeriodReset>(OnPeriodReset);
        }

        private void OnStarted(QuestStarted @event)
        {
            _parameters.Clear();
            _parameters["set_id"] = @event.SetId;
            _parameters["quest_id"] = @event.QuestId;

            UniAnalytics.Track(StartedEvent, _parameters);
        }

        private void OnProgressed(QuestProgressed @event)
        {
            _parameters.Clear();
            _parameters["set_id"] = @event.SetId;
            _parameters["quest_id"] = @event.QuestId;
            _parameters["objective_key"] = @event.ObjectiveKey;
            _parameters["current"] = @event.Current;
            _parameters["target"] = @event.Target;

            UniAnalytics.Track(ProgressedEvent, _parameters);
        }

        private void OnCompleted(QuestCompleted @event)
        {
            _parameters.Clear();
            _parameters["set_id"] = @event.SetId;
            _parameters["quest_id"] = @event.QuestId;

            UniAnalytics.Track(CompletedEvent, _parameters);
        }

        private void OnClaimed(QuestClaimed @event)
        {
            _parameters.Clear();
            _parameters["set_id"] = @event.SetId;
            _parameters["quest_id"] = @event.QuestId;
            _parameters["reward_count"] = @event.Rewards?.Count ?? 0;

            UniAnalytics.Track(ClaimedEvent, _parameters);
        }

        private void OnGrantFailed(QuestGrantFailed @event)
        {
            _parameters.Clear();
            _parameters["set_id"] = @event.SetId;
            _parameters["quest_id"] = @event.QuestId;
            _parameters["reward_id"] = @event.RewardId;

            UniAnalytics.Track(GrantFailedEvent, _parameters);
        }

        private void OnPeriodReset(QuestPeriodReset @event)
        {
            _parameters.Clear();
            _parameters["set_id"] = @event.SetId;
            _parameters["quest_id"] = @event.QuestId;
            _parameters["reset"] = @event.Reset.ToString();

            UniAnalytics.Track(PeriodResetEvent, _parameters);
        }
    }
}
