using System;
using NUnit.Framework;
using UniTx.Core;

namespace UniTx.Quests.Tests
{
    public sealed class QuestCalculatorTests
    {
        [Test]
        public void StartOfDay_MidnightReset_BeginsAtUtcMidnight()
        {
            var now = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

            var dayStart = QuestTime.StartOfDay(now.ToUnixTimestamp(), 0);

            Assert.AreEqual(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                QuestTime.FromUnix(dayStart));
        }

        [Test]
        public void StartOfDay_CustomResetHour_ShiftsTheBoundary()
        {
            // 05:00 UTC is before the 09:00 reset, so it still belongs to the previous day.
            var beforeReset = new DateTime(2026, 6, 15, 5, 0, 0, DateTimeKind.Utc);
            var afterReset = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

            Assert.AreEqual(new DateTime(2026, 6, 14, 9, 0, 0, DateTimeKind.Utc),
                QuestTime.FromUnix(QuestTime.StartOfDay(beforeReset.ToUnixTimestamp(), 9)));
            Assert.AreEqual(new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc),
                QuestTime.FromUnix(QuestTime.StartOfDay(afterReset.ToUnixTimestamp(), 9)));
        }

        [Test]
        public void StartOfWeek_MondayStart_AlignsToMondayBoundary()
        {
            // 2026-06-18 is a Thursday. With a Monday week start, the week began on the 15th.
            var now = new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc);

            var weekStart = QuestTime.StartOfWeek(now.ToUnixTimestamp(), 0, 1);

