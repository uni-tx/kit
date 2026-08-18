using System;
using NUnit.Framework;

namespace UniTx.Store.Tests
{
    /// <summary>
    /// The pure rules: cooldowns, claim limits and offer states.
    /// </summary>
    public sealed class StoreCalculatorTests
    {
        private const long Now = 1_700_000_000;

        [Test]
        public void EvaluateState_NoOffer_IsNone()
        {
            var state = StoreCalculator.EvaluateState(null, null, Now);

            Assert.AreEqual(StoreOfferState.None, state);
        }

        [Test]
        public void EvaluateState_FreshOffer_IsReady()
        {
            var store = StoreJson.ThreeKinds();
            var offer = store.GetOffer("free1");

            var state = StoreCalculator.EvaluateState(offer, null, Now);

            Assert.AreEqual(StoreOfferState.Ready, state);
        }

        [Test]
        public void EvaluateState_OnCooldown_ReportsOnCooldown()
        {
            var store = StoreJson.ThreeKinds();
            var offer = store.GetOffer("free1");
            var record = new StoreOfferRecord("free1");
            record.RecordClaim(Now - 10); // 10s ago; cooldown is 60s.

            var state = StoreCalculator.EvaluateState(offer, record, Now);

            Assert.AreEqual(StoreOfferState.OnCooldown, state);
        }

        [Test]
        public void EvaluateState_OffCooldown_IsReady()
        {
            var store = StoreJson.ThreeKinds();
            var offer = store.GetOffer("free1");
            var record = new StoreOfferRecord("free1");
            record.RecordClaim(Now - 61); // 61s ago; cooldown is 60s.

            var state = StoreCalculator.EvaluateState(offer, record, Now);

            Assert.AreEqual(StoreOfferState.Ready, state);
        }

        [Test]
        public void EvaluateState_LimitReached_ReportsLimitReached()
        {
            var store = StoreJson.ThreeKinds();
            var offer = store.GetOffer("free1");
            var record = new StoreOfferRecord("free1");

            for (var i = 0; i < 3; i++) record.RecordClaim(Now - (i + 1) * 120);

            var state = StoreCalculator.EvaluateState(offer, record, Now);

            Assert.AreEqual(StoreOfferState.LimitReached, state);
        }

        [Test]
        public void CanClaim_FreshOffer_True()
        {
            var store = StoreJson.ThreeKinds();

            Assert.IsTrue(StoreCalculator.CanClaim(store.GetOffer("free1"), null, Now));
        }

        [Test]
        public void CanClaim_OnCooldown_False()
        {
            var store = StoreJson.ThreeKinds();
            var record = new StoreOfferRecord("free1");
            record.RecordClaim(Now - 10);

            Assert.IsFalse(StoreCalculator.CanClaim(store.GetOffer("free1"), record, Now));
        }

        [Test]
        public void CanClaim_LimitReached_False()
        {
            var store = StoreJson.ThreeKinds();
            var record = new StoreOfferRecord("free1");

            for (var i = 0; i < 3; i++) record.RecordClaim(Now - (i + 1) * 120);

            Assert.IsFalse(StoreCalculator.CanClaim(store.GetOffer("free1"), record, Now));
        }

        [Test]
        public void RemainingCooldown_NoCooldown_Zero()
        {
            var store = StoreJson.ThreeKinds();
            var offer = store.GetOffer("rewarded1"); // no cooldown in the JSON.

            Assert.AreEqual(0, StoreCalculator.RemainingCooldownSeconds(offer, null, Now));
        }

        [Test]
        public void RemainingCooldown_WithinCooldown_CountsDown()
        {
            var store = StoreJson.ThreeKinds();
            var offer = store.GetOffer("free1");
            var record = new StoreOfferRecord("free1");
            record.RecordClaim(Now - 20); // 40s remain of the 60s cooldown.

            var remaining = StoreCalculator.RemainingCooldownSeconds(offer, record, Now);

            Assert.AreEqual(40, remaining);
        }

        [Test]
        public void RemainingCooldown_Expired_Zero()
        {
            var store = StoreJson.ThreeKinds();
            var offer = store.GetOffer("free1");
            var record = new StoreOfferRecord("free1");
            record.RecordClaim(Now - 120);

            Assert.AreEqual(0, StoreCalculator.RemainingCooldownSeconds(offer, record, Now));
        }

        [Test]
        public void IsLimitReached_NoLimit_False()
        {
            var store = StoreJson.ThreeKinds();
            var record = new StoreOfferRecord("rewarded1");
            record.RecordClaim(Now);

            Assert.IsFalse(StoreCalculator.IsLimitReached(store.GetOffer("rewarded1"), record));
        }

        [Test]
        public void IsLimitReached_AtLimit_True()
        {
            var store = StoreJson.ThreeKinds();
            var offer = store.GetOffer("free1");
            var record = new StoreOfferRecord("free1");

            for (var i = 0; i < 3; i++) record.RecordClaim(Now - (i + 1) * 120);

            Assert.IsTrue(StoreCalculator.IsLimitReached(offer, record));
        }
    }
}
