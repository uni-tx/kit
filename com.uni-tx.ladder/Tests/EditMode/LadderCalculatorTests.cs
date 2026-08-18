using NUnit.Framework;

namespace UniTx.Ladder.Tests
{
    /// <summary>
    /// The pure math: rung states, claim planning and progress rendering.
    /// </summary>
    public sealed class LadderCalculatorTests
    {
        [Test]
        public void EvaluateState_BelowThreshold_IsLocked()
        {
            var ladder = LadderJson.ThreeRungs();
            var rung = ladder.GetRung("r2");

            var state = LadderCalculator.EvaluateState(rung, null, 2);

            Assert.AreEqual(LadderState.Locked, state);
        }

        [Test]
        public void EvaluateState_AtThreshold_IsReached()
        {
            var ladder = LadderJson.ThreeRungs();
            var rung = ladder.GetRung("r2");

            var state = LadderCalculator.EvaluateState(rung, null, 3);

            Assert.AreEqual(LadderState.Reached, state);
        }

        [Test]
        public void EvaluateState_OverThreshold_IsReached()
        {
            var ladder = LadderJson.ThreeRungs();
            var rung = ladder.GetRung("r2");

            var state = LadderCalculator.EvaluateState(rung, null, 4);

            Assert.AreEqual(LadderState.Reached, state);
        }

        [Test]
        public void EvaluateState_Claimed_StaysClaimed()
        {
            var ladder = LadderJson.ThreeRungs();
            var rung = ladder.GetRung("r1");
            var record = new LadderRungRecord("r1");
            record.RecordClaim();

            var state = LadderCalculator.EvaluateState(rung, record, 0);

            Assert.AreEqual(LadderState.Claimed, state);
        }

        [Test]
        public void PlanClaim_BelowThreshold_NotReached()
        {
            var ladder = LadderJson.ThreeRungs();

            var plan = LadderCalculator.PlanClaim(ladder.GetRung("r2"), null, 2);

            Assert.AreEqual(LadderClaimResult.NotReached, plan.Outcome);
        }

        [Test]
        public void PlanClaim_AtThreshold_Claimable()
        {
            var ladder = LadderJson.ThreeRungs();

            var plan = LadderCalculator.PlanClaim(ladder.GetRung("r2"), null, 3);

            Assert.AreEqual(LadderClaimResult.Claimable, plan.Outcome);
        }

        [Test]
        public void PlanClaim_AlreadyClaimed_IsReported()
        {
            var ladder = LadderJson.ThreeRungs();
            var record = new LadderRungRecord("r1");
            record.RecordClaim();

            var plan = LadderCalculator.PlanClaim(ladder.GetRung("r1"), record, 1);

            Assert.AreEqual(LadderClaimResult.AlreadyClaimed, plan.Outcome);
        }

        [Test]
        public void PlanClaim_InvalidRung_Rejected()
        {
            var ladder = LadderJson.ThreeRungs();
            var rung = ladder.GetRung("r1");
            rung.GetType().GetField("_steps",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(rung, 0);

            var plan = LadderCalculator.PlanClaim(rung, null, 0);

            Assert.AreEqual(LadderClaimResult.Rejected, plan.Outcome);
        }

        [Test]
        public void GetProgress_NoProgress_PointsAtFirstRung()
        {
            var ladder = LadderJson.ThreeRungs();
            var saved = new LadderSavedData();

            var progress = LadderCalculator.GetProgress(ladder, saved);

            Assert.AreEqual(1, progress.NextRungSteps);
            Assert.AreEqual(0f, progress.Progress, 0.0001f);
            Assert.IsFalse(progress.IsComplete);
        }

        [Test]
        public void GetProgress_Partway_BetweenRungs()
        {
            var ladder = LadderJson.ThreeRungs();
            var saved = new LadderSavedData();
            saved.AddSteps(2);

            var progress = LadderCalculator.GetProgress(ladder, saved);

            // Between rung one (1) and rung two (3): halfway.
            Assert.AreEqual(3, progress.NextRungSteps);
            Assert.AreEqual(0.5f, progress.Progress, 0.0001f);
            Assert.IsFalse(progress.IsComplete);
        }

        [Test]
        public void GetProgress_AllClaimed_IsComplete()
        {
            var ladder = LadderJson.ThreeRungs();
            var saved = new LadderSavedData();
            saved.AddSteps(5);

            foreach (var rung in ladder.Rungs)
            {
                var record = saved.GetOrCreateRecord(rung.Id);
                record.RecordClaim();
            }

            var progress = LadderCalculator.GetProgress(ladder, saved);

            Assert.AreEqual(0, progress.NextRungSteps);
            Assert.AreEqual(1f, progress.Progress, 0.0001f);
            Assert.IsTrue(progress.IsComplete);
        }

        [Test]
        public void LadderData_RungsAreSortedByCumulativeSteps()
        {
            var ladder = LadderJson.Parse(
                $@"{{
                  ""_id"": ""unsorted"",
                  ""_displayName"": ""Unsorted"",
                  ""_rungs"": [
                    {{ ""_id"": ""high"", ""_displayName"": ""High"", ""_steps"": 50,
                       ""_rewards"": [{{ ""_rewardId"": ""r1"", ""_kind"": 0, ""_itemId"": ""c"", ""_amount"": 1 }}] }},
                    {{ ""_id"": ""low"", ""_displayName"": ""Low"", ""_steps"": 5,
                       ""_rewards"": [{{ ""_rewardId"": ""r2"", ""_kind"": 0, ""_itemId"": ""c"", ""_amount"": 1 }}] }},
                    {{ ""_id"": ""mid"", ""_displayName"": ""Mid"", ""_steps"": 25,
                       ""_rewards"": [{{ ""_rewardId"": ""r3"", ""_kind"": 0, ""_itemId"": ""c"", ""_amount"": 1 }}] }}
                  ]
                }}");

            Assert.AreEqual("low", ladder.Rungs[0].Id);
            Assert.AreEqual("mid", ladder.Rungs[1].Id);
            Assert.AreEqual("high", ladder.Rungs[2].Id);
            Assert.AreSame(ladder.TopRung, ladder.Rungs[2]);
        }

        [Test]
        public void LadderData_IsTop_OnlyForTheGrandPrize()
        {
            var ladder = LadderJson.ThreeRungs();

            Assert.IsFalse(ladder.IsTop(ladder.GetRung("r1")));
            Assert.IsFalse(ladder.IsTop(ladder.GetRung("r2")));
            Assert.IsTrue(ladder.IsTop(ladder.GetRung("r3")));
        }

        [Test]
        public void LadderData_DescribeProblems_ReportsDuplicateThresholds()
        {
            var ladder = LadderJson.DuplicateThresholds();

            var problems = ladder.DescribeProblems();

            Assert.That(problems, Does.Contain("same step threshold"));
        }

        [Test]
        public void LadderData_DescribeProblems_ReportsEmptyLadder()
        {
            var ladder = LadderJson.Empty();

            var problems = ladder.DescribeProblems();

            Assert.That(problems, Does.Contain("no rungs are defined"));
        }

        [Test]
        public void LadderData_DescribeProblems_CleanLadderIsEmpty()
        {
            var ladder = LadderJson.ThreeRungs();

            Assert.AreEqual(string.Empty, ladder.DescribeProblems());
        }
    }
}