            Assert.AreEqual(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                QuestTime.FromUnix(weekStart));
        }

        [Test]
        public void GetPeriodStart_None_IsAlwaysZero()
        {
            var now = new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc);

            Assert.AreEqual(0, QuestTime.GetPeriodStart(QuestReset.None,
                now.ToUnixTimestamp(), 0, 1));
        }

        [Test]
        public void EvaluateState_NoRecord_IsAvailable()
        {
            var set = QuestSetJson.Single();

            var state = QuestCalculator.EvaluateState(set.Quests[0], null, true);

            Assert.AreEqual(QuestState.Available, state);
        }

        [Test]
        public void EvaluateState_UnclaimedPrerequisite_LocksTheQuest()
        {
            var set = QuestSetJson.Chain();

            // The prerequisite (q1) is not claimed, so q2 stays locked.
            var state = QuestCalculator.EvaluateState(set.Quests[1], null, false);

            Assert.AreEqual(QuestState.Locked, state);
        }

        [Test]
        public void EvaluateState_ClaimedPrerequisite_UnlocksTheQuest()
        {
            var set = QuestSetJson.Chain();

            var state = QuestCalculator.EvaluateState(set.Quests[1], null, true);

            Assert.AreEqual(QuestState.Available, state);
        }

        [Test]
        public void EvaluateState_PartialProgress_IsInProgress()
        {
            var set = QuestSetJson.Single(target: 3);
            var record = new QuestRecord("q1", Day(15));
            record.AddProgress("win_match", 2);

            var state = QuestCalculator.EvaluateState(set.Quests[0], record, true);

            Assert.AreEqual(QuestState.InProgress, state);
        }

        [Test]
        public void EvaluateState_AllObjectivesMet_IsCompleted()
        {
            var set = QuestSetJson.Single(target: 3);
            var record = new QuestRecord("q1", Day(15));
            record.AddProgress("win_match", 3);

            var state = QuestCalculator.EvaluateState(set.Quests[0], record, true);

            Assert.AreEqual(QuestState.Completed, state);
        }

        [Test]
        public void EvaluateState_Claimed_StaysClaimed()
        {
            var set = QuestSetJson.Single(target: 3);
            var record = new QuestRecord("q1", Day(15));
            record.AddProgress("win_match", 3);
            record.RecordClaim(Day(15));

            var state = QuestCalculator.EvaluateState(set.Quests[0], record, true);

            Assert.AreEqual(QuestState.Claimed, state);
        }

        [Test]
        public void PlanReport_FirstReport_AddsProgressAndStartsTheQuest()
        {
            var set = QuestSetJson.Single(target: 3);

            var plan = QuestCalculator.PlanReport(set.Quests[0], null, true, "win_match", 1,
                Day(15));

            Assert.IsNotNull(plan);

            var report = plan.Value;

            Assert.AreEqual(1, report.Added);
            Assert.AreEqual(1, report.Current);
            Assert.AreEqual(3, report.Target);
            Assert.IsFalse(report.CompletesQuest);
        }

        [Test]
        public void PlanReport_Overflow_CapsAtTheTarget()
        {
            var set = QuestSetJson.Single(target: 3);
            var record = new QuestRecord("q1", Day(15));
            record.AddProgress("win_match", 2);

            var plan = QuestCalculator.PlanReport(set.Quests[0], record, true, "win_match", 5,
                Day(15));

            Assert.IsNotNull(plan);

            var report = plan.Value;

            Assert.AreEqual(1, report.Added);
            Assert.AreEqual(3, report.Current);
            Assert.IsTrue(report.CompletesQuest);
        }

        [Test]
        public void PlanReport_UnmatchedKey_ReturnsNull()
        {
            var set = QuestSetJson.Single();

            var plan = QuestCalculator.PlanReport(set.Quests[0], null, true, "spend_coins", 1,
                Day(15));

            Assert.IsNull(plan);
        }

        [Test]
        public void PlanReport_ClaimedQuest_IgnoresReports()
        {
            var set = QuestSetJson.Single(target: 1);
            var record = new QuestRecord("q1", Day(15));
            record.AddProgress("win_match", 1);
            record.RecordClaim(Day(15));

            var plan = QuestCalculator.PlanReport(set.Quests[0], record, true, "win_match", 1,
                Day(15));

            Assert.IsNull(plan);
        }

        [Test]
        public void PlanReport_LockedQuest_IgnoresReports()
        {
            var set = QuestSetJson.Chain();

            var plan = QuestCalculator.PlanReport(set.Quests[1], null, false, "win_match", 1,
                Day(15));

            Assert.IsNull(plan);
        }

        [Test]
        public void PlanClaim_NotStarted_IsNotCompleted()
        {
            var set = QuestSetJson.Single(target: 3);

            var plan = QuestCalculator.PlanClaim(set.Quests[0], null, true, Day(15));

            Assert.AreEqual(QuestClaimResult.NotCompleted, plan.Outcome);
        }

        [Test]
        public void PlanClaim_Completed_IsClaimable()
        {
            var set = QuestSetJson.Single(target: 3);
            var record = new QuestRecord("q1", Day(15));
            record.AddProgress("win_match", 3);

            var plan = QuestCalculator.PlanClaim(set.Quests[0], record, true, Day(15));

            Assert.AreEqual(QuestClaimResult.Claimable, plan.Outcome);
        }

        [Test]
        public void PlanClaim_AlreadyClaimed_IsAlreadyClaimed()
        {
            var set = QuestSetJson.Single(target: 3);
            var record = new QuestRecord("q1", Day(15));
            record.AddProgress("win_match", 3);
            record.RecordClaim(Day(15));

            var plan = QuestCalculator.PlanClaim(set.Quests[0], record, true, Day(15));

            Assert.AreEqual(QuestClaimResult.AlreadyClaimed, plan.Outcome);
        }

        [Test]
        public void PlanClaim_UnclaimedPrerequisite_IsLocked()
        {
            var set = QuestSetJson.Chain();

            var plan = QuestCalculator.PlanClaim(set.Quests[1], null, false, Day(15));

            Assert.AreEqual(QuestClaimResult.Locked, plan.Outcome);
        }

        [Test]
        public void PlanClaim_StaleRecordForOldPeriod_IsNotCompleted()
        {
            var set = QuestSetJson.Single(target: 3);
            var record = new QuestRecord("q1", Day(14));
            record.AddProgress("win_match", 3);

            // The record belongs to yesterday; today it reads as not completed.
            var plan = QuestCalculator.PlanClaim(set.Quests[0], record, true, Day(15));

            Assert.AreEqual(QuestClaimResult.NotCompleted, plan.Outcome);
        }

        [Test]
        public void GetNextResetUnix_MixedSet_PicksTheSoonestBoundary()
        {
            var set = QuestSetJson.Mixed();
            var now = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

            var next = QuestCalculator.GetNextResetUnix(set.Quests, now.ToUnixTimestamp(), 0, 1);

            // The daily quest resets at UTC midnight tomorrow; that beats the weekly reset.
            Assert.AreEqual(new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc),
                QuestTime.FromUnix(next));
        }

        private static long Day(int day) =>
            new DateTime(2026, 6, day, 10, 0, 0, DateTimeKind.Utc).ToUnixTimestamp();
    }
}
