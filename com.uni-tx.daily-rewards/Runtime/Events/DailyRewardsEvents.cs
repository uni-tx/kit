using UniTx.Events;
using UniTx.Rewards;

namespace UniTx.DailyRewards
{
    /// <summary>
    /// Raised after a daily reward reaches the player.
    /// </summary>
    /// <remarks>
    /// Struct event on the kit bus, so a toast, a streak widget and an analytics adapter can
    /// all listen without knowing about each other.
    /// </remarks>
    public readonly struct DailyRewardClaimed : IEvent
    {
        /// <summary>
        /// The calendar the claim belongs to.
        /// </summary>
        public readonly string CalendarId;

        /// <summary>
        /// The 1-based day number claimed.
        /// </summary>
        public readonly int Day;

        /// <summary>
        /// The 0-based slot index claimed.
        /// </summary>
        public readonly int SlotIndex;

        /// <summary>
        /// The reward definition id.
        /// </summary>
        public readonly string RewardId;

        /// <summary>
        /// The game-side id of the granted item or currency.
        /// </summary>
        public readonly string ItemId;

        /// <summary>
        /// What kind of thing was granted.
        /// </summary>
        public readonly RewardKind Kind;

        /// <summary>
        /// How many units the slot asked for.
        /// </summary>
        public readonly int Amount;

        /// <summary>
        /// The streak after this claim.
        /// </summary>
        public readonly int Streak;

        /// <summary>
        /// The idempotency id the delivery was recorded under.
        /// </summary>
        public readonly string GrantId;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="calendarId">The calendar id.</param>
        /// <param name="day">The 1-based day number.</param>
        /// <param name="slotIndex">The 0-based slot index.</param>
        /// <param name="rewardId">The reward definition id.</param>
        /// <param name="itemId">The granted item or currency id.</param>
        /// <param name="kind">The reward kind.</param>
        /// <param name="amount">How many units.</param>
        /// <param name="streak">The streak after the claim.</param>
        /// <param name="grantId">The idempotency id.</param>
        public DailyRewardClaimed(string calendarId, int day, int slotIndex, string rewardId,
            string itemId, RewardKind kind, int amount, int streak, string grantId)
        {
            CalendarId = calendarId;
            Day = day;
            SlotIndex = slotIndex;
            RewardId = rewardId;
            ItemId = itemId;
            Kind = kind;
            Amount = amount;
            Streak = streak;
            GrantId = grantId;
        }
    }

    /// <summary>
    /// Raised when a reward delivery fails, so the slot is queued for retry.
    /// </summary>
    public readonly struct DailyRewardGrantFailed : IEvent
    {
        /// <summary>
        /// The calendar the claim belongs to.
        /// </summary>
        public readonly string CalendarId;

        /// <summary>
        /// The 0-based slot index that could not be delivered.
        /// </summary>
        public readonly int SlotIndex;

        /// <summary>
        /// The reward definition id.
        /// </summary>
        public readonly string RewardId;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="calendarId">The calendar id.</param>
        /// <param name="slotIndex">The 0-based slot index.</param>
        /// <param name="rewardId">The reward definition id.</param>
        public DailyRewardGrantFailed(string calendarId, int slotIndex, string rewardId)
        {
            CalendarId = calendarId;
            SlotIndex = slotIndex;
            RewardId = rewardId;
        }
    }

    /// <summary>
    /// Raised when a missed day breaks the streak.
    /// </summary>
    /// <remarks>
    /// The churn signal: a player who was coming back daily has stopped. Listen here for a
    /// re-engagement campaign, and on <see cref="DailyRewardClaimed"/> for the recovery.
    /// </remarks>
    public readonly struct DailyStreakReset : IEvent
    {
        /// <summary>
        /// The calendar the streak belonged to.
        /// </summary>
        public readonly string CalendarId;

        /// <summary>
        /// The streak before it broke.
        /// </summary>
        public readonly int PreviousStreak;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="calendarId">The calendar id.</param>
        /// <param name="previousStreak">The streak before it broke.</param>
        public DailyStreakReset(string calendarId, int previousStreak)
        {
            CalendarId = calendarId;
            PreviousStreak = previousStreak;
        }
    }
}
