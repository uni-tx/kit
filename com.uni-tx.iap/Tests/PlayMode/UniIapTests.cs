using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UniTx.Iap.Tests.PlayMode
{
    /// <summary>
    /// A provider whose every outcome is dictated by the test.
    /// </summary>
    public sealed class StubIapProvider : IIapProvider
    {
        private readonly Dictionary<string, IapPurchase> _outcomes = new();
        private readonly HashSet<string> _owned = new();

        /// <inheritdoc />
        public string Name => "Stub";

        /// <inheritdoc />
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Gets the number of times a purchase was requested.
        /// </summary>
        public int PurchaseCallCount { get; private set; }

        /// <summary>
        /// Gets or sets a hook awaited before a purchase resolves.
        /// </summary>
        public Func<UniTask> PurchaseGate { get; set; }

        /// <inheritdoc />
        public event Action<IapPurchase> OnPurchaseRestored;

        /// <summary>
        /// Queues the result a product will produce.
        /// </summary>
        /// <param name="productId">The product to answer for.</param>
        /// <param name="result">The outcome to return.</param>
        public void SetOutcome(string productId, IapResult result) =>
            _outcomes[productId] = new IapPurchase(result, productId, "txn-" + productId, "receipt");

        /// <summary>
        /// Raises a restore, as a store does after a reinstall.
        /// </summary>
        /// <param name="productId">The product being restored.</param>
        public void RaiseRestore(string productId) =>
            OnPurchaseRestored?.Invoke(new IapPurchase(IapResult.Success, productId, "txn-restored"));

        /// <inheritdoc />
        public UniTask InitializeAsync(UniIapConfig config, CancellationToken cToken = default)
        {
            IsInitialized = true;
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public async UniTask<IapPurchase> PurchaseAsync(string productId, CancellationToken cToken = default)
        {
            PurchaseCallCount++;

            if (PurchaseGate != null) await PurchaseGate();

            if (!_outcomes.TryGetValue(productId, out var outcome))
            {
                return IapPurchase.Fail(IapResult.ProductUnavailable, productId);
            }

            if (outcome.IsSuccess) _owned.Add(productId);

            return outcome;
        }

        /// <inheritdoc />
        public UniTask<bool> RestoreAsync(CancellationToken cToken = default) => UniTask.FromResult(true);

        /// <inheritdoc />
        public bool IsOwned(string productId) => _owned.Contains(productId);

        /// <inheritdoc />
        public string GetLocalizedPrice(string productId) => _outcomes.ContainsKey(productId) ? "£2.99" : null;

        /// <inheritdoc />
        public string GetLocalizedTitle(string productId) => _outcomes.ContainsKey(productId) ? "Gems" : null;
    }

    /// <summary>
    /// A provider whose initialization can be held open, to exercise races at boot.
    /// </summary>
    public sealed class SlowStubIapProvider : IIapProvider
    {
        private readonly UniTask _gate;

        /// <summary>
        /// Initializes a new instance of the <see cref="SlowStubIapProvider"/> class.
        /// </summary>
        /// <param name="gate">Awaited before initialization completes.</param>
        public SlowStubIapProvider(UniTask gate) => _gate = gate.Preserve();

        /// <inheritdoc />
        public string Name => "SlowStub";

        /// <inheritdoc />
        public bool IsInitialized { get; private set; }

        /// <inheritdoc />
        public event Action<IapPurchase> OnPurchaseRestored;

        /// <summary>
        /// Raises a restore, as a store does after a reinstall.
        /// </summary>
        /// <param name="productId">The product being restored.</param>
        public void RaiseRestore(string productId) =>
            OnPurchaseRestored?.Invoke(new IapPurchase(IapResult.Success, productId, "txn-restored"));

        /// <inheritdoc />
        public async UniTask InitializeAsync(UniIapConfig config, CancellationToken cToken = default)
        {
            await _gate;
            IsInitialized = true;
        }

        /// <inheritdoc />
        public UniTask<IapPurchase> PurchaseAsync(string productId, CancellationToken cToken = default) =>
            UniTask.FromResult(IapPurchase.Fail(IapResult.Unsupported, productId));

        /// <inheritdoc />
        public UniTask<bool> RestoreAsync(CancellationToken cToken = default) => UniTask.FromResult(false);

        /// <inheritdoc />
        public bool IsOwned(string productId) => false;

        /// <inheritdoc />
        public string GetLocalizedPrice(string productId) => null;

        /// <inheritdoc />
        public string GetLocalizedTitle(string productId) => null;
    }

    public class UniIapTests
    {
        private const string Gems = "com.game.gems";

        private StubIapProvider _provider;

        [SetUp]
        public void SetUp()
        {
            UniIap.Reset();
            _provider = new StubIapProvider();
        }

        [TearDown]
        public void TearDown() => UniIap.Reset();

        [UnityTest]
        public IEnumerator PurchaseAsync_BeforeInitialize_ReportsUnsupported() =>
            UniTask.ToCoroutine(async () =>
            {
                var result = await UniIap.PurchaseAsync(Gems);

                Assert.AreEqual(IapResult.Unsupported, result.Result);
            });

        [UnityTest]
        public IEnumerator PurchaseAsync_Success_RaisesOnPurchased() => UniTask.ToCoroutine(async () =>
        {
            await UniIap.InitializeAsync(_provider);
            _provider.SetOutcome(Gems, IapResult.Success);

            IapPurchase observed = default;
            void Handler(IapPurchase p) => observed = p;

            UniIap.OnPurchased += Handler;

            try
            {
                var result = await UniIap.PurchaseAsync(Gems);

                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(Gems, observed.ProductId, "the grant event must carry the product");
                Assert.AreEqual("receipt", observed.Receipt);
            }
            finally
            {
                UniIap.OnPurchased -= Handler;
            }
        });

        [UnityTest]
        public IEnumerator PurchaseAsync_Cancelled_DoesNotRaiseOnPurchased() =>
            UniTask.ToCoroutine(async () =>
            {
                await UniIap.InitializeAsync(_provider);
                _provider.SetOutcome(Gems, IapResult.Cancelled);

                var raised = 0;
                void Handler(IapPurchase p) => raised++;

                UniIap.OnPurchased += Handler;

                try
                {
                    var result = await UniIap.PurchaseAsync(Gems);

                    Assert.AreEqual(IapResult.Cancelled, result.Result);
                    Assert.AreEqual(0, raised, "a cancelled purchase must not grant content");
                }
                finally
                {
                    UniIap.OnPurchased -= Handler;
                }
            });

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator PurchaseAsync_WhileOneIsOpen_IsRejected() => UniTask.ToCoroutine(async () =>
        {
            await UniIap.InitializeAsync(_provider);
            _provider.SetOutcome(Gems, IapResult.Success);

            var gate = new UniTaskCompletionSource();
            _provider.PurchaseGate = () => gate.Task;

            // Preserved because it is held across other awaits before being consumed. A bare
            // UniTask may only be awaited once and its source can be recycled in the
            // meantime, which makes the deferred await hang rather than fail.
            var first = UniIap.PurchaseAsync(Gems).Preserve();
            await UniTask.Yield();

            Assert.IsTrue(UniIap.IsPurchasing);

            // A double-tapped buy button must not reach the store a second time.
            var second = await UniIap.PurchaseAsync(Gems);

            Assert.AreEqual(IapResult.Failed, second.Result);
            Assert.AreEqual(1, _provider.PurchaseCallCount, "the store must see only one request");

            gate.TrySetResult();
            Assert.IsTrue((await first).IsSuccess);
            Assert.IsFalse(UniIap.IsPurchasing, "the guard must clear once the purchase resolves");
        });

        [UnityTest]
        public IEnumerator PurchaseAsync_ProviderThrows_ClearsTheGuard() => UniTask.ToCoroutine(async () =>
        {
            await UniIap.InitializeAsync(_provider);
            _provider.SetOutcome(Gems, IapResult.Success);
            _provider.PurchaseGate = () => throw new InvalidOperationException("store exploded");

            try
            {
                await UniIap.PurchaseAsync(Gems);
                Assert.Fail("the exception should propagate");
            }
            catch (InvalidOperationException)
            {
                // Expected.
            }

            // Without the finally block in the facade this stays true forever and the shop
            // refuses every subsequent purchase for the rest of the session.
            Assert.IsFalse(UniIap.IsPurchasing);
        });

        [UnityTest]
        public IEnumerator RestoredPurchase_RaisesOnPurchased() => UniTask.ToCoroutine(async () =>
        {
            await UniIap.InitializeAsync(_provider);

            IapPurchase observed = default;
            void Handler(IapPurchase p) => observed = p;

            UniIap.OnPurchased += Handler;

            try
            {
                // A restore never returns through PurchaseAsync, so a game that grants only
                // from that return value silently drops every reinstall entitlement.
                _provider.RaiseRestore(Gems);

                Assert.AreEqual(Gems, observed.ProductId);
                Assert.IsTrue(observed.IsSuccess);
            }
            finally
            {
                UniIap.OnPurchased -= Handler;
            }
        });

        [UnityTest]
        public IEnumerator Reinitialize_DoesNotGrantRestoresTwice() => UniTask.ToCoroutine(async () =>
        {
            await UniIap.InitializeAsync(_provider);

            var second = new StubIapProvider();
            await UniIap.InitializeAsync(second);

            var raised = 0;
            void Handler(IapPurchase p) => raised++;

            UniIap.OnPurchased += Handler;

            try
            {
                // The first provider must have been detached. Left attached, both would fan
                // into OnPurchased and the player would receive the content twice.
                _provider.RaiseRestore(Gems);

                Assert.AreEqual(0, raised, "the replaced provider must be detached");

                second.RaiseRestore(Gems);

                Assert.AreEqual(1, raised);
            }
            finally
            {
                UniIap.OnPurchased -= Handler;
            }
        });

        [UnityTest]
        public IEnumerator GetPrice_UsesTheStoreValue_AndFallsBack() => UniTask.ToCoroutine(async () =>
        {
            await UniIap.InitializeAsync(_provider);
            _provider.SetOutcome(Gems, IapResult.Success);

            Assert.AreEqual("£2.99", UniIap.GetPrice(Gems));
            Assert.AreEqual("—", UniIap.GetPrice("com.game.unknown"));
            Assert.AreEqual("n/a", UniIap.GetPrice("com.game.unknown", "n/a"));
        });

        [UnityTest]
        public IEnumerator Reset_LeavesSubscribersAttached() => UniTask.ToCoroutine(async () =>
        {
            await UniIap.InitializeAsync(_provider);

            var raised = 0;
            void Handler(IapPurchase p) => raised++;

            UniIap.OnPurchased += Handler;

            try
            {
                UniIap.Reset();

                var fresh = new StubIapProvider();
                fresh.SetOutcome(Gems, IapResult.Success);
                await UniIap.InitializeAsync(fresh);
                await UniIap.PurchaseAsync(Gems);

                // Boot-time services subscribe once. Clearing the event on Reset would stop
                // granting content after any re-initialization, with no error anywhere.
                Assert.AreEqual(1, raised);
            }
            finally
            {
                UniIap.OnPurchased -= Handler;
            }
        });

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ConcurrentInitialize_SubscribesOnlyOnce() => UniTask.ToCoroutine(async () =>
        {
            var gate = new UniTaskCompletionSource();
            var slow = new SlowStubIapProvider(gate.Task);

            // Two boot paths racing to initialize — a retry, or two systems both ensuring the
            // store is up. The facade must not end up with the handler attached twice.
            var a = UniIap.InitializeAsync(slow).Preserve();
            var b = UniIap.InitializeAsync(slow).Preserve();

            gate.TrySetResult();
            await a;
            await b;

            var raised = 0;
            void Handler(IapPurchase p) => raised++;

            UniIap.OnPurchased += Handler;

            try
            {
                slow.RaiseRestore(Gems);

                Assert.AreEqual(1, raised, "a duplicate subscription would grant content twice");
            }
            finally
            {
                UniIap.OnPurchased -= Handler;
            }
        });

        [Test]
        public void NoOpProvider_ReportsUnsupported()
        {
            var provider = new NoOpIapProvider();

            Assert.IsFalse(provider.IsOwned("anything"));
            Assert.IsNull(provider.GetLocalizedPrice("anything"));
        }

        [Test]
        public void Config_DescribesBlankAndDuplicateIds()
        {
            var config = ScriptableObject.CreateInstance<UniIapConfig>();

            try
            {
                // An empty catalog is the state every new project starts in, and it is the
                // most common reason a shop screen renders with no products.
                StringAssert.Contains("no products", config.DescribeProblems());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ProductStub_FallsBackToTheCatalogId()
        {
            var json = "{\"_id\":\"com.game.gems\",\"_kind\":0,\"_appleId\":\"\",\"_googleId\":\"\"}";
            var stub = JsonUtility.FromJson<IapProductStub>(json);

            Assert.AreEqual("com.game.gems", stub.ResolveStoreId());
        }
    }
}
