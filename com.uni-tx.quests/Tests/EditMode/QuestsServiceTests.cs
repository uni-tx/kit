using System;
using System.Threading;
using NUnit.Framework;

namespace UniTx.Quests.Tests
{
    public sealed class QuestsServiceTests
    {
        private static readonly DateTime Noon = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Initialize_LoadsSetAndSave_QuestsAreAvailable()
        {
            var harness = Harness.Create(Noon);

            Assert.IsTrue(harness.Service.IsReady);
            Assert.IsNotNull(harness.Service.Set);
            Assert.AreEqual(QuestSetJson.SetId, harness.Service.Set.Id);
            Assert.AreEqual(QuestSetJson.SetId, harness.Service.Snapshot.SetId);
            Assert.AreEqual(1, harness.Service.Snapshot.Quests.Count);
            Assert.AreEqual(QuestState.Available, harness.Service.Snapshot.Quests[0].State);
        }

        [Test]
        public void ReportProgress_AdvancesMatchingObjective()
        {
            var harness = Harness.Create(Noon);

            var advanced = harness.Service.ReportProgressAsync("win_match", 2)
                .GetAwaiter().GetResult();

            Assert.AreEqual(1, advanced);

            var snapshot = harness.Service.Snapshot.Quests[0];

            Assert.AreEqual(QuestState.InProgress, snapshot.State);
            Assert.AreEqual(1, snapshot.Objectives.Count);
            Assert.AreEqual(2, snapshot.Objectives[0].Current);
            Assert.AreEqual(3, snapshot.Objectives[0].Objective.Target);
        }

        [Test]
        public void ReportProgress_UnmatchedKey_AdvancesNothing()
        {
            var harness = Harness.Create(Noon);

            var advanced = harness.Service.ReportProgressAsync("spend_coins", 5)
                .GetAwaiter().GetResult();

            Assert.AreEqual(0, advanced);
            Assert.AreEqual(QuestState.Available, harness.Service.Snapshot.Quests[0].State);
        }

        [Test]
        public void ReportProgress_Overflow_CapsAtTheTarget()
        {
            var harness = Harness.Create(Noon);

            harness.Service.ReportProgressAsync("win_match", 10).GetAwaiter().GetResult();

            var snapshot = harness.Service.Snapshot.Quests[0];

            Assert.AreEqual(3, snapshot.Objectives[0].Current);
            Assert.AreEqual(QuestState.Completed, snapshot.State);
            Assert.IsTrue(snapshot.IsClaimable);
        }

        [Test]
        public void ReportProgress_CompletingTheQuest_RaisesCompletion()
        {
            var harness = Harness.Create(Noon);

            harness.Service.ReportProgressAsync("win_match", 3).GetAwaiter().GetResult();

            var snapshot = harness.Service.Snapshot.Quests[0];

            Assert.AreEqual(QuestState.Completed, snapshot.State);
            Assert.AreEqual(1, snapshot.CompletedObjectives);
            Assert.AreEqual(1, snapshot.TotalObjectives);
        }

        [Test]
        public void Claim_CompletedQuest_GrantsRewardsOnce()
        {
            var harness = Harness.Create(Noon);

            harness.Service.ReportProgressAsync("win_match", 3).GetAwaiter().GetResult();

            var result = harness.Service.ClaimAsync("q1").GetAwaiter().GetResult();

            Assert.AreEqual(QuestClaimResult.Claimed, result);
            Assert.AreEqual(1, harness.Granter.Granted.Count);
            Assert.AreEqual("q1", harness.Granter.Granted[0].QuestId);
            Assert.AreEqual(1, harness.Granter.GrantIds.Count);

            var snapshot = harness.Service.Snapshot.Quests[0];

            Assert.AreEqual(QuestState.Claimed, snapshot.State);
            Assert.IsFalse(snapshot.IsClaimable);
        }

        [Test]
        public void Claim_IncompleteQuest_IsRefused()
        {
            var harness = Harness.Create(Noon);

            var result = harness.Service.ClaimAsync("q1").GetAwaiter().GetResult();

            Assert.AreEqual(QuestClaimResult.NotCompleted, result);
            Assert.AreEqual(0, harness.Granter.Granted.Count);
        }

        [Test]
        public void Claim_UnknownQuest_IsRefused()
        {
            var harness = Harness.Create(Noon);

            var result = harness.Service.ClaimAsync("nope").GetAwaiter().GetResult();

            Assert.AreEqual(QuestClaimResult.NoQuest, result);
        }

