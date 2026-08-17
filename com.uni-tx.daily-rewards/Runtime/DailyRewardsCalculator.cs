using System;

namespace UniTx.DailyRewards
{
    /// <summary>
    /// Pure calendar math: which slot a claim lands on, and how the position advances.
    /// </summary>
    /// <remarks>
    /// No I/O, no state of its own — everything is derived from the calendar definition,
    /// the saved data and the current day boundary, so the rules can be unit-tested without
    /// the Unity engine.
    /// </remarks>
    public static class DailyRewardsCalculator
    {
        /// <summary>
        /// Plans the claim for the current day against the saved position.
        /// </summary>
        /// <param name="calendar">The active calendar.</param>
        /// <param name="saved">The player's saved position.</param>
        /// <param name="currentDayStart">Today's day boundary.</param>
        /// <returns>What a claim would do.</returns>
        public static DailyClaimPlan PlanClaim(DailyRewardsData calendar,
            DailyRewardsSavedData saved, long currentDayStart)
        {
            var count = calendar.SlotCount;

            if (count == 0) return new DailyClaimPlan(DailyClaimOutcome.Finished, 0, false, 0);

            // First ever claim always starts at day one, whatever the mode.
            if (saved.LastClaimDayStartUnix == 0)
            {
                return new DailyClaimPlan(DailyClaimOutcome.Claimable, 0, false, 0);
            }

            var daysSince = DailyRewardsTime.DaysBetween(saved.LastClaimDayStartUnix, currentDayStart);

            if (daysSince == 0) return new DailyClaimPlan(DailyClaimOutcome.AlreadyClaimed, 0, false, 0);

            // A day boundary moved past the last claim breaks the streak in both modes; in
            // Streak mode it also resets the position.
            var resetsStreak = daysSince > 1;

            // A delivery that failed earlier today retries the same slot — the position is
            // never advanced until a claim actually lands.
            if (saved.FailedClaimDayStartUnix == currentDayStart)
            {
                return new DailyClaimPlan(DailyClaimOutcome.Claimable, saved.NextSlotIndex,
                    resetsStreak, saved.Streak);
            }

            if (calendar.Mode == DailyRewardsMode.Streak && resetsStreak)
            {
                return new DailyClaimPlan(DailyClaimOutcome.Claimable, 0, true, saved.Streak);
            }

            // Calendar mode advances by the number of missed days, so the reward is always
            // the one for today's position; Streak mode advances exactly one slot per day.
            // The stored next index is the slot that was already due, so the advance counts
            // from it — hence the minus one.
            var advance = calendar.Mode == DailyRewardsMode.Calendar ? daysSince : 1;
            var next = saved.NextSlotIndex + (int)advance - 1;

            if (!calendar.Loop && next >= count)
            {
                return new DailyClaimPlan(DailyClaimOutcome.Finished, 0, false, 0);
            }

            var slot = calendar.Loop ? next % count : next;

            return new DailyClaimPlan(DailyClaimOutcome.Claimable, slot, resetsStreak, saved.Streak);
        }

        /// <summary>
        /// Returns the slot index the next claim will deliver after a successful claim.
        /// </summary>
        /// <param name="calendar">The active calendar.</param>
        /// <param name="claimedSlotIndex">The slot index that was claimed.</param>
        /// <remarks>
        /// Both modes advance identically after a claim — the mode only decides which slot
        /// gets claimed. A looping calendar wraps; a finite one lands exactly on
        /// <c>count</c>, which reads as <see cref="DailyClaimOutcome.Finished"/>.
        /// </remarks>
        public static int GetNextSlotIndex(DailyRewardsData calendar, int claimedSlotIndex)
        {
            var count = calendar.SlotCount;

            if (count == 0) return 0;

            return calendar.Loop ? (claimedSlotIndex + 1) % count : Math.Min(claimedSlotIndex + 1, count);
        }

        /// <summary>
        /// Returns the streak as it stands right now.
        /// </summary>
        /// <param name="saved">The player's saved position.</param>
        /// <param name="currentDayStart">Today's day boundary.</param>
        /// <remarks>
        /// A claim within the last day keeps the saved streak alive even before today's
        /// claim; more than one missed day reads as zero until the next claim restarts it.
        /// </remarks>
        public static int GetCurrentStreak(DailyRewardsSavedData saved, long currentDayStart)
        {
            if (saved.LastClaimDayStartUnix == 0) return 0;

            var daysSince = DailyRewardsTime.DaysBetween(saved.LastClaimDayStartUnix, currentDayStart);

            return daysSince > 1 ? 0 : saved.Streak;
        }

        /// <summary>
        /// Returns the slot index a UI should highlight for the current state.
        /// </summary>
        /// <param name="saved">The player's saved position.</param>
        /// <param name="slotCount">How many slots the calendar has.</param>
        /// <remarks>
        /// In the claimed state the highlighted slot is the one collected today — the index
        /// before the stored next one, with the looped wrap and the finite end both guarded.
        /// </remarks>
        public static int GetCurrentSlotIndex(DailyRewardsSavedData saved, int slotCount)
        {
            var last = Math.Max(0, slotCount - 1);

            if (saved.NextSlotIndex <= 0) return last;

            return Math.Min(saved.NextSlotIndex - 1, last);
        }
    }

    /// <summary>
    /// What a claim against the current position would do.
    /// </summary>
    public readonly struct DailyClaimPlan
    {
        /// <summary>
        /// The outcome a claim would produce.
        /// </summary>
        public readonly DailyClaimOutcome Outcome;

        /// <summary>
        /// The 0-based slot index to claim, when the outcome is <see cref="DailyClaimOutcome.Claimable"/>.
        /// </summary>
        public readonly int SlotIndex;

        /// <summary>
        /// Indicates whether this claim breaks and restarts the streak.
        /// </summary>
        public readonly bool ResetsStreak;

        /// <summary>
        /// The saved streak before this claim.
        /// </summary>
        public readonly int PreviousStreak;

        /// <summary>
        /// Creates a plan.
        /// </summary>
        /// <param name="outcome">The outcome.</param>
        /// <param name="slotIndex">The slot index to claim.</param>
        /// <param name="resetsStreak">Whether the streak resets.</param>
        /// <param name="previousStreak">The streak before the claim.</param>
        public DailyClaimPlan(DailyClaimOutcome outcome, int slotIndex, bool resetsStreak,
            int previousStreak)
        {
            Outcome = outcome;
            SlotIndex = slotIndex;
            ResetsStreak = resetsStreak;
            PreviousStreak = previousStreak;
        }
    }

    /// <summary>
    /// The possible outcomes of planning a claim.
    /// </summary>
    public enum DailyClaimOutcome
    {
        /// <summary>
        /// A slot can be claimed.
        /// </summary>
        Claimable = 0,

        /// <summary>
        /// Today was already claimed.
        /// </summary>
        AlreadyClaimed = 1,

        /// <summary>
        /// A finite calendar has run out.
        /// </summary>
        Finished = 2,
    }
}
