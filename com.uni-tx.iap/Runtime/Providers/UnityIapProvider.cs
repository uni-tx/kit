#if UNITX_UNITY_IAP
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine.Purchasing;

namespace UniTx.Iap.Providers
{
    /// <summary>
    /// Billing adapter backed by Unity IAP.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Targets the Unity IAP 5.x service API — <c>UnityIAPServices.StoreController()</c> —
    /// not the 4.x <c>IStoreController</c>/<c>IStoreListener</c> pair, which still ships under
    /// <c>Legacy/</c> and is what most published samples show.
    /// </para>
    /// <para>
    /// Compiled only when <c>com.unity.purchasing</c> 5.0.0 or newer is installed, via the
    /// <c>UNITX_UNITY_IAP</c> constraint on this assembly. Without the package the kit falls
    /// back to <see cref="NoOpIapProvider"/> and the game still builds.
    /// </para>
    /// </remarks>
    public sealed class UnityIapProvider : IIapProvider
    {
        private readonly Dictionary<string, Product> _products = new();
        private readonly HashSet<string> _owned = new();

        private StoreController _controller;
        private UniIapConfig _config;
        private UniTaskCompletionSource<IapPurchase> _purchaseSource;
        private UniTaskCompletionSource<bool> _fetchSource;
        private string _purchasingProductId;
        private bool _isVerbose;
        private bool _isInitializing;

        /// <inheritdoc />
        public string Name => "UnityIAP";

        /// <inheritdoc />
        public bool IsInitialized { get; private set; }

        /// <inheritdoc />
        public event Action<IapPurchase> OnPurchaseRestored;

