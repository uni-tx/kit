using System;
using System.Threading;
using NUnit.Framework;
using UniTx.Ads;
using UniTx.Iap;
using UnityEngine.TestTools;

namespace UniTx.Store.Tests
{
    /// <summary>
    /// The service rules: claims that only land after delivery, cooldowns, limits,
    /// IAP entitlements and retries.
    /// </summary>
    public sealed class StoreServiceTests
    {
        [SetUp]
        public void SetUp()
        {
            // The store forwards rewarded and IAP offers through the kit's static facades,
            // so the tests install simulated providers: rewarded ads always complete, and
            // the billing provider sells nothing (resolving to Unsupported). SyncAdsProvider
            // completes instantly — EditMode has no player loop, so NoOpAdsProvider's
            // simulated delay would never elapse.
            UniAds.Reset();
            UniAds.InitializeAsync(new SyncAdsProvider(AdResult.Completed),
                null, CancellationToken.None).GetAwaiter().GetResult();

            UniIap.Reset();
            UniIap.InitializeAsync(new NoOpIapProvider(), null, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        [TearDown]
        public void TearDown()
        {
            UniAds.Reset();
            UniIap.Reset();
        }

        [Test]
        public void Initialize_LoadsStoreAndSave_IsReady()
        {
            var harness = Harness.Create();

            Assert.IsTrue(harness.Service.IsReady);
            Assert.IsNotNull(harness.Service.Store);
            Assert.AreEqual(StoreJson.StoreId, harness.Service.Store.Id);
            Assert.AreEqual(StoreJson.StoreId, harness.Service.Snapshot.StoreId);
            Assert.AreEqual(3, harness.Service.Snapshot.Offers.Count);
            Assert.AreEqual(StoreOfferState.Ready, harness.Service.Snapshot.Offers[0].State);
        }

        [Test]
        public void Claim_FreeOffer_DeliversAndRecords()
        {
            var harness = Harness.Create();

            var result = harness.Service.ClaimAsync("free1")
                .GetAwaiter().GetResult();

            Assert.AreEqual(StoreClaimResult.Claimed, result);
            Assert.AreEqual(1, harness.Granter.CountFor("free1"));
            Assert.AreEqual(1, harness.Service.SavedData.GetOrCreateRecord("free1").ClaimCount);
            // free1 has a 60s cooldown, so right after the claim it is waiting it out.
            Assert.AreEqual(StoreOfferState.OnCooldown, harness.Service.Snapshot.Offers[0].State);
        }

        [Test]
        public void Claim_FreeOffer_TwiceImmediately_SecondIsOnCooldown()
        {
            var harness = Harness.Create();

            harness.Service.ClaimAsync("free1").GetAwaiter().GetResult();

            var second = harness.Service.ClaimAsync("free1").GetAwaiter().GetResult();

            Assert.AreEqual(StoreClaimResult.OnCooldown, second);
            Assert.AreEqual(1, harness.Granter.CountFor("free1"));
        }

        [Test]
        public void Claim_FreeOffer_AfterCooldown_ClaimsAgain()
        {
            var harness = Harness.Create();

            harness.Service.ClaimAsync("free1").GetAwaiter().GetResult();
            harness.Clock.Advance(TimeSpan.FromSeconds(61));

            var second = harness.Service.ClaimAsync("free1").GetAwaiter().GetResult();

            Assert.AreEqual(StoreClaimResult.Claimed, second);
            Assert.AreEqual(2, harness.Granter.CountFor("free1"));
            Assert.AreEqual(2, harness.Service.SavedData.GetOrCreateRecord("free1").ClaimCount);
        }

        [Test]
        public void Claim_FreeOffer_AtLimit_IsLimitReached()
        {
            var harness = Harness.Create();

            for (var i = 0; i < 3; i++)
            {
                var result = harness.Service.ClaimAsync("free1").GetAwaiter().GetResult();
                Assert.AreEqual(StoreClaimResult.Claimed, result);
                harness.Clock.Advance(TimeSpan.FromSeconds(61));
            }

            var fourth = harness.Service.ClaimAsync("free1").GetAwaiter().GetResult();

            Assert.AreEqual(StoreClaimResult.LimitReached, fourth);
            Assert.AreEqual(3, harness.Granter.CountFor("free1"));
            Assert.AreEqual(StoreOfferState.LimitReached, harness.Service.Snapshot.Offers[0].State);
        }

        [Test]
        public void Claim_RewardedOffer_AdCompletes_Delivers()
        {
            var harness = Harness.Create();

            var result = harness.Service.ClaimAsync("rewarded1").GetAwaiter().GetResult();

            Assert.AreEqual(StoreClaimResult.Rewarded, result);
            Assert.AreEqual(1, harness.Granter.CountFor("rewarded1"));
            Assert.AreEqual(1, harness.Service.SavedData.GetOrCreateRecord("rewarded1").ClaimCount);
        }

        [Test]
        public void Claim_RewardedOffer_AdSkipped_DoesNotDeliver()
        {
            // Install a provider whose rewarded ads are skipped — the no-reward path.
            UniAds.Reset();
            UniAds.InitializeAsync(new SyncAdsProvider(AdResult.Skipped),
                null, CancellationToken.None).GetAwaiter().GetResult();

            var harness = Harness.Create();

            var result = harness.Service.ClaimAsync("rewarded1").GetAwaiter().GetResult();

            Assert.AreEqual(StoreClaimResult.AdNotCompleted, result);
            Assert.AreEqual(0, harness.Granter.CountFor("rewarded1"));
            Assert.AreEqual(0, harness.Service.SavedData.GetOrCreateRecord("rewarded1").ClaimCount);
        }

        [Test]
        public void Claim_IapOffer_NoProvider_IsUnavailable()
        {
            var harness = Harness.Create();

            var result = harness.Service.ClaimAsync("iap1").GetAwaiter().GetResult();

            Assert.AreEqual(StoreClaimResult.Unavailable, result);
            Assert.AreEqual(0, harness.Granter.CountFor("iap1"));
        }

        [Test]
        public void DeliverIap_WithTransaction_DeliversOnce()
        {
            var harness = Harness.Create();

            var first = harness.Service.DeliverIapAsync("com.test.starter", "txn-1")
                .GetAwaiter().GetResult();

            Assert.AreEqual(StoreClaimResult.Claimed, first);
            Assert.AreEqual(1, harness.Granter.CountFor("iap1"));

            // A restore replaying the same transaction must not pay twice.
            var replay = harness.Service.DeliverIapAsync("com.test.starter", "txn-1")
                .GetAwaiter().GetResult();

            Assert.AreEqual(StoreClaimResult.Claimed, replay);
            Assert.AreEqual(1, harness.Granter.CountFor("iap1"));
        }

        [Test]
        public void DeliverIap_UnknownProduct_IsNoOffer()
        {
            var harness = Harness.Create();

            var result = harness.Service.DeliverIapAsync("com.test.nope", "txn-1")
                .GetAwaiter().GetResult();

            Assert.AreEqual(StoreClaimResult.NoOffer, result);
            Assert.AreEqual(0, harness.Granter.Granted.Count);
        }

        [Test]
        public void Claim_GranterRefuses_StaysClaimableAndRetries()
        {
            var harness = Harness.Create();
            harness.Granter.ShouldFail = true;

            var result = harness.Service.ClaimAsync("free1").GetAwaiter().GetResult();

            Assert.AreEqual(StoreClaimResult.GrantFailed, result);
            Assert.AreEqual(0, harness.Service.SavedData.GetOrCreateRecord("free1").ClaimCount);

            // The failed delivery is retried on the next refresh once the granter recovers.
            harness.Granter.ShouldFail = false;
            harness.Service.RefreshAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, harness.Granter.CountFor("free1"));
            Assert.AreEqual(1, harness.Service.SavedData.GetOrCreateRecord("free1").ClaimCount);
        }

        [Test]
        public void Claim_GranterThrows_StaysClaimable()
        {
            var harness = Harness.Create();
            harness.Granter.ShouldThrow = true;

            // The service logs the granter's exception before treating it as a failure;
            // the throw is the point of the test, so the log is expected noise.
            LogAssert.ignoreFailingMessages = true;

            var result = harness.Service.ClaimAsync("free1").GetAwaiter().GetResult();

            Assert.AreEqual(StoreClaimResult.GrantFailed, result);
            Assert.AreEqual(0, harness.Service.SavedData.GetOrCreateRecord("free1").ClaimCount);
        }

        [Test]
        public void Claim_NoStore_IsNoStore()
        {
            var harness = Harness.Create(noStore: true);

            var result = harness.Service.ClaimAsync("free1").GetAwaiter().GetResult();

            Assert.AreEqual(StoreClaimResult.NoStore, result);
        }

        [Test]
        public void Claim_UnknownOffer_IsNoOffer()
        {
            var harness = Harness.Create();

            var result = harness.Service.ClaimAsync("nope").GetAwaiter().GetResult();

            Assert.AreEqual(StoreClaimResult.NoOffer, result);
        }

        private sealed class Harness
        {
            public FakeClock Clock;
            public FakeSerialisationService Serialisation;
            public FakeContentService Content;
            public FakeBackend Backend;
            public RecordingGranter Granter;
            public StoreService Service;

            public static Harness Create(bool noStore = false)
            {
                var harness = new Harness
                {
                    Clock = new FakeClock(new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc)),
                    Serialisation = new FakeSerialisationService(),
                    Content = new FakeContentService(),
                };

                if (!noStore) harness.Content.Add(StoreJson.ThreeKinds());

                harness.Backend = new FakeBackend(harness.Serialisation);
                harness.Granter = new RecordingGranter();
                harness.Service = new StoreService(harness.Clock, harness.Content,
                    harness.Backend, ConfigFactory.Create());
                harness.Service.SetRewardGranter(harness.Granter);

                harness.Service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

                return harness;
            }
        }
    }
}
