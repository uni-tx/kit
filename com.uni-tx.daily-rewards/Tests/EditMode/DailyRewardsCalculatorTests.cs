using System;
using NUnit.Framework;
using UniTx.Core;

namespace UniTx.DailyRewards.Tests
{
    public sealed class DailyRewardsCalculatorTests
    {
        private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void StartOfDay_MidnightReset_BeginsAtUtcMidnight()
        {
            var now = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

            var dayStart = DailyRewardsTime.StartOfDay(now.ToUnixTimestamp(), 0);

            Assert.AreEqual(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                DailyRewardsTime.FromUnix(dayStart));
        }

        [Test]
        public void StartOfDay_CustomResetHour_ShiftsTheBoundary()
        {
            // 05:00 UTC is before the 09:00 reset, so it still belongs to the previous day.
            var beforeReset = new DateTime(2026, 6, 15, 5, 0, 0, DateTimeKind.Utc);
            var afterReset = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

            Assert.AreEqual(new DateTime(2026, 6, 14, 9, 0, 0, DateTimeKind.Utc),
                DailyRewardsTime.FromUnix(DailyRewardsTime.StartOfDay(beforeReset.ToUnixTimestamp(), 9)));
            Assert.AreEqual(new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc),
                DailyRewardsTime.FromUnix(DailyRewardsTime.StartOfDay(afterReset.ToUnixTimestamp(), 9)));
        }

        [Test]
        public void PlanClaim_FirstClaim_StartsAtDayOne()
        {
            var calendar = CalendarJson.Standard(days: 7);
            var saved = new DailyRewardsSavedData();
            var dayStart = Day(2);

            var plan = DailyRewardsCalculator.PlanClaim(calendar, saved, dayStart);

            Assert.AreEqual(DailyClaimOutcome.Claimable, plan.Outcome);
            Assert.AreEqual(0, plan.SlotIndex);
            Assert.IsFalse(plan.ResetsStreak);
        }

        [Test]
        public void PlanClaim_SameDay_AlreadyClaimed()
        {
            var calendar = CalendarJson.Standard(days: 7);
            var saved = new DailyRewardsSavedData();
            var dayStart = Day(2);

            saved.RecordClaim(CalendarJson.CalendarId, 1, 0, 1, 1, dayStart, dayStart, "grant");

            var plan = DailyRewardsCalculator.PlanClaim(calendar, saved, dayStart);

            Assert.AreEqual(DailyClaimOutcome.AlreadyClaimed, plan.Outcome);
        }

        [Test]
        public void PlanClaim_ConsecutiveDay_CalendarMode_AdvancesOneSlot()
        {
            var calendar = CalendarJson.Standard(days: 7, mode: (int)DailyRewardsMode.Calendar);
            var saved = new DailyRewardsSavedData();

            saved.RecordClaim(CalendarJson.CalendarId, 1, 0, 1, 1, Day(2), Day(2), "grant");

            var plan = DailyRewardsCalculator.PlanClaim(calendar, saved, Day(3));

            Assert.AreEqual(DailyClaimOutcome.Claimable, plan.Outcome);
            Assert.AreEqual(1, plan.SlotIndex);
            Assert.IsFalse(plan.ResetsStreak);
        }

        [Test]
        public void PlanClaim_MissedDays_CalendarMode_SkipsAhead()
        {
            var calendar = CalendarJson.Standard(days: 7, mode: (int)DailyRewardsMode.Calendar);
            var saved = new DailyRewardsSavedData();

            saved.RecordClaim(CalendarJson.CalendarId, 1, 0, 1, 1, Day(2), Day(2), "grant");

            // Claimed on day 2, returns on day 6: three days were missed, so the position
            // lands on slot 4 — the reward for "today" on the calendar.
            var plan = DailyRewardsCalculator.PlanClaim(calendar, saved, Day(6));

            Assert.AreEqual(DailyClaimOutcome.Claimable, plan.Outcome);
            Assert.AreEqual(4, plan.SlotIndex);
            Assert.IsTrue(plan.ResetsStreak);
        }

