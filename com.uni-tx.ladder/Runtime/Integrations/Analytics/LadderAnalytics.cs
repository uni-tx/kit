using System;
using System.Collections.Generic;
using UniTx.Analytics;
using UniTx.Events;

namespace UniTx.Ladder.Integrations
{
    /// <summary>
    /// Reports the ladder funnel to every registered analytics provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Subscribes to the kit's event bus rather than sitting on the call path, so
    /// instrumentation is opt-in, cannot slow a step report down, and cannot be forgotten
    /// at a new call site — every route into the system ends in the same events.
    /// </para>
    /// <para>
    /// The funnel these events answer: do players climb at all (steps added), how far does
    /// each rung get (rung reached), do they collect (rung claimed), do deliveries fail
    /// (grant failed), and do they reach the top (completed). Completed is the conversion
    /// event; steps added is the engagement one.
    /// </para>
    /// </remarks>
    public sealed class LadderAnalytics : IDisposable
    {
        /// <summary>
        /// Event name reported when steps are added to the climb.
        /// </summary>
        public const string StepsAddedEvent = "ladder_steps_added";

        /// <summary>
        /// Event name reported when a rung's threshold is crossed.
        /// </summary>
        public const string RungReachedEvent = "ladder_rung_reached";

        /// <summary>
        /// Event name reported when a rung's rewards are collected.
        /// </summary>
        public const string RungClaimedEvent = "ladder_rung_claimed";

        /// <summary>
        /// Event name reported when a reward could not be delivered.
        /// </summary>
        public const string GrantFailedEvent = "ladder_grant_failed";

        /// <summary>
        /// Event name reported when the grand prize is claimed.
        /// </summary>
        public const string CompletedEvent = "ladder_completed";

        private readonly Dictionary<string, object> _parameters = new();

        private bool _isDisposed;

        /// <summary>
        /// Starts reporting ladder events.
        /// </summary>
        public LadderAnalytics()
        {
            UniEvents.Subscribe<LadderStepsAdded>(OnStepsAdded);
            UniEvents.Subscribe<LadderRungReached>(OnRungReached);
            UniEvents.Subscribe<LadderRungClaimed>(OnRungClaimed);
            UniEvents.Subscribe<LadderGrantFailed>(OnGrantFailed);
            UniEvents.Subscribe<LadderCompleted>(OnCompleted);
        }

        /// <summary>
        /// Stops reporting.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;

            UniEvents.Unsubscribe<LadderStepsAdded>(OnStepsAdded);
            UniEvents.Unsubscribe<LadderRungReached>(OnRungReached);
            UniEvents.Unsubscribe<LadderRungClaimed>(OnRungClaimed);
            UniEvents.Unsubscribe<LadderGrantFailed>(OnGrantFailed);
            UniEvents.Unsubscribe<LadderCompleted>(OnCompleted);
        }

        private void OnStepsAdded(LadderStepsAdded @event)
        {
            _parameters.Clear();
            _parameters["ladder_id"] = @event.LadderId;
            _parameters["steps"] = @event.Steps;
            _parameters["total_steps"] = @event.TotalSteps;

            UniAnalytics.Track(StepsAddedEvent, _parameters);
        }

        private void OnRungReached(LadderRungReached @event)
        {
            _parameters.Clear();
            _parameters["ladder_id"] = @event.LadderId;
            _parameters["rung_id"] = @event.RungId;
            _parameters["steps"] = @event.Steps;

            UniAnalytics.Track(RungReachedEvent, _parameters);
        }

        private void OnRungClaimed(LadderRungClaimed @event)
        {
            _parameters.Clear();
            _parameters["ladder_id"] = @event.LadderId;
            _parameters["rung_id"] = @event.RungId;
            _parameters["reward_count"] = @event.Rewards?.Count ?? 0;

            UniAnalytics.Track(RungClaimedEvent, _parameters);
        }

        private void OnGrantFailed(LadderGrantFailed @event)
        {
            _parameters.Clear();
            _parameters["ladder_id"] = @event.LadderId;
            _parameters["rung_id"] = @event.RungId;
            _parameters["reward_id"] = @event.RewardId;

            UniAnalytics.Track(GrantFailedEvent, _parameters);
        }

        private void OnCompleted(LadderCompleted @event)
        {
            _parameters.Clear();
            _parameters["ladder_id"] = @event.LadderId;
            _parameters["rung_id"] = @event.RungId;

            UniAnalytics.Track(CompletedEvent, _parameters);
        }
    }
}
