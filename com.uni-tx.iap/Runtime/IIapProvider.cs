using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniTx.Iap
{
    /// <summary>
    /// A single billing backend. Implement one adapter per store SDK.
    /// </summary>
    /// <remarks>
    /// The kit depends on no store SDK. Unity IAP brings native billing libraries, manifest
    /// entries and store policy obligations, so the choice — and the build-size cost — stays
    /// with the game. A project with no adapter still runs; purchases resolve to
    /// <see cref="IapResult.Unsupported"/> instead of throwing, which is also what a desktop
    /// build should do.
    /// </remarks>
    public interface IIapProvider
    {
        /// <summary>
        /// Gets a short name used in logs, e.g. "UnityIAP".
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets a value indicating whether the store is connected and the catalog is known.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Raised when an entitlement arrives outside a purchase call.
        /// </summary>
        /// <remarks>
        /// Restores, deferred purchases that finally clear, and subscription renewals all
        /// arrive this way. A game that grants entitlements only from the return value of
        /// <see cref="PurchaseAsync"/> will drop every one of them.
        /// </remarks>
        event Action<IapPurchase> OnPurchaseRestored;

        /// <summary>
        /// Connects to the store and fetches the catalog.
        /// </summary>
        /// <param name="config">The product catalog and behaviour flags.</param>
        /// <param name="cToken">Token to cancel initialization.</param>
        UniTask InitializeAsync(UniIapConfig config, CancellationToken cToken = default);

        /// <summary>
        /// Buys a product and waits for the store to resolve the order.
        /// </summary>
        /// <param name="productId">The catalog id to buy.</param>
        /// <param name="cToken">Token to cancel the request.</param>
        /// <returns>The outcome, including the receipt when the purchase succeeded.</returns>
        UniTask<IapPurchase> PurchaseAsync(string productId, CancellationToken cToken = default);

        /// <summary>
        /// Re-delivers previously bought non-consumables and subscriptions.
        /// </summary>
        /// <param name="cToken">Token to cancel the request.</param>
        /// <returns>True when the store reported the restore completed.</returns>
        /// <remarks>
        /// Entitlements arrive through <see cref="OnPurchaseRestored"/>, not through the
        /// return value — the boolean only reports whether the store finished the sweep.
        /// iOS requires an explicit, player-initiated restore button to pass review.
        /// </remarks>
        UniTask<bool> RestoreAsync(CancellationToken cToken = default);

        /// <summary>
        /// Indicates whether the player currently owns a product.
        /// </summary>
        /// <param name="productId">The catalog id to check.</param>
        /// <remarks>
        /// Reflects what the store reported this session. It is a convenience for gating UI,
        /// not an authority — a client-side ownership check is trivially defeated, so
        /// anything valuable should be confirmed against a receipt on a server.
        /// </remarks>
        bool IsOwned(string productId);

        /// <summary>
        /// Returns the localized price string for a product, e.g. "£2.99".
        /// </summary>
        /// <param name="productId">The catalog id to look up.</param>
        /// <returns>The store-formatted price, or null when the product is unknown.</returns>
        /// <remarks>
        /// Always prefer this to a hard-coded price. The store returns the player's currency
        /// and local formatting, and showing a price that differs from the one on the payment
        /// sheet is a store-review rejection.
        /// </remarks>
        string GetLocalizedPrice(string productId);

        /// <summary>
        /// Returns the store-supplied title for a product.
        /// </summary>
        /// <param name="productId">The catalog id to look up.</param>
        /// <returns>The localized title, or null when the product is unknown.</returns>
        string GetLocalizedTitle(string productId);
    }
}
