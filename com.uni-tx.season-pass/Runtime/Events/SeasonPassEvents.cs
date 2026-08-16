using UniTx.Events;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// Raised after season XP is added.
    /// </summary>
    /// <remarks>
    /// Struct events, dispatched through the kit's priority bus, so a season pass screen, an
    /// analytics adapter and a "quest complete" toast can all listen without any of them
    /// knowing about the others — and without boxing on every raise.
    /// </remarks>
    public readonly struct SeasonXpGranted : IEvent
    {
        /// <summary>
        /// The season the XP was added to.
        /// </summary>
        public readonly string SeasonId;

        /// <summary>
        /// The whitelisted source that produced it.
        /// </summary>
        public readonly string SourceId;

        /// <summary>
        /// How much XP was actually added, after any daily cap.
        /// </summary>
        public readonly int Amount;

        /// <summary>
        /// The player's total after the grant.
        /// </summary>
        public readonly int TotalXp;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="seasonId">The season id.</param>
        /// <param name="sourceId">The XP source.</param>
        /// <param name="amount">XP added after capping.</param>
        /// <param name="totalXp">The resulting total.</param>
        public SeasonXpGranted(string seasonId, string sourceId, int amount, int totalXp)
        {
            SeasonId = seasonId;
            SourceId = sourceId;
            Amount = amount;
            TotalXp = totalXp;
        }
    }

    /// <summary>
    /// Raised once for each tier crossed by an XP grant.
    /// </summary>
    public readonly struct SeasonTierUnlocked : IEvent
    {
        /// <summary>
        /// The season the tier belongs to.
        /// </summary>
        public readonly string SeasonId;

        /// <summary>
        /// The tier just reached.
        /// </summary>
        public readonly int Tier;

        /// <summary>
        /// Indicates whether this is a repeatable bonus tier past the ladder's end.
        /// </summary>
        public readonly bool IsBonusTier;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="seasonId">The season id.</param>
        /// <param name="tier">The tier reached.</param>
        /// <param name="isBonusTier">Whether it is a bonus tier.</param>
        public SeasonTierUnlocked(string seasonId, int tier, bool isBonusTier)
        {
            SeasonId = seasonId;
            Tier = tier;
            IsBonusTier = isBonusTier;
        }
    }

    /// <summary>
    /// Raised after a reward is delivered and recorded.
    /// </summary>
    public readonly struct SeasonRewardClaimed : IEvent
    {
        /// <summary>
        /// Which reward slot was claimed.
        /// </summary>
        public readonly SeasonRewardRef Reward;

        /// <summary>
        /// Indicates whether it was granted automatically rather than by a player tap.
        /// </summary>
        public readonly bool WasAutomatic;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="reward">The claimed slot.</param>
        /// <param name="wasAutomatic">Whether the claim was automatic.</param>
        public SeasonRewardClaimed(SeasonRewardRef reward, bool wasAutomatic)
        {
            Reward = reward;
            WasAutomatic = wasAutomatic;
        }
    }

    /// <summary>
    /// Raised when a reward could not be delivered and was queued for retry.
    /// </summary>
    /// <remarks>
    /// Worth surfacing rather than swallowing: it is the difference between "the game ate my
    /// reward" and a message telling the player it will arrive shortly.
    /// </remarks>
    public readonly struct SeasonRewardGrantFailed : IEvent
    {
        /// <summary>
        /// The reward that could not be delivered.
        /// </summary>
        public readonly SeasonRewardRef Reward;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="reward">The undelivered slot.</param>
        public SeasonRewardGrantFailed(SeasonRewardRef reward) => Reward = reward;
    }

    /// <summary>
    /// Raised when a paid track becomes owned.
    /// </summary>
    public readonly struct SeasonTrackUnlocked : IEvent
    {
        /// <summary>
        /// The season the track belongs to.
        /// </summary>
        public readonly string SeasonId;

        /// <summary>
        /// The newly owned track.
        /// </summary>
        public readonly SeasonTrack Track;

        /// <summary>
        /// How it was paid for.
        /// </summary>
        public readonly SeasonPassPayment Payment;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="seasonId">The season id.</param>
        /// <param name="track">The unlocked track.</param>
        /// <param name="payment">How it was paid for.</param>
        public SeasonTrackUnlocked(string seasonId, SeasonTrack track, SeasonPassPayment payment)
        {
            SeasonId = seasonId;
            Track = track;
            Payment = payment;
        }
    }

    /// <summary>
    /// Raised when the active season is replaced by a different one.
    /// </summary>
    public readonly struct SeasonChanged : IEvent
    {
        /// <summary>
        /// The season that ended, or null on a first run.
        /// </summary>
        public readonly string PreviousSeasonId;

        /// <summary>
        /// The season now active.
        /// </summary>
        public readonly string SeasonId;

        /// <summary>
        /// How many unlocked rewards were forfeited by the rollover.
        /// </summary>
        public readonly int ForfeitedRewards;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="previousSeasonId">The outgoing season id.</param>
        /// <param name="seasonId">The incoming season id.</param>
        /// <param name="forfeitedRewards">How many rewards were lost.</param>
        public SeasonChanged(string previousSeasonId, string seasonId, int forfeitedRewards)
        {
            PreviousSeasonId = previousSeasonId;
            SeasonId = seasonId;
            ForfeitedRewards = forfeitedRewards;
        }
    }

    /// <summary>
    /// Raised the first time a season enters its final stretch.
    /// </summary>
    public readonly struct SeasonEndingSoon : IEvent
    {
        /// <summary>
        /// The season about to end.
        /// </summary>
        public readonly string SeasonId;

        /// <summary>
        /// Hours left before earning closes.
        /// </summary>
        public readonly double HoursRemaining;

        /// <summary>
        /// How many rewards the player has unlocked but not claimed.
        /// </summary>
        public readonly int UnclaimedRewards;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="seasonId">The season id.</param>
        /// <param name="hoursRemaining">Hours before earning closes.</param>
        /// <param name="unclaimedRewards">Unclaimed reward count.</param>
        public SeasonEndingSoon(string seasonId, double hoursRemaining, int unclaimedRewards)
        {
            SeasonId = seasonId;
            HoursRemaining = hoursRemaining;
            UnclaimedRewards = unclaimedRewards;
        }
    }

    /// <summary>
    /// Raised when a quest is completed and its XP paid.
    /// </summary>
    public readonly struct SeasonQuestCompleted : IEvent
    {
        /// <summary>
        /// The season the quest belongs to.
        /// </summary>
        public readonly string SeasonId;

        /// <summary>
        /// The completed quest.
        /// </summary>
        public readonly string QuestId;

        /// <summary>
        /// The XP it paid.
        /// </summary>
        public readonly int XpReward;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="seasonId">The season id.</param>
        /// <param name="questId">The quest id.</param>
        /// <param name="xpReward">The XP paid.</param>
        public SeasonQuestCompleted(string seasonId, string questId, int xpReward)
        {
            SeasonId = seasonId;
            QuestId = questId;
            XpReward = xpReward;
        }
    }
}
