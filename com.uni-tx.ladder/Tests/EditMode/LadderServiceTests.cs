using System.Threading;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace UniTx.Ladder.Tests
{
    /// <summary>
    /// The service rules: climbing, claims that only land after delivery, retries and
    /// ladder replacement.
    /// </summary>
    public sealed class LadderServiceTests
    {
        [Test]
        public void Initialize_LoadsLadderAndSave_IsReady()
        {
            var harness = Harness.Create();

            Assert.IsTrue(harness.Service.IsReady);
            Assert.IsNotNull(harness.Service.Ladder);
            Assert.AreEqual(LadderJson.LadderId, harness.Service.Ladder.Id);
            Assert.AreEqual(LadderJson.LadderId, harness.Service.Snapshot.LadderId);
            Assert.AreEqual(3, harness.Service.Snapshot.Rungs.Count);
            Assert.AreEqual(LadderState.Locked, harness.Service.Snapshot.Rungs[0].State);
        }

        [Test]
        public void ReportSteps_CrossingOneRung_ReportsOneReached()
        {
            var harness = Harness.Create();

            var reached = harness.Service.ReportStepsAsync(1)
                .GetAwaiter().GetResult();

            Assert.AreEqual(1, reached);
            Assert.AreEqual(1, harness.Service.SavedData.TotalSteps);
            Assert.AreEqual(LadderState.Reached, harness.Service.Snapshot.Rungs[0].State);
            Assert.AreEqual(LadderState.Locked, harness.Service.Snapshot.Rungs[1].State);
        }

        [Test]
        public void ReportSteps_CrossingTwoRungs_ReportsTwoReached()
        {
            var harness = Harness.Create();

            var reached = harness.Service.ReportStepsAsync(3)
                .GetAwaiter().GetResult();

            Assert.AreEqual(2, reached);
            Assert.AreEqual(3, harness.Service.SavedData.TotalSteps);
            Assert.AreEqual(LadderState.Reached, harness.Service.Snapshot.Rungs[0].State);
            Assert.AreEqual(LadderState.Reached, harness.Service.Snapshot.Rungs[1].State);
            Assert.AreEqual(LadderState.Locked, harness.Service.Snapshot.Rungs[2].State);
        }

        [Test]
        public void ReportSteps_NegativeOrZero_DoesNothing()
        {
            var harness = Harness.Create();

            var reached = harness.Service.ReportStepsAsync(0)
                .GetAwaiter().GetResult();

            Assert.AreEqual(0, reached);
            Assert.AreEqual(0, harness.Service.SavedData.TotalSteps);
        }

        [Test]
        public void Claim_BelowThreshold_NotReached()
        {
            var harness = Harness.Create();

            var result = harness.Service.ClaimAsync("r2").GetAwaiter().GetResult();

            Assert.AreEqual(LadderClaimResult.NotReached, result);
            Assert.AreEqual(0, harness.Granter.CountFor("r2"));
        }

        [Test]
        public void Claim_AtThreshold_DeliversAndRecords()
        {
            var harness = Harness.Create();

            harness.Service.ReportStepsAsync(1).GetAwaiter().GetResult();

            var result = harness.Service.ClaimAsync("r1").GetAwaiter().GetResult();

            Assert.AreEqual(LadderClaimResult.Claimed, result);
            Assert.AreEqual(1, harness.Granter.CountFor("r1"));
            Assert.IsTrue(harness.Service.SavedData.GetRecord("r1").IsClaimed);
            Assert.AreEqual(LadderState.Claimed, harness.Service.Snapshot.Rungs[0].State);
        }

        [Test]
        public void Claim_TopRung_CompletesTheLadder()
        {
            var harness = Harness.Create();

            harness.Service.ReportStepsAsync(5).GetAwaiter().GetResult();

            var result = harness.Service.ClaimAsync("r3").GetAwaiter().GetResult();

            Assert.AreEqual(LadderClaimResult.Claimed, result);
            Assert.IsTrue(harness.Service.Snapshot.IsComplete);
        }

        [Test]
        public void Claim_Twice_SecondIsAlreadyClaimed()
        {
            var harness = Harness.Create();

            harness.Service.ReportStepsAsync(1).GetAwaiter().GetResult();
            harness.Service.ClaimAsync("r1").GetAwaiter().GetResult();

            var second = harness.Service.ClaimAsync("r1").GetAwaiter().GetResult();

            Assert.AreEqual(LadderClaimResult.AlreadyClaimed, second);
            Assert.AreEqual(1, harness.Granter.CountFor("r1"));
        }

        [Test]
        public void Claim_UnknownRung_NoRung()
        {
            var harness = Harness.Create();

            var result = harness.Service.ClaimAsync("missing").GetAwaiter().GetResult();

            Assert.AreEqual(LadderClaimResult.NoRung, result);
        }

        [Test]
        public void Claim_GranterFails_LeavesClaimableAndRetriesOnRefresh()
        {
            var harness = Harness.Create();
            harness.Granter.ShouldFail = true;

            harness.Service.ReportStepsAsync(1).GetAwaiter().GetResult();

            var first = harness.Service.ClaimAsync("r1").GetAwaiter().GetResult();

            Assert.AreEqual(LadderClaimResult.GrantFailed, first);
            Assert.IsFalse(harness.Service.SavedData.GetRecord("r1").IsClaimed);
            Assert.IsTrue(harness.Service.SavedData.GetRecord("r1").IsFailed);

            harness.Granter.ShouldFail = false;

            harness.Service.RefreshAsync().GetAwaiter().GetResult();

            Assert.IsTrue(harness.Service.SavedData.GetRecord("r1").IsClaimed);
            Assert.AreEqual(1, harness.Granter.CountFor("r1"));
        }

        [Test]
        public void Claim_GranterThrows_LeavesClaimable()
        {
            var harness = Harness.Create();
            harness.Granter.ShouldThrow = true;

            harness.Service.ReportStepsAsync(1).GetAwaiter().GetResult();

            // The service logs the granter's exception before treating it as a failure;
            // the throw is the point of the test, so the log is expected noise.
            LogAssert.ignoreFailingMessages = true;

            var result = harness.Service.ClaimAsync("r1").GetAwaiter().GetResult();

            Assert.AreEqual(LadderClaimResult.GrantFailed, result);
            Assert.IsFalse(harness.Service.SavedData.GetRecord("r1").IsClaimed);
        }

        [Test]
        public void Refresh_Persists_WithoutClaimingAnything()
        {
            var harness = Harness.Create();

            harness.Service.ReportStepsAsync(1).GetAwaiter().GetResult();

            var savesBefore = harness.Serialisation.SaveCount;

            harness.Service.RefreshAsync().GetAwaiter().GetResult();

            Assert.Greater(harness.Serialisation.SaveCount, savesBefore);
            Assert.AreEqual(0, harness.Granter.Granted.Count);
        }

        [Test]
        public void ReplacementLadder_RestartsClimb_KeepsGrantLedger()
        {
            var harness = Harness.Create();

            harness.Service.ReportStepsAsync(5).GetAwaiter().GetResult();
            harness.Service.ClaimAsync("r1").GetAwaiter().GetResult();

            var grantIdBefore = harness.Granter.GrantIds[0];

            harness.Content.Remove(LadderJson.LadderId);
            harness.Content.Add(LadderJson.ThreeRungs("ladder_v2"));

            harness.Service.RefreshAsync().GetAwaiter().GetResult();

            Assert.AreEqual(0, harness.Service.SavedData.TotalSteps);
            Assert.AreEqual("ladder_v2", harness.Service.SavedData.LadderId);
            Assert.IsTrue(harness.Service.SavedData.HasAppliedGrant(grantIdBefore));

            // The same rung id on the new ladder is a fresh claim — a replay of the old
            // grant must not double-pay, but a new climb may claim again.
            harness.Service.ReportStepsAsync(1).GetAwaiter().GetResult();
            var result = harness.Service.ClaimAsync("r1").GetAwaiter().GetResult();

            Assert.AreEqual(LadderClaimResult.Claimed, result);
            Assert.AreEqual(2, harness.Granter.CountFor("r1"));
        }

        [Test]
        public void Snapshot_Progress_ReflectsClimb()
        {
            var harness = Harness.Create();

            Assert.AreEqual(1, harness.Service.Snapshot.NextRungSteps);
            Assert.AreEqual(0f, harness.Service.Snapshot.Progress, 0.0001f);

            harness.Service.ReportStepsAsync(2).GetAwaiter().GetResult();

            Assert.AreEqual(3, harness.Service.Snapshot.NextRungSteps);
            Assert.AreEqual(0.5f, harness.Service.Snapshot.Progress, 0.0001f);
        }

        [Test]
        public void Initialize_NoLadderRegistered_IsReadyWithNoLadder()
        {
            var harness = Harness.Create(noLadder: true);

            Assert.IsTrue(harness.Service.IsReady);
            Assert.IsNull(harness.Service.Ladder);
            Assert.IsNull(harness.Service.Snapshot.LadderId);

            var result = harness.Service.ClaimAsync("r1").GetAwaiter().GetResult();

            Assert.AreEqual(LadderClaimResult.NoLadder, result);
        }

        [Test]
        public void GrantId_IsScopedToTheLadderAndRung()
        {
            var harness = Harness.Create();

            harness.Service.ReportStepsAsync(1).GetAwaiter().GetResult();
            harness.Service.ClaimAsync("r1").GetAwaiter().GetResult();

            Assert.IsNotEmpty(harness.Granter.GrantIds);
            StringAssert.StartsWith($"ladder:{LadderJson.LadderId}:r1:",
                harness.Granter.GrantIds[0]);
            Assert.IsTrue(harness.Service.SavedData.HasAppliedGrant(harness.Granter.GrantIds[0]));
        }

        /// <summary>
        /// A fully wired service over in-memory dependencies.
        /// </summary>
        private sealed class Harness
        {
            public FakeSerialisationService Serialisation;
            public FakeContentService Content;
            public FakeBackend Backend;
            public RecordingGranter Granter;
            public LadderService Service;

            public static Harness Create(bool noLadder = false)
            {
                var harness = new Harness
                {
                    Serialisation = new FakeSerialisationService(),
                    Content = new FakeContentService(),
                };

                if (!noLadder) harness.Content.Add(LadderJson.ThreeRungs());

                harness.Backend = new FakeBackend(harness.Serialisation);
                harness.Granter = new RecordingGranter();
                harness.Service = new LadderService(harness.Content, harness.Backend,
                    ConfigFactory.Create());
                harness.Service.SetRewardGranter(harness.Granter);

                harness.Service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

                return harness;
            }
        }
    }
}
