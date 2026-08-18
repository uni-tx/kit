using System;
using System.Threading;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace UniTx.Economy.Tests
{
    /// <summary>
    /// The economy service: exchange, purchase and the N-economy split.
    /// </summary>
    public sealed class EconomyServiceTests
    {
        [SetUp]
        public void SetUp()
        {
        }

        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void Initialize_LoadsBothEconomies_IsReady()
        {
            var harness = Harness.Create();

            Assert.IsTrue(harness.Service.IsReady);
            Assert.AreEqual(2, harness.Service.GetEconomyIds().Count);
        }

        [Test]
        public void SelectEconomy_Unknown_ReturnsFalse()
        {
            var harness = Harness.Create();

            Assert.IsFalse(harness.Service.SelectEconomy("nope"));
        }

        [Test]
        public void SelectEconomy_Known_Selects()
        {
            var harness = Harness.Create();

            Assert.IsTrue(harness.Service.SelectEconomy(EconomyJson.CoreId));
            Assert.AreEqual(EconomyJson.CoreId, harness.Service.SelectedEconomyId);
        }

        [Test]
        public void GetSnapshot_ShowsCurrenciesAndRules()
        {
            var harness = Harness.Create();
            harness.Currencies.SetBalance("coins", 100);
            harness.Currencies.SetBalance("gems", 7);

            var snapshot = harness.Service.GetSnapshot(EconomyJson.CoreId);

            Assert.AreEqual(EconomyJson.CoreId, snapshot.EconomyId);
            Assert.AreEqual(2, snapshot.Currencies.Length);
            Assert.AreEqual(1, snapshot.ExchangeRules.Length);
            Assert.AreEqual(1, snapshot.Purchases.Length);
            Assert.AreEqual(100, snapshot.Currencies[0].Balance);
        }

        [Test]
        public void Exchange_SpendsSourceAndGrantsTarget()
        {
            var harness = Harness.Create();
            harness.Currencies.SetBalance("coins", 100);
            harness.Currencies.SetBalance("gems", 0);

            var result = harness.Service.ExchangeAsync(EconomyJson.CoreId, "coins_to_gems",
                7, "x1").GetAwaiter().GetResult();

            Assert.AreEqual(ExchangeResult.Exchanged, result);
            Assert.AreEqual(93, harness.Currencies.GetBalance("coins"));
            Assert.AreEqual(70, harness.Currencies.GetBalance("gems"));
        }

        [Test]
        public void Exchange_ReplaySameId_IsDuplicate()
        {
            var harness = Harness.Create();
            harness.Currencies.SetBalance("coins", 100);
            harness.Currencies.SetBalance("gems", 0);

            harness.Service.ExchangeAsync(EconomyJson.CoreId, "coins_to_gems", 7, "x1")
                .GetAwaiter().GetResult();

            var replay = harness.Service.ExchangeAsync(EconomyJson.CoreId, "coins_to_gems",
                7, "x1").GetAwaiter().GetResult();

            Assert.AreEqual(ExchangeResult.Duplicate, replay);
            Assert.AreEqual(93, harness.Currencies.GetBalance("coins"));
            Assert.AreEqual(70, harness.Currencies.GetBalance("gems"));
        }

        [Test]
        public void Exchange_TooLittleSource_IsInsufficient()
        {
            var harness = Harness.Create();
            harness.Currencies.SetBalance("coins", 4);
            harness.Currencies.SetBalance("gems", 0);

            var result = harness.Service.ExchangeAsync(EconomyJson.CoreId, "coins_to_gems",
                7, "x1").GetAwaiter().GetResult();

            Assert.AreEqual(ExchangeResult.InsufficientBalance, result);
        }

        [Test]
        public void Exchange_BelowMinimum_IsOutOfRange()
        {
            var harness = Harness.Create();
            harness.Currencies.SetBalance("coins", 100);
            harness.Currencies.SetBalance("gems", 0);

            var result = harness.Service.ExchangeAsync(EconomyJson.CoreId, "coins_to_gems",
                3, "x1").GetAwaiter().GetResult();

            Assert.AreEqual(ExchangeResult.AmountOutOfRange, result);
        }

        [Test]
        public void Exchange_UnknownRule_IsNoRule()
        {
            var harness = Harness.Create();

            var result = harness.Service.ExchangeAsync(EconomyJson.CoreId, "nope", 7, "x1")
                .GetAwaiter().GetResult();

            Assert.AreEqual(ExchangeResult.NoRule, result);
        }

        [Test]
        public void Purchase_ChargesCostsAndGrantsRewards()
        {
            var harness = Harness.Create();
            harness.Currencies.SetBalance("gems", 5);

            var result = harness.Service.PurchaseAsync(EconomyJson.CoreId, "power_up", "k1")
                .GetAwaiter().GetResult();

            Assert.AreEqual(PurchaseResult.Purchased, result);
            Assert.AreEqual(2, harness.Currencies.GetBalance("gems"));
            Assert.AreEqual(1, harness.Rewards.GrantedIds.Count);
            Assert.IsTrue(harness.Rewards.GrantedIds[0].StartsWith("purchase:core:k1:"));
        }

        [Test]
        public void Purchase_ReplaySameKey_IsDuplicate()
        {
            var harness = Harness.Create();
            harness.Currencies.SetBalance("gems", 5);

            harness.Service.PurchaseAsync(EconomyJson.CoreId, "power_up", "k1")
                .GetAwaiter().GetResult();

            var replay = harness.Service.PurchaseAsync(EconomyJson.CoreId, "power_up", "k1")
                .GetAwaiter().GetResult();

            Assert.AreEqual(PurchaseResult.Duplicate, replay);
            Assert.AreEqual(2, harness.Currencies.GetBalance("gems"));
            Assert.AreEqual(1, harness.Rewards.GrantedIds.Count);
        }

        [Test]
        public void Purchase_CannotAfford_IsInsufficient()
        {
            var harness = Harness.Create();
            harness.Currencies.SetBalance("gems", 2);

            var result = harness.Service.PurchaseAsync(EconomyJson.CoreId, "power_up", "k1")
                .GetAwaiter().GetResult();

            Assert.AreEqual(PurchaseResult.InsufficientBalance, result);
            Assert.AreEqual(2, harness.Currencies.GetBalance("gems"));
            Assert.AreEqual(0, harness.Rewards.GrantedIds.Count);
        }

        [Test]
        public void Purchase_RewardFails_PendingAndRetriedOnRefresh()
        {
            var harness = Harness.Create();
            harness.Currencies.SetBalance("gems", 5);
            harness.Rewards.ShouldFail = true;

            var result = harness.Service.PurchaseAsync(EconomyJson.CoreId, "power_up", "k1")
                .GetAwaiter().GetResult();

            Assert.AreEqual(PurchaseResult.RewardFailed, result);
            Assert.AreEqual(2, harness.Currencies.GetBalance("gems"));

            // The costs stay charged and the retry only re-grants the owed rewards.
            harness.Rewards.ShouldFail = false;
            harness.Service.RefreshAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, harness.Rewards.GrantedIds.Count);
            Assert.AreEqual(2, harness.Currencies.GetBalance("gems"));
        }

        [Test]
        public void Purchase_UnknownPurchase_IsInvalid()
        {
            var harness = Harness.Create();

            var result = harness.Service.PurchaseAsync(EconomyJson.CoreId, "nope", "k1")
                .GetAwaiter().GetResult();

            Assert.AreEqual(PurchaseResult.Invalid, result);
        }

        [Test]
        public void MetaEconomy_IsIndependent()
        {
            var harness = Harness.Create();
            harness.Currencies.SetBalance("tokens", 5);

            var snapshot = harness.Service.GetSnapshot(EconomyJson.MetaId);

            Assert.AreEqual(EconomyJson.MetaId, snapshot.EconomyId);
            Assert.AreEqual(1, snapshot.Currencies.Length);
            Assert.AreEqual(5, snapshot.Currencies[0].Balance);

            // The meta economy's purchase works off its own currency, not the core's.
            var result = harness.Service.PurchaseAsync(EconomyJson.MetaId, "skin", "m1")
                .GetAwaiter().GetResult();

            Assert.AreEqual(PurchaseResult.Purchased, result);
            Assert.AreEqual(0, harness.Currencies.GetBalance("tokens"));
        }

        private sealed class Harness
        {
            public FakeClock Clock;
            public FakeSerialisationService Serialisation;
            public FakeContentService Content;
            public FakeBackend Backend;
            public FakeCurrencyService Currencies;
            public RecordingRewardService Rewards;
            public EconomyService Service;

            public static Harness Create()
            {
                var harness = new Harness
                {
                    Clock = new FakeClock(new DateTime(2026, 8, 18, 12, 0, 0,
                        DateTimeKind.Utc)),
                    Serialisation = new FakeSerialisationService(),
                    Content = new FakeContentService(),
                    Currencies = new FakeCurrencyService(),
                    Rewards = new RecordingRewardService(),
                };

                harness.Content.Add(EconomyJson.Core());
                harness.Content.Add(EconomyJson.Meta());

                harness.Backend = new FakeBackend(harness.Serialisation);

                harness.Service = new EconomyService(harness.Clock, harness.Content,
                    harness.Backend, harness.Currencies, harness.Rewards);

                harness.Service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

                return harness;
            }
        }
    }
}
