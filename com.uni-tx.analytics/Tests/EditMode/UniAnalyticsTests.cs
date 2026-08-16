using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UniTx.Analytics.Tests.EditMode
{
    internal sealed class RecordingProvider : IAnalyticsProvider
    {
        public readonly List<string> Events = new();
        public readonly List<string> Properties = new();
        public readonly List<string> Revenue = new();

        public string Name => "Recording";
        public bool IsReady { get; set; } = true;
        public bool ConsentGiven { get; private set; } = true;
        public int FlushCount { get; private set; }
        public bool ThrowOnTrack { get; set; }

        public UniTask InitializeAsync(CancellationToken cToken = default) => UniTask.CompletedTask;

        public void TrackEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            if (ThrowOnTrack) throw new InvalidOperationException("provider exploded");

            Events.Add(parameters == null ? eventName : $"{eventName}:{parameters.Count}");
        }

        public void SetUserProperty(string key, object value) => Properties.Add($"{key}={value}");

        public void TrackRevenue(string productId, string currency, decimal amount)
            => Revenue.Add($"{productId}:{amount}{currency}");

        public void SetConsent(bool hasConsent) => ConsentGiven = hasConsent;

        public void Flush() => FlushCount++;
    }

    internal sealed class FailingProvider : IAnalyticsProvider
    {
        public string Name => "Failing";
        public bool IsReady => true;

        public UniTask InitializeAsync(CancellationToken cToken = default)
            => UniTask.FromException(new InvalidOperationException("boom"));

        public void TrackEvent(string eventName, IReadOnlyDictionary<string, object> parameters) { }
        public void SetUserProperty(string key, object value) { }
        public void TrackRevenue(string productId, string currency, decimal amount) { }
        public void SetConsent(bool hasConsent) { }
        public void Flush() { }
    }

    public class UniAnalyticsTests
    {
        private RecordingProvider _provider;

        [SetUp]
        public void SetUp()
        {
            UniAnalytics.Reset();
            _provider = new RecordingProvider();
            UniAnalytics.Register(_provider);
        }

        [TearDown]
        public void TearDown() => UniAnalytics.Reset();

        [Test]
        public void Track_ForwardsToRegisteredProviders()
        {
            UniAnalytics.Track("level_start");

            CollectionAssert.AreEqual(new[] { "level_start" }, _provider.Events);
        }

        [Test]
        public void Track_FansOutToEveryProvider()
        {
            var second = new RecordingProvider();
            UniAnalytics.Register(second);

            UniAnalytics.Track("level_start");

            Assert.AreEqual(1, _provider.Events.Count);
            Assert.AreEqual(1, second.Events.Count);
        }

        [Test]
        public void Register_SameProviderTwice_IsIgnored()
        {
            UniAnalytics.Register(_provider);

            Assert.AreEqual(1, UniAnalytics.RegisteredProviders.Count);
        }

        [Test]
        public void Register_Null_Throws()
            => Assert.Throws<ArgumentNullException>(() => UniAnalytics.Register(null));

        [Test]
        public void Track_EmptyName_IsIgnored()
        {
            UniAnalytics.Track("   ");

            CollectionAssert.IsEmpty(_provider.Events);
        }

        [Test]
        public void Track_SkipsProvidersThatAreNotReady()
        {
            _provider.IsReady = false;

            UniAnalytics.Track("level_start");

            CollectionAssert.IsEmpty(_provider.Events);
        }

        [Test]
        public void Track_WithoutConsent_SendsNothing()
        {
            UniAnalytics.SetConsent(false);

            UniAnalytics.Track("level_start");
            UniAnalytics.SetUserProperty("tier", 1);
            UniAnalytics.TrackRevenue("pack", "USD", 0.99m);

            // Gated centrally so a provider that forgets to honour consent still cannot leak.
            CollectionAssert.IsEmpty(_provider.Events);
            CollectionAssert.IsEmpty(_provider.Properties);
            CollectionAssert.IsEmpty(_provider.Revenue);
        }

        [Test]
        public void SetConsent_PropagatesToProviders()
        {
            UniAnalytics.SetConsent(false);
            Assert.IsFalse(_provider.ConsentGiven);

            UniAnalytics.SetConsent(true);
            Assert.IsTrue(_provider.ConsentGiven);
        }

        [Test]
        public void Register_AppliesCurrentConsentImmediately()
        {
            UniAnalytics.SetConsent(false);

            var late = new RecordingProvider();
            UniAnalytics.Register(late);

            // A provider registered after consent was withdrawn must not start out permitted.
            Assert.IsFalse(late.ConsentGiven);
        }

        [Test]
        public void Track_ProviderThrows_DoesNotPropagate()
        {
            _provider.ThrowOnTrack = true;

            // The facade swallows the provider's exception and logs it, so the test has to
            // declare the expected error or the runner treats it as an unhandled failure.
            LogAssert.Expect(LogType.Error, new Regex("Recording.*provider exploded"));

            // Analytics must never take gameplay down.
            Assert.DoesNotThrow(() => UniAnalytics.Track("level_start"));
        }

        [Test]
        public void InitializeAsync_OneProviderFails_OthersStillInitialize()
        {
            UniAnalytics.Reset();
            UniAnalytics.Register(new FailingProvider());
            var healthy = new RecordingProvider();
            UniAnalytics.Register(healthy);

            // Both providers complete synchronously here, so the edit-mode test can drain the
            // UniTask directly rather than needing a PlayMode UnityTest.
            UniAnalytics.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

            LogAssert.Expect(LogType.Error, new Regex("Failing.*boom"));

            UniAnalytics.Track("after_init");
            Assert.AreEqual(1, healthy.Events.Count);
        }

        [Test]
        public void Track_SingleParameterOverload_PassesOneParameter()
        {
            UniAnalytics.Track("level_end", "score", 100);

            CollectionAssert.AreEqual(new[] { "level_end:1" }, _provider.Events);
        }

        [Test]
        public void TrackRevenue_ForwardsAmountAndCurrency()
        {
            UniAnalytics.TrackRevenue("starter_pack", "USD", 4.99m);

            CollectionAssert.AreEqual(new[] { "starter_pack:4.99USD" }, _provider.Revenue);
        }

        [Test]
        public void Flush_ForwardsToProviders()
        {
            UniAnalytics.Flush();

            Assert.AreEqual(1, _provider.FlushCount);
        }

        [Test]
        public void Unregister_StopsDelivery()
        {
            Assert.IsTrue(UniAnalytics.Unregister(_provider));

            UniAnalytics.Track("level_start");

            CollectionAssert.IsEmpty(_provider.Events);
        }

        [Test]
        public void Track_WithNoProviders_DoesNotThrow()
        {
            UniAnalytics.Reset();

            Assert.DoesNotThrow(() => UniAnalytics.Track("orphan"));
        }
    }
}
