using System;
using System.Collections.Generic;
using UniTx.Analytics;
using UniTx.Events;

namespace UniTx.SeasonPass.Integrations
{
    /// <summary>
    /// Reports the season pass funnel to every registered analytics provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Subscribes to the kit's event bus rather than sitting on the call path, so
    /// instrumentation is opt-in, cannot slow a grant down, and cannot be forgotten at a new
    /// call site — every route into the pass ends in the same events.
    /// </para>
    /// <para>
    /// The funnel these events answer: how far do players get, where do they stall, and does
    /// the paid track get bought before or after the stall. Tier-up is the retention signal;
    /// track unlock with the tier attached is the monetization one.
    /// </para>
    /// </remarks>
    public sealed class SeasonPassAnalytics : IDisposable
    {
        /// <summary>
        /// Event name reported when a tier is reached.
        /// </summary>
        public const string TierUnlockedEvent = "season_pass_tier_unlocked";

        /// <summary>
        /// Event name reported when a reward is collected.
        /// </summary>
        public const string RewardClaimedEvent = "season_pass_reward_claimed";

        /// <summary>
        /// Event name reported when a paid track is unlocked.
        /// </summary>
        public const string TrackUnlockedEvent = "season_pass_track_unlocked";

        /// <summary>
        /// Event name reported when a season is replaced.
        /// </summary>
        public const string SeasonChangedEvent = "season_pass_season_changed";

        /// <summary>
        /// Event name reported when a quest completes.
        /// </summary>
        public const string QuestCompletedEvent = "season_pass_quest_completed";

        /// <summary>
        /// Event name reported when a reward could not be delivered.
        /// </summary>
        public const string GrantFailedEvent = "season_pass_grant_failed";

        private readonly Dictionary<string, object> _parameters = new();

        private bool _isDisposed;

        /// <summary>
        /// Starts reporting season pass events.
        /// </summary>
        public SeasonPassAnalytics()
        {
            UniEvents.Subscribe<SeasonTierUnlocked>(OnTierUnlocked);
            UniEvents.Subscribe<SeasonRewardClaimed>(OnRewardClaimed);
            UniEvents.Subscribe<SeasonTrackUnlocked>(OnTrackUnlocked);
            UniEvents.Subscribe<SeasonChanged>(OnSeasonChanged);
            UniEvents.Subscribe<SeasonQuestCompleted>(OnQuestCompleted);
            UniEvents.Subscribe<SeasonRewardGrantFailed>(OnGrantFailed);
        }

        /// <summary>
        /// Stops reporting.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;

            UniEvents.Unsubscribe<SeasonTierUnlocked>(OnTierUnlocked);
            UniEvents.Unsubscribe<SeasonRewardClaimed>(OnRewardClaimed);
            UniEvents.Unsubscribe<SeasonTrackUnlocked>(OnTrackUnlocked);
            UniEvents.Unsubscribe<SeasonChanged>(OnSeasonChanged);
            UniEvents.Unsubscribe<SeasonQuestCompleted>(OnQuestCompleted);
            UniEvents.Unsubscribe<SeasonRewardGrantFailed>(OnGrantFailed);
        }

        private void OnTierUnlocked(SeasonTierUnlocked @event)
        {
            _parameters.Clear();
            _parameters["season_id"] = @event.SeasonId;
            _parameters["tier"] = @event.Tier;
            _parameters["is_bonus_tier"] = @event.IsBonusTier;

            UniAnalytics.Track(TierUnlockedEvent, _parameters);
        }

        private void OnRewardClaimed(SeasonRewardClaimed @event)
        {
            _parameters.Clear();
            _parameters["season_id"] = @event.Reward.SeasonId;
            _parameters["tier"] = @event.Reward.Tier;
            _parameters["track"] = @event.Reward.Track.ToString();
            _parameters["reward_id"] = @event.Reward.RewardId;
            _parameters["was_automatic"] = @event.WasAutomatic;

            UniAnalytics.Track(RewardClaimedEvent, _parameters);
        }

        private void OnTrackUnlocked(SeasonTrackUnlocked @event)
        {
            _parameters.Clear();
            _parameters["season_id"] = @event.SeasonId;
            _parameters["track"] = @event.Track.ToString();
            _parameters["payment"] = @event.Payment.ToString();

            // The tier at purchase time separates "bought the pass up front" from "bought it
            // after grinding", which are different players and different offers.
            _parameters["tier_at_purchase"] = UniSeasonPass.Snapshot.Progress.Tier;

            UniAnalytics.Track(TrackUnlockedEvent, _parameters);
        }

        private void OnSeasonChanged(SeasonChanged @event)
        {
            _parameters.Clear();
            _parameters["previous_season_id"] = @event.PreviousSeasonId ?? string.Empty;
            _parameters["season_id"] = @event.SeasonId;
            _parameters["forfeited_rewards"] = @event.ForfeitedRewards;

            UniAnalytics.Track(SeasonChangedEvent, _parameters);
        }

        private void OnQuestCompleted(SeasonQuestCompleted @event)
        {
            _parameters.Clear();
            _parameters["season_id"] = @event.SeasonId;
            _parameters["quest_id"] = @event.QuestId;
            _parameters["xp_reward"] = @event.XpReward;

            UniAnalytics.Track(QuestCompletedEvent, _parameters);
        }

        private void OnGrantFailed(SeasonRewardGrantFailed @event)
        {
            _parameters.Clear();
            _parameters["season_id"] = @event.Reward.SeasonId;
            _parameters["tier"] = @event.Reward.Tier;
            _parameters["track"] = @event.Reward.Track.ToString();
            _parameters["reward_id"] = @event.Reward.RewardId;

            UniAnalytics.Track(GrantFailedEvent, _parameters);
        }
    }
}
