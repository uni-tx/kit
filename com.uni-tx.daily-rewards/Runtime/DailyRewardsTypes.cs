using System;

namespace UniTx.DailyRewards
{
    /// <summary>
    /// How the calendar advances when a player misses days.
    /// </summary>
    /// <remarks>
    /// Enum values are stable; they are stored in JSON content and in saves.
    /// </remarks>
    public enum DailyRewardsMode
    {
        /// <summary>
        /// The position follows the wall clock. Missed days are skipped — the player claims
        /// the slot for wherever the calendar has advanced to, and can never go back.
        /// </summary>
        Calendar = 0,

        /// <summary>
        /// The position follows consecutive claims. Missing a day resets the calendar to
        /// day one, so the reward at day N genuinely requires N consecutive logins.
        /// </summary>
        Streak = 1,
    }

    /// <summary>
    /// What a daily rewards screen needs to know about the calendar right now.
    /// </summary>
    public enum DailyRewardsState
    {
        /// <summary>
        /// No calendar is loaded.
        /// </summary>
        None = 0,

        /// <summary>
        /// A reward can be claimed right now.
        /// </summary>
        Claimable = 1,

        /// <summary>
        /// Today's reward has already been claimed; the next one unlocks at the reset.
        /// </summary>
        Claimed = 2,

        /// <summary>
        /// A non-looping calendar has run out and will never pay out again.
        /// </summary>
        Finished = 3,
    }

    /// <summary>
    /// Outcome of a claim attempt.
    /// </summary>
    public enum DailyClaimResult
    {
        /// <summary>
        /// The reward reached the player.
        /// </summary>
        Claimed = 0,

        /// <summary>
        /// Today's reward was already collected.
        /// </summary>
        AlreadyClaimed = 1,

        /// <summary>
        /// A non-looping calendar has paid out every slot.
        /// </summary>
        Finished = 2,

        /// <summary>
        /// The granter refused or failed; nothing was recorded, so the slot stays claimable.
        /// </summary>
        GrantFailed = 3,

        /// <summary>
        /// No calendar is loaded.
        /// </summary>
        NoCalendar = 4,

        /// <summary>
        /// The slot at the current position is missing the fields a granter needs.
        /// </summary>
        Rejected = 5,
    }

    /// <summary>
    /// A slot reference carried through the granter, for logging and telemetry.
    /// </summary>
    public readonly struct DailyRewardRef : IEquatable<DailyRewardRef>
    {
        /// <summary>
        /// The calendar the slot belongs to.
        /// </summary>
        public readonly string CalendarId;

        /// <summary>
        /// The 0-based slot index within the calendar.
        /// </summary>
        public readonly int SlotIndex;

        /// <summary>
        /// The 1-based day number shown in the UI.
        /// </summary>
        public readonly int Day;

        /// <summary>
        /// The reward id of the slot.
        /// </summary>
        public readonly string RewardId;

        /// <summary>
        /// Creates a reference to one reward slot.
        /// </summary>
        /// <param name="calendarId">The owning calendar id.</param>
        /// <param name="slotIndex">The 0-based slot index.</param>
        /// <param name="day">The 1-based day number.</param>
        /// <param name="rewardId">The reward id within the slot.</param>
        public DailyRewardRef(string calendarId, int slotIndex, int day, string rewardId)
        {
            CalendarId = calendarId;
            SlotIndex = slotIndex;
            Day = day;
            RewardId = rewardId;
        }

        /// <summary>
        /// Builds the idempotent grant id for this slot.
        /// </summary>
        /// <param name="calendarId">The owning calendar id.</param>
        /// <param name="dayStartUnix">The day boundary the claim belongs to.</param>
        public static string GrantId(string calendarId, long dayStartUnix) =>
            $"daily:{calendarId}:{dayStartUnix}";

        /// <inheritdoc />
        public bool Equals(DailyRewardRef other) =>
            SlotIndex == other.SlotIndex &&
            string.Equals(CalendarId, other.CalendarId, StringComparison.Ordinal) &&
            string.Equals(RewardId, other.RewardId, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is DailyRewardRef other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SlotIndex;
                hash = (hash * 397) ^ (CalendarId != null ? StringComparer.Ordinal.GetHashCode(CalendarId) : 0);
                hash = (hash * 397) ^ (RewardId != null ? StringComparer.Ordinal.GetHashCode(RewardId) : 0);
                return hash;
            }
        }

        /// <inheritdoc />
        public override string ToString() => $"daily:{CalendarId}:{SlotIndex}";
    }

    /// <summary>
    /// Everything a daily rewards screen needs in one value.
    /// </summary>
    /// <remarks>
    /// Immutable by design — the service builds it on demand, and UI binds to
    /// <c>OnChanged</c> rather than holding a stale copy across a day boundary.
    /// </remarks>
    public readonly struct DailyRewardsSnapshot
    {
        /// <summary>
        /// The calendar id, or null when none is loaded.
        /// </summary>
        public readonly string CalendarId;

        /// <summary>
        /// What the calendar is doing right now.
        /// </summary>
        public readonly DailyRewardsState State;

        /// <summary>
        /// The current consecutive-day streak, or zero when it has broken.
        /// </summary>
        public readonly int Streak;

        /// <summary>
        /// How many slots the calendar has.
        /// </summary>
        public readonly int TotalDays;

        /// <summary>
        /// The slot index the UI should highlight: the claimable one, or the one claimed today.
        /// </summary>
        public readonly int CurrentSlotIndex;

        /// <summary>
        /// The reward preview for <see cref="CurrentSlotIndex"/>, or null.
        /// </summary>
        public readonly DailyRewardSlotData CurrentSlot;

        /// <summary>
        /// Indicates whether the current slot is the calendar's milestone reward.
        /// </summary>
        public readonly bool IsMilestone;

        /// <summary>
        /// The Unix timestamp the next claim unlocks at (today's boundary plus a day).
        /// </summary>
        public readonly long NextClaimUnix;

        /// <summary>
        /// Seconds until the next claim unlocks.
        /// </summary>
        public readonly long RemainingSeconds;

        /// <summary>
        /// Creates a snapshot.
        /// </summary>
        /// <param name="calendarId">The calendar id, or null.</param>
        /// <param name="state">The current state.</param>
        /// <param name="streak">The current streak.</param>
        /// <param name="totalDays">The calendar length.</param>
        /// <param name="currentSlotIndex">The highlighted slot index.</param>
        /// <param name="currentSlot">The highlighted slot, or null.</param>
        /// <param name="isMilestone">Whether the highlighted slot is a milestone.</param>
        /// <param name="nextClaimUnix">When the next claim unlocks.</param>
        /// <param name="remainingSeconds">Seconds until it unlocks.</param>
        public DailyRewardsSnapshot(string calendarId, DailyRewardsState state, int streak,
            int totalDays, int currentSlotIndex, DailyRewardSlotData currentSlot,
            bool isMilestone, long nextClaimUnix, long remainingSeconds)
        {
            CalendarId = calendarId;
            State = state;
            Streak = streak;
            TotalDays = totalDays;
            CurrentSlotIndex = currentSlotIndex;
            CurrentSlot = currentSlot;
            IsMilestone = isMilestone;
            NextClaimUnix = nextClaimUnix;
            RemainingSeconds = remainingSeconds;
        }
    }
}