        [Test]
        public void PlanClaim_MissedDays_StreakMode_ResetsToDayOne()
        {
            var calendar = CalendarJson.Standard(days: 7, mode: (int)DailyRewardsMode.Streak);
            var saved = new DailyRewardsSavedData();

            saved.RecordClaim(CalendarJson.CalendarId, 3, 2, 3, 3, Day(4), Day(4), "grant");

            var plan = DailyRewardsCalculator.PlanClaim(calendar, saved, Day(7));

            Assert.AreEqual(DailyClaimOutcome.Claimable, plan.Outcome);
            Assert.AreEqual(0, plan.SlotIndex);
            Assert.IsTrue(plan.ResetsStreak);
        }

        [Test]
        public void PlanClaim_LoopedCalendar_WrapsAfterTheLastSlot()
        {
            var calendar = CalendarJson.Standard(days: 7, loop: true);
            var saved = new DailyRewardsSavedData();

            // Day 7 was claimed: slot 6, next index wrapped back to 0.
            saved.RecordClaim(CalendarJson.CalendarId, 7, 6, 0, 7, Day(8), Day(8), "grant");

            var plan = DailyRewardsCalculator.PlanClaim(calendar, saved, Day(9));

            Assert.AreEqual(DailyClaimOutcome.Claimable, plan.Outcome);
            Assert.AreEqual(0, plan.SlotIndex);
        }

        [Test]
        public void PlanClaim_FiniteCalendar_FinishesAfterTheLastSlot()
        {
            var calendar = CalendarJson.Standard(days: 3, loop: false);
            var saved = new DailyRewardsSavedData();

            // Day 3 was claimed; next index is 3, which is past the end of a 3-slot ladder.
            saved.RecordClaim(CalendarJson.CalendarId, 3, 2, 3, 3, Day(4), Day(4), "grant");

            var plan = DailyRewardsCalculator.PlanClaim(calendar, saved, Day(5));

            Assert.AreEqual(DailyClaimOutcome.Finished, plan.Outcome);
        }

        [Test]
        public void PlanClaim_FailedDelivery_Today_RetriesTheSameSlot()
        {
            var calendar = CalendarJson.Standard(days: 7);
            var saved = new DailyRewardsSavedData();

            saved.RecordClaim(CalendarJson.CalendarId, 1, 0, 1, 1, Day(2), Day(2), "grant");
            saved.MarkClaimFailed(Day(3));

            var plan = DailyRewardsCalculator.PlanClaim(calendar, saved, Day(3));

            Assert.AreEqual(DailyClaimOutcome.Claimable, plan.Outcome);
            Assert.AreEqual(1, plan.SlotIndex);
        }

        [Test]
        public void GetNextSlotIndex_LoopedCalendar_Wraps()
        {
            var calendar = CalendarJson.Standard(days: 7, loop: true);

            Assert.AreEqual(0, DailyRewardsCalculator.GetNextSlotIndex(calendar, 6));
            Assert.AreEqual(4, DailyRewardsCalculator.GetNextSlotIndex(calendar, 3));
        }

        [Test]
        public void GetNextSlotIndex_FiniteCalendar_CapsAtTheEnd()
        {
            var calendar = CalendarJson.Standard(days: 3, loop: false);

            // The value equals the slot count, which reads as Finished.
            Assert.AreEqual(3, DailyRewardsCalculator.GetNextSlotIndex(calendar, 2));
        }

        [Test]
        public void GetCurrentStreak_AliveWithinOneDay_MissedLongerReadsZero()
        {
            var saved = new DailyRewardsSavedData();
            saved.RecordClaim(CalendarJson.CalendarId, 1, 0, 1, 4, Day(2), Day(2), "grant");

            Assert.AreEqual(4, DailyRewardsCalculator.GetCurrentStreak(saved, Day(2)));
            Assert.AreEqual(4, DailyRewardsCalculator.GetCurrentStreak(saved, Day(3)));
            Assert.AreEqual(0, DailyRewardsCalculator.GetCurrentStreak(saved, Day(5)));
        }

        private static long Day(int day) =>
            new DateTime(2026, 6, day, 10, 0, 0, DateTimeKind.Utc).ToUnixTimestamp();
    }
}