        [Test]
        public void Claim_Twice_SecondIsAlreadyClaimed()
        {
            var harness = Harness.Create(Noon);

            harness.Service.ReportProgressAsync("win_match", 3).GetAwaiter().GetResult();

            Assert.AreEqual(QuestClaimResult.Claimed,
                harness.Service.ClaimAsync("q1").GetAwaiter().GetResult());
            Assert.AreEqual(QuestClaimResult.AlreadyClaimed,
                harness.Service.ClaimAsync("q1").GetAwaiter().GetResult());

            // One reward delivery, one grant id.
            Assert.AreEqual(1, harness.Granter.Granted.Count);
            Assert.AreEqual(1, harness.Granter.GrantIds.Count);
        }

        [Test]
        public void Claim_LockedQuest_IsRefused()
        {
            var set = QuestSetJson.Chain();
            var harness = Harness.Create(Noon, set);

            // q2 requires q1, which is neither completed nor claimed.
            var result = harness.Service.ClaimAsync("q2").GetAwaiter().GetResult();

            Assert.AreEqual(QuestClaimResult.Locked, result);
        }

        [Test]
        public void PrerequisiteChain_CompletesInOrder()
        {
            var set = QuestSetJson.Chain();
            var harness = Harness.Create(Noon, set);

            Assert.AreEqual(QuestState.Locked, harness.Service.Snapshot.Quests[1].State);

            // q2 ignores reports while locked.
            harness.Service.ReportProgressAsync("win_match", 1).GetAwaiter().GetResult();
            Assert.AreEqual(QuestState.Locked, harness.Service.Snapshot.Quests[1].State);

            // Complete and claim q1; q2 unlocks and becomes claimable.
            harness.Service.ReportProgressAsync("tutorial", 1).GetAwaiter().GetResult();
            harness.Service.ClaimAsync("q1").GetAwaiter().GetResult();

            Assert.AreEqual(QuestState.Available, harness.Service.Snapshot.Quests[1].State);

            harness.Service.ReportProgressAsync("win_match", 1).GetAwaiter().GetResult();

            Assert.AreEqual(QuestState.Completed, harness.Service.Snapshot.Quests[1].State);
        }

        [Test]
        public void DailyReset_RollsProgressOverAtMidnight()
        {
            var harness = Harness.Create(Noon);

            harness.Service.ReportProgressAsync("win_match", 3).GetAwaiter().GetResult();
            harness.Service.ClaimAsync("q1").GetAwaiter().GetResult();

            Assert.AreEqual(QuestState.Claimed, harness.Service.Snapshot.Quests[0].State);

            // Next day: the daily quest rolls over and is fresh again.
            harness.Clock.Advance(TimeSpan.FromDays(1));

            harness.Service.RefreshAsync().GetAwaiter().GetResult();

            var snapshot = harness.Service.Snapshot.Quests[0];

            Assert.AreEqual(QuestState.Available, snapshot.State);
            Assert.AreEqual(0, snapshot.Objectives[0].Current);
        }

        [Test]
        public void OneTimeQuest_NeverRollsOver()
        {
            var set = QuestSetJson.Single(reset: (int)QuestReset.None);
            var harness = Harness.Create(Noon, set);

            harness.Service.ReportProgressAsync("win_match", 3).GetAwaiter().GetResult();
            harness.Service.ClaimAsync("q1").GetAwaiter().GetResult();

            harness.Clock.Advance(TimeSpan.FromDays(3));
            harness.Service.RefreshAsync().GetAwaiter().GetResult();

            Assert.AreEqual(QuestState.Claimed, harness.Service.Snapshot.Quests[0].State);
        }

        [Test]
        public void WeeklyReset_RollsOverOnTheWeekStartBoundary()
        {
            var set = QuestSetJson.Single(reset: (int)QuestReset.Weekly);
            var harness = Harness.Create(Noon, set);

            harness.Service.ReportProgressAsync("win_match", 1).GetAwaiter().GetResult();

            // Later in the same week: progress holds.
            harness.Clock.Advance(TimeSpan.FromDays(2));
            harness.Service.RefreshAsync().GetAwaiter().GetResult();

            Assert.AreEqual(QuestState.InProgress, harness.Service.Snapshot.Quests[0].State);

            // Jump past the next Monday boundary (the configured week start): it resets.
            harness.Clock.Advance(TimeSpan.FromDays(6));
            harness.Service.RefreshAsync().GetAwaiter().GetResult();

            Assert.AreEqual(QuestState.Available, harness.Service.Snapshot.Quests[0].State);
        }

        [Test]
        public void Claim_GranterRefuses_ThenTheSameQuestIsRetried()
        {
            var harness = Harness.Create(Noon);
            harness.Granter.ShouldFail = true;

            harness.Service.ReportProgressAsync("win_match", 3).GetAwaiter().GetResult();

            var first = harness.Service.ClaimAsync("q1").GetAwaiter().GetResult();

            Assert.AreEqual(QuestClaimResult.GrantFailed, first);
            Assert.AreEqual(0, harness.Granter.Granted.Count);
            Assert.AreEqual(QuestState.Completed, harness.Service.Snapshot.Quests[0].State);

            harness.Granter.ShouldFail = false;

            var second = harness.Service.ClaimAsync("q1").GetAwaiter().GetResult();

            Assert.AreEqual(QuestClaimResult.Claimed, second);
            Assert.AreEqual(1, harness.Granter.Granted.Count);
            Assert.AreEqual(QuestState.Claimed, harness.Service.Snapshot.Quests[0].State);
        }

