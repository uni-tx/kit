using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Iap
{
    /// <summary>
    /// Static facade over the game's billing provider.
    /// </summary>
    /// <remarks>
    /// Overlap protection, catalog validation and entitlement fan-out live here rather than in
    /// each adapter, so every provider behaves the same way and a new adapter cannot forget
    /// one of them.
    /// </remarks>
    public static class UniIap
    {
        private static IIapProvider _provider;
        private static UniIapConfig _config;
        private static bool _isPurchasing;

        /// <summary>
        /// Gets the active provider, or null when none is installed.
        /// </summary>
        public static IIapProvider Provider => _provider;

        /// <summary>
        /// Gets the active configuration, or null before initialization.
        /// </summary>
        public static UniIapConfig Config => _config;

        /// <summary>
        /// Gets a value indicating whether the store is connected and the catalog is known.
        /// </summary>
        public static bool IsInitialized => _provider != null && _provider.IsInitialized;

        /// <summary>
        /// Gets a value indicating whether a purchase dialog is open right now.
        /// </summary>
        public static bool IsPurchasing => _isPurchasing;

        /// <summary>
        /// Raised whenever an entitlement is granted, from a purchase or a restore.
        /// </summary>
        /// <remarks>
        /// The single place to grant content. Subscribing here rather than acting on the
        /// result of <see cref="PurchaseAsync"/> is what makes restores, deferred purchases
        /// and subscription renewals work — those never return through a purchase call.
        /// </remarks>
        public static event Action<IapPurchase> OnPurchased;

        /// <summary>
        /// Installs a provider and connects it to the store.
        /// </summary>
        /// <param name="provider">The adapter to install.</param>
        /// <param name="config">The catalog. Falls back to Resources/UniIapConfig.</param>
        /// <param name="cToken">Token to cancel initialization.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
        public static async UniTask InitializeAsync(IIapProvider provider, UniIapConfig config = null,
            CancellationToken cToken = default)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            // Detach the previous provider first. Re-initializing without this leaves the old
            // adapter still raising restores into OnPurchased, which grants content twice.
            if (_provider != null) _provider.OnPurchaseRestored -= HandleRestored;

            _provider = provider;
            // Fully qualified: inside a UniTx.* namespace the bare name `Resources` binds to
            // the sibling UniTx.Resources namespace, not UnityEngine.Resources.
            _config = config != null
                ? config
                : UnityEngine.Resources.Load<UniIapConfig>(UniIapConfig.DefaultResourcePath);

            if (_config != null)
            {
                var problems = _config.DescribeProblems();

                if (!string.IsNullOrEmpty(problems))
                {
                    UniStatics.LogWarning($"UniIapConfig has problems: {problems}.", null);
                }
            }
            else
            {
                UniStatics.LogWarning(
                    "No UniIapConfig supplied and none found at Resources/UniIapConfig; " +
                    "no products will be available.", null);
            }

            _provider.OnPurchaseRestored += HandleRestored;

            await _provider.InitializeAsync(_config, cToken);
        }

        /// <summary>
        /// Buys a product and waits for the store to resolve the order.
        /// </summary>
        /// <param name="productId">The catalog id to buy.</param>
        /// <param name="cToken">Token to cancel the request.</param>
        /// <returns>The outcome, including the receipt when the purchase succeeded.</returns>
        /// <remarks>
        /// Grant content from <see cref="OnPurchased"/> rather than from this result. Both
        /// fire for a direct purchase, but only the event fires for a restore or a deferred
        /// order that clears later.
        /// </remarks>
        public static async UniTask<IapPurchase> PurchaseAsync(string productId,
            CancellationToken cToken = default)
        {
            if (string.IsNullOrEmpty(productId))
            {
                return IapPurchase.Fail(IapResult.ProductUnavailable, productId, "blank product id");
            }

            if (_provider == null) return IapPurchase.Fail(IapResult.Unsupported, productId);

            if (!_provider.IsInitialized)
            {
                return IapPurchase.Fail(IapResult.NotInitialized, productId);
            }

            // Stores serialize purchases anyway, but a second call while the sheet is open
            // surfaces as ExistingPurchasePending — a confusing error for what is really a
            // double-tapped button.
            if (_isPurchasing)
            {
                return IapPurchase.Fail(IapResult.Failed, productId, "a purchase is already in progress");
            }

            _isPurchasing = true;

            try
            {
                var purchase = await _provider.PurchaseAsync(productId, cToken);

                if (purchase.IsSuccess) OnPurchased.SafeInvoke(purchase);

                return purchase;
            }
            finally
            {
                _isPurchasing = false;
            }
        }

        /// <summary>
        /// Re-delivers previously bought non-consumables and subscriptions.
        /// </summary>
        /// <param name="cToken">Token to cancel the request.</param>
        /// <returns>True when the store reported the restore completed.</returns>
        /// <remarks>
        /// Entitlements arrive through <see cref="OnPurchased"/>, not the return value. iOS
        /// requires a player-initiated restore button to pass review, so this needs to be
        /// reachable from the shop UI.
        /// </remarks>
        public static async UniTask<bool> RestoreAsync(CancellationToken cToken = default)
        {
            if (_provider == null || !_provider.IsInitialized) return false;

            return await _provider.RestoreAsync(cToken);
        }

        /// <summary>
        /// Indicates whether the player currently owns a product.
        /// </summary>
        /// <param name="productId">The catalog id to check.</param>
        /// <remarks>
        /// Suitable for gating UI, not for protecting anything valuable — a client-side
        /// ownership check is trivially defeated. Confirm receipts on a server instead.
        /// </remarks>
        public static bool IsOwned(string productId) =>
            _provider != null && _provider.IsOwned(productId);

        /// <summary>
        /// Returns the localized price string for a product, e.g. "£2.99".
        /// </summary>
        /// <param name="productId">The catalog id to look up.</param>
        /// <param name="fallback">Returned when the store has no price for the product.</param>
        /// <returns>The store-formatted price, or <paramref name="fallback"/>.</returns>
        /// <remarks>
        /// Always prefer this to a hard-coded price. Showing a price that differs from the one
        /// on the payment sheet is a store-review rejection, and the store already returns the
        /// player's own currency and formatting.
        /// </remarks>
        public static string GetPrice(string productId, string fallback = "—")
        {
            var price = _provider?.GetLocalizedPrice(productId);
            return string.IsNullOrEmpty(price) ? fallback : price;
        }

        /// <summary>
        /// Returns the store-supplied title for a product.
        /// </summary>
        /// <param name="productId">The catalog id to look up.</param>
        /// <param name="fallback">Returned when the store has no title for the product.</param>
        /// <returns>The localized title, or <paramref name="fallback"/>.</returns>
        public static string GetTitle(string productId, string fallback = null)
        {
            var title = _provider?.GetLocalizedTitle(productId);
            return string.IsNullOrEmpty(title) ? fallback : title;
        }

        /// <summary>
        /// Detaches the provider and clears cached state.
        /// </summary>
        /// <remarks>
        /// <see cref="OnPurchased"/> is deliberately left intact. Subscribers are typically
        /// long-lived services registered at boot, and clearing their subscription here would
        /// silently stop granting content after any re-initialization.
        /// </remarks>
        public static void Reset()
        {
            if (_provider != null) _provider.OnPurchaseRestored -= HandleRestored;

            _provider = null;
            _config = null;
            _isPurchasing = false;
        }

        private static void HandleRestored(IapPurchase purchase) => OnPurchased.SafeInvoke(purchase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain reload can be disabled, in which case statics survive entering play mode
            // and the next session starts holding a dead provider from the last one.
            Reset();
            OnPurchased = null;
        }
    }
}
