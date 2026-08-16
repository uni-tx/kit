using System;
using UnityEngine;

namespace UniTx.Iap
{
    /// <summary>
    /// How a product behaves once it has been bought.
    /// </summary>
    /// <remarks>
    /// Mirrors the store-level distinction rather than a game-level one, because it decides
    /// whether a purchase may be repeated and whether it is restored on a new device.
    /// </remarks>
    public enum IapProductKind
    {
        /// <summary>
        /// Can be bought repeatedly; the entitlement is consumed by the game.
        /// </summary>
        Consumable = 0,

        /// <summary>
        /// Bought once and owned permanently; restored on reinstall.
        /// </summary>
        NonConsumable = 1,

        /// <summary>
        /// Valid for a billing period and renewed by the store.
        /// </summary>
        Subscription = 2,
    }

    /// <summary>
    /// The outcome of a purchase or restore request.
    /// </summary>
    /// <remarks>
    /// Deliberately coarser than the store's own failure enums. A game reacts to "the player
    /// cancelled" and "the network is down" very differently, but has no distinct response to
    /// the dozen ways a signature can be malformed — those collapse into
    /// <see cref="Failed"/> and are distinguished only in the log.
    /// </remarks>
    public enum IapResult
    {
        /// <summary>
        /// The purchase completed and the entitlement was granted.
        /// </summary>
        Success = 0,

        /// <summary>
        /// The player dismissed the store dialog.
        /// </summary>
        Cancelled = 1,

        /// <summary>
        /// A non-consumable or subscription the player already owns.
        /// </summary>
        AlreadyOwned = 2,

        /// <summary>
        /// The product id is not sold on this store, or is not yet approved.
        /// </summary>
        ProductUnavailable = 3,

        /// <summary>
        /// The store could not be reached.
        /// </summary>
        NetworkUnavailable = 4,

        /// <summary>
        /// The payment method was refused.
        /// </summary>
        PaymentDeclined = 5,

        /// <summary>
        /// Billing is unavailable — no provider is installed, or the platform has no store.
        /// </summary>
        Unsupported = 6,

        /// <summary>
        /// A purchase was requested before <see cref="UniIap.InitializeAsync"/> completed.
        /// </summary>
        NotInitialized = 7,

        /// <summary>
        /// Awaiting external approval, such as Ask-to-Buy parental consent.
        /// </summary>
        /// <remarks>
        /// Not a failure and not a grant. The order resolves later — possibly days later, on a
        /// subsequent launch — so the entitlement must be delivered from the restore path, not
        /// from the return value of the call that started it.
        /// </remarks>
        Deferred = 8,

        /// <summary>
        /// The purchase failed for a reason the game cannot act on.
        /// </summary>
        Failed = 9,
    }

    /// <summary>
    /// The result of a single purchase attempt.
    /// </summary>
    /// <remarks>
    /// A struct so that awaiting a purchase in a per-frame UI path allocates nothing.
    /// </remarks>
    public readonly struct IapPurchase
    {
        /// <summary>
        /// Gets the outcome of the attempt.
        /// </summary>
        public IapResult Result { get; }

        /// <summary>
        /// Gets the catalog id of the product involved.
        /// </summary>
        public string ProductId { get; }

        /// <summary>
        /// Gets the store transaction id, or null when the purchase did not complete.
        /// </summary>
        public string TransactionId { get; }

        /// <summary>
        /// Gets the raw store receipt for server-side validation, or null when unavailable.
        /// </summary>
        /// <remarks>
        /// Forwarded verbatim rather than parsed. Validating it on the device only proves the
        /// device agrees with itself; a receipt is worth checking on a server the player does
        /// not control.
        /// </remarks>
        public string Receipt { get; }

        /// <summary>
        /// Gets store-supplied failure detail, or null on success.
        /// </summary>
        public string Details { get; }

        /// <summary>
        /// Gets a value indicating whether the entitlement was granted.
        /// </summary>
        public bool IsSuccess => Result == IapResult.Success;

        /// <summary>
        /// Initializes a new instance of the <see cref="IapPurchase"/> struct.
        /// </summary>
        /// <param name="result">The outcome of the attempt.</param>
        /// <param name="productId">The catalog id of the product involved.</param>
        /// <param name="transactionId">The store transaction id, if any.</param>
        /// <param name="receipt">The raw store receipt, if any.</param>
        /// <param name="details">Store-supplied failure detail, if any.</param>
        public IapPurchase(IapResult result, string productId, string transactionId = null,
            string receipt = null, string details = null)
        {
            Result = result;
            ProductId = productId;
            TransactionId = transactionId;
            Receipt = receipt;
            Details = details;
        }

        /// <summary>
        /// Creates a failed result for a product.
        /// </summary>
        /// <param name="result">The failure to report.</param>
        /// <param name="productId">The product that was requested.</param>
        /// <param name="details">Optional detail for the log.</param>
        /// <returns>A purchase result carrying the failure.</returns>
        public static IapPurchase Fail(IapResult result, string productId, string details = null) =>
            new IapPurchase(result, productId, details: details);

        /// <inheritdoc />
        public override string ToString() =>
            $"{ProductId}: {Result}{(string.IsNullOrEmpty(Details) ? string.Empty : $" ({Details})")}";
    }

    /// <summary>
    /// One product as the game declares it, before the store has been asked about it.
    /// </summary>
    /// <remarks>
    /// Store-specific ids are separate fields because the App Store and Google Play impose
    /// different id rules, and a shipped id cannot be renamed. Leaving them blank falls back
    /// to <see cref="Id"/>, which is the common case.
    /// </remarks>
    [Serializable]
    public sealed class IapProductStub
    {
        [Tooltip("Catalog id used by game code. Falls back to this on any store with no override.")]
        [SerializeField] private string _id;

        [Tooltip("Consumable, non-consumable or subscription.")]
        [SerializeField] private IapProductKind _kind = IapProductKind.Consumable;

        [Tooltip("App Store product id, when it differs from the catalog id.")]
        [SerializeField] private string _appleId;

        [Tooltip("Google Play product id, when it differs from the catalog id.")]
        [SerializeField] private string _googleId;

        /// <summary>
        /// Gets the catalog id used by game code.
        /// </summary>
        public string Id => _id;

        /// <summary>
        /// Gets the product kind.
        /// </summary>
        public IapProductKind Kind => _kind;

        /// <summary>
        /// Gets the App Store id override, which may be empty.
        /// </summary>
        public string AppleId => _appleId;

        /// <summary>
        /// Gets the Google Play id override, which may be empty.
        /// </summary>
        public string GoogleId => _googleId;

        /// <summary>
        /// Gets the id this product is sold under on the current platform.
        /// </summary>
        /// <returns>The platform-specific id, or <see cref="Id"/> when no override is set.</returns>
        public string ResolveStoreId()
        {
#if UNITY_IOS || UNITY_TVOS
            if (!string.IsNullOrEmpty(_appleId)) return _appleId;
#elif UNITY_ANDROID
            if (!string.IsNullOrEmpty(_googleId)) return _googleId;
#endif
            return _id;
        }
    }
}