        /// <inheritdoc />
        /// <remarks>
        /// Re-entrant calls are ignored. <see cref="IsInitialized"/> only becomes true once
        /// the catalog has been fetched, so a second call arriving while the first is still
        /// awaiting the store would otherwise subscribe a duplicate set of handlers — and
        /// every purchase after that would grant its content twice.
        /// </remarks>
        public async UniTask InitializeAsync(UniIapConfig config, CancellationToken cToken = default)
        {
            if (IsInitialized || _isInitializing) return;

            _isInitializing = true;

            try
            {
                await ConnectAndFetchAsync(config, cToken);
            }
            catch
            {
                // Handlers are attached before the store is reached, so a failed or cancelled
                // connect must detach them. Otherwise the natural next move — retrying —
                // subscribes a second set and every purchase grants its content twice.
                Dispose();
                throw;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private async UniTask ConnectAndFetchAsync(UniIapConfig config, CancellationToken cToken)
        {
            _config = config;
            _isVerbose = config == null || config.VerboseLogging;

            _controller = UnityIAPServices.StoreController();

            _controller.OnPurchasePending += HandlePurchasePending;
            _controller.OnPurchaseFailed += HandlePurchaseFailed;
            _controller.OnPurchaseDeferred += HandlePurchaseDeferred;
            _controller.OnProductsFetched += HandleProductsFetched;
            _controller.OnProductsFetchFailed += HandleProductsFetchFailed;
            _controller.OnPurchasesFetched += HandlePurchasesFetched;

            await _controller.Connect().AsUniTask().AttachExternalCancellation(cToken);

            if (config == null || config.Products.Count == 0)
            {
                UniStatics.LogWarning("UniIap: no products declared, so nothing was fetched.", null);
                IsInitialized = true;
                return;
            }

            var definitions = new List<ProductDefinition>(config.Products.Count);

            foreach (var stub in config.Products)
            {
                if (stub == null || string.IsNullOrWhiteSpace(stub.Id)) continue;

                definitions.Add(new ProductDefinition(stub.Id, stub.ResolveStoreId(), ToStoreType(stub.Kind)));
            }

            _fetchSource = new UniTaskCompletionSource<bool>();
            _controller.FetchProducts(definitions);

            await _fetchSource.Task.AttachExternalCancellation(cToken);
            _fetchSource = null;

            IsInitialized = true;

            // Owned non-consumables have to be re-read on every launch: the store, not the
            // device, is the record of what the player has bought.
            if (config.RestoreOnInitialize) _controller.FetchPurchases();
        }

        /// <inheritdoc />
        public async UniTask<IapPurchase> PurchaseAsync(string productId, CancellationToken cToken = default)
        {
            if (!IsInitialized) return IapPurchase.Fail(IapResult.NotInitialized, productId);

            if (!_products.TryGetValue(productId, out var product))
            {
                return IapPurchase.Fail(IapResult.ProductUnavailable, productId, "not in the fetched catalog");
            }

            if (!product.availableToPurchase)
            {
                return IapPurchase.Fail(IapResult.ProductUnavailable, productId, "the store will not sell it");
            }

            _purchasingProductId = productId;
            _purchaseSource = new UniTaskCompletionSource<IapPurchase>();

            if (_isVerbose) UniStatics.LogInfo($"UniIap: purchasing '{productId}'.", null);

            _controller.PurchaseProduct(product);

            try
            {
                // Cancellation abandons the await, never the order. If the player completes
                // the sheet anyway the entitlement still arrives through OnPurchaseRestored,
                // because the pending-order handler confirms it regardless of who is waiting.
                return await _purchaseSource.Task.AttachExternalCancellation(cToken);
            }
            finally
            {
                _purchaseSource = null;
                _purchasingProductId = null;
            }
        }

        /// <inheritdoc />
        public async UniTask<bool> RestoreAsync(CancellationToken cToken = default)
        {
            if (!IsInitialized) return false;

            var source = new UniTaskCompletionSource<bool>();

            // RestoreTransactions is the Apple-facing call and is what the review guidelines
            // require a button for. On Google Play the equivalent sweep is FetchPurchases,
            // which the store also performs on connect.
            _controller.RestoreTransactions((success, error) =>
            {
                if (!success) UniStatics.LogWarning($"UniIap: restore failed — {error}.", null);

                source.TrySetResult(success);
            });

            return await source.Task.AttachExternalCancellation(cToken);
        }

        /// <inheritdoc />
        public bool IsOwned(string productId) => _owned.Contains(productId);

        /// <inheritdoc />
        public string GetLocalizedPrice(string productId) =>
            _products.TryGetValue(productId, out var p) ? p.metadata?.localizedPriceString : null;

        /// <inheritdoc />
        public string GetLocalizedTitle(string productId) =>
            _products.TryGetValue(productId, out var p) ? p.metadata?.localizedTitle : null;

        /// <summary>
        /// Detaches every store callback and clears cached catalog state.
        /// </summary>
        /// <remarks>
        /// Leaving handlers attached across a re-initialization is what makes a restored
        /// purchase grant its content twice.
        /// </remarks>
        public void Dispose()
        {
            if (_controller != null)
            {
                _controller.OnPurchasePending -= HandlePurchasePending;
                _controller.OnPurchaseFailed -= HandlePurchaseFailed;
                _controller.OnPurchaseDeferred -= HandlePurchaseDeferred;
                _controller.OnProductsFetched -= HandleProductsFetched;
                _controller.OnProductsFetchFailed -= HandleProductsFetchFailed;
                _controller.OnPurchasesFetched -= HandlePurchasesFetched;
            }

            _products.Clear();
            _owned.Clear();
            _controller = null;
            IsInitialized = false;

            // A caller still awaiting a fetch or a purchase would otherwise hang forever on a
            // source nothing can complete now that the handlers are gone.
            _fetchSource?.TrySetResult(false);
            _fetchSource = null;

            _purchaseSource?.TrySetResult(
                IapPurchase.Fail(IapResult.Unsupported, _purchasingProductId, "the provider was disposed"));
            _purchaseSource = null;
            _purchasingProductId = null;
        }

        private void HandleProductsFetched(List<Product> products)
        {
            _products.Clear();

            foreach (var product in products)
            {
                var id = product.definition?.id;
                if (!string.IsNullOrEmpty(id)) _products[id] = product;
            }

            if (_isVerbose) UniStatics.LogInfo($"UniIap: fetched {_products.Count} product(s).", null);

            _fetchSource?.TrySetResult(true);
        }

        private void HandleProductsFetchFailed(ProductFetchFailed failure)
        {
            UniStatics.LogWarning($"UniIap: product fetch failed — {failure?.FailureReason}.", null);

            // Resolved rather than faulted: a store that returns nothing is a normal state
            // during review or on a device with no network, and the shop should render with
            // unavailable products instead of the game failing to boot.
            _fetchSource?.TrySetResult(false);
        }

        private void HandlePurchasePending(PendingOrder order)
        {
            var productId = ResolveProductId(order);

            // Confirming is not optional. An unconfirmed order is re-delivered on every
            // launch, and on Google Play a consumable is never consumed, so the player cannot
            // buy it a second time.
            _controller.ConfirmPurchase(order);

            var purchase = new IapPurchase(
                IapResult.Success,
                productId,
                order.Info?.TransactionID,
                order.Info?.Receipt);

            if (!IsConsumable(productId)) _owned.Add(productId);

            if (_isVerbose) UniStatics.LogInfo($"UniIap: confirmed '{productId}'.", null);

            // An order for something other than the in-flight purchase is a restore, a
            // renewal, or a deferred order that finally cleared.
            if (_purchaseSource != null && productId == _purchasingProductId)
            {
                _purchaseSource.TrySetResult(purchase);
                return;
            }

            OnPurchaseRestored.SafeInvoke(purchase);
        }

        private void HandlePurchaseFailed(FailedOrder order)
        {
            var productId = ResolveProductId(order);
            var result = ToResult(order.FailureReason);

            if (_isVerbose)
            {
                UniStatics.LogWarning($"UniIap: '{productId}' failed — {order.FailureReason}.", null);
            }

            if (_purchaseSource != null && productId == _purchasingProductId)
            {
                _purchaseSource.TrySetResult(IapPurchase.Fail(result, productId, order.Details));
            }
        }

        private void HandlePurchaseDeferred(DeferredOrder order)
        {
            var productId = ResolveProductId(order);

            if (_purchaseSource != null && productId == _purchasingProductId)
            {
                _purchaseSource.TrySetResult(IapPurchase.Fail(
                    IapResult.Deferred, productId, "awaiting external approval"));
            }
        }

        private void HandlePurchasesFetched(Orders orders)
        {
            // Purchases already confirmed in a previous session arrive here rather than
            // through OnPurchasePending, so ownership has to be rebuilt from them too.
            if (orders?.ConfirmedOrders == null) return;

            foreach (var order in orders.ConfirmedOrders)
            {
                var productId = ResolveProductId(order);
                if (string.IsNullOrEmpty(productId) || IsConsumable(productId)) continue;

                if (_owned.Add(productId))
                {
                    OnPurchaseRestored.SafeInvoke(new IapPurchase(
                        IapResult.Success, productId, order.Info?.TransactionID, order.Info?.Receipt));
                }
            }
        }

        private static string ResolveProductId(Order order)
        {
            var items = order?.CartOrdered?.Items();
            if (items == null || items.Count == 0) return null;

            return items[0].Product?.definition?.id;
        }

        private bool IsConsumable(string productId)
        {
            var stub = _config?.Find(productId);
            return stub == null || stub.Kind == IapProductKind.Consumable;
        }

        private static ProductType ToStoreType(IapProductKind kind) => kind switch
        {
            IapProductKind.NonConsumable => ProductType.NonConsumable,
            IapProductKind.Subscription => ProductType.Subscription,
            _ => ProductType.Consumable,
        };

        private static IapResult ToResult(PurchaseFailureReason reason) => reason switch
        {
            PurchaseFailureReason.UserCancelled => IapResult.Cancelled,
            PurchaseFailureReason.DuplicateTransaction => IapResult.AlreadyOwned,
            PurchaseFailureReason.ExistingPurchasePending => IapResult.AlreadyOwned,
            PurchaseFailureReason.ProductUnavailable => IapResult.ProductUnavailable,
            PurchaseFailureReason.PurchasingUnavailable => IapResult.Unsupported,
            PurchaseFailureReason.StoreNotConnected => IapResult.NetworkUnavailable,
            PurchaseFailureReason.PaymentDeclined => IapResult.PaymentDeclined,
            _ => IapResult.Failed,
        };
    }
}
#endif