        [Test]
        public void Refresh_RetriesAFailedClaimFromThePreviousSession()
        {
            var harness = Harness.Create(Noon);
            harness.Granter.ShouldFail = true;

            harness.Service.ReportProgressAsync("win_match", 3).GetAwaiter().GetResult();

            Assert.AreEqual(QuestClaimResult.GrantFailed,
                harness.Service.ClaimAsync("q1").GetAwaiter().GetResult());

            harness.Granter.ShouldFail = false;

            harness.Service.RefreshAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, harness.Granter.Granted.Count);
            Assert.AreEqual(QuestState.Claimed, harness.Service.Snapshot.Quests[0].State);
        }

        [Test]
        public void ClockWoundBack_CannotReclaimAQuest()
        {
            var harness = Harness.Create(Noon);

            harness.Service.ReportProgressAsync("win_match", 3).GetAwaiter().GetResult();
            harness.Service.ClaimAsync("q1").GetAwaiter().GetResult();

            // The device clock is wound back a day; the high-water mark keeps time forward.
            harness.Clock.UtcNow = Noon.AddDays(-1);

            Assert.AreEqual(QuestClaimResult.AlreadyClaimed,
                harness.Service.ClaimAsync("q1").GetAwaiter().GetResult());
        }

        [Test]
        public void SetReplacement_ResetsProgress_KeepsGrantLedger()
        {
            var harness = Harness.Create(Noon);

            harness.Service.ReportProgressAsync("win_match", 3).GetAwaiter().GetResult();
            harness.Service.ClaimAsync("q1").GetAwaiter().GetResult();

            var grantId = harness.Granter.GrantIds[0];

            // A new board ships under a different id.
            harness.Content.Add(QuestSetJson.Single(questId: "q2", setId: "quests_v2"));
            harness.Content.Remove(QuestSetJson.SetId);

            harness.Service.RefreshAsync().GetAwaiter().GetResult();

            Assert.AreEqual("quests_v2", harness.Service.Set.Id);
            Assert.AreEqual(QuestState.Available, harness.Service.Snapshot.Quests[0].State);
            Assert.IsTrue(harness.Service.SavedData.HasAppliedGrant(grantId));
        }

        [Test]
        public void GrantId_IsScopedToTheSetQuestAndPeriod()
        {
            var harness = Harness.Create(Noon);

            harness.Service.ReportProgressAsync("win_match", 3).GetAwaiter().GetResult();
            harness.Service.ClaimAsync("q1").GetAwaiter().GetResult();

            Assert.IsNotEmpty(harness.Granter.GrantIds);
            StringAssert.StartsWith($"quest:{QuestSetJson.SetId}:q1:",
                harness.Granter.GrantIds[0]);
            Assert.IsTrue(harness.Service.SavedData.HasAppliedGrant(harness.Granter.GrantIds[0]));
        }

        [Test]
        public void Snapshot_ReportsRemainingTimeToNextReset()
        {
            var harness = Harness.Create(Noon);

            var snapshot = harness.Service.Snapshot;

            Assert.Greater(snapshot.NextResetUnix, 0);
            Assert.Greater(snapshot.RemainingSeconds, 0);
            Assert.LessOrEqual(snapshot.RemainingSeconds, QuestTime.SecondsPerDay);
        }

        /// <summary>
        /// A fully wired service over in-memory dependencies.
        /// </summary>
        private sealed class Harness
        {
            public FakeClock Clock;
            public FakeContentService Content;
            public FakeSerialisationService Serialisation;
            public FakeBackend Backend;
            public RecordingGranter Granter;
            public QuestsService Service;

            public static Harness Create(DateTime now, QuestSetData set = null,
                UniQuestsConfig config = null)
            {
                var harness = new Harness
                {
                    Clock = new FakeClock(now),
                    Content = new FakeContentService(),
                    Serialisation = new FakeSerialisationService(),
                };

                harness.Content.Add(set ?? QuestSetJson.Single());
                harness.Backend = new FakeBackend(harness.Serialisation);
                harness.Granter = new RecordingGranter();
                harness.Service = new QuestsService(harness.Clock, harness.Content,
                    harness.Backend, config ?? ConfigFactory.Create());
                harness.Service.SetRewardGranter(harness.Granter);

                harness.Service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

                return harness;
            }
        }
    }
}
