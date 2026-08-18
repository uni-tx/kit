using System;
using System.Collections.Generic;
using UniTx.Events;

namespace UniTx.Store
{
    /// <summary>
    /// How an offer is paid for.
    /// </summary>
    /// <remarks>
    /// Mirrors the three monetization surfaces a mobile shop has: sell through the store,
    /// give away on a timer, or trade attention for a reward. The enum value decides which
    /// path <c>StoreService.ClaimAsync</c> takes and how the UI presents the row.
    /// </remarks>
    public enum StoreOfferKind
    {
        /// <summary>
        /// Sold through the billing provider; the price comes from the store.
        /// </summary>
        Iap = 0,

        /// <summary>
        /// Claimable with no payment, typically on a per-offer cooldown.
        /// </summary>
        Free = 1,

        /// <summary>
        /// A rewarded ad; the reward is granted only when the ad completes.
        /// </summary>
        Rewarded = 2,
    }

    /// <summary>
    /// A display badge an offer can carry, e.g. "Best value".
    /// </summary>
    /// <remarks>
    /// Presentation-only: badges never change the rules, so a designer can retune the
    /// shop's persuasion without touching the service.
    /// </remarks>
    public enum StoreBadge
    {
        /// <summary>
        /// No badge.
        /// </summary>
        None = 0,

        /// <summary>
        /// "Best value" — the strongest offer per unit of spend.
        /// </summary>
        BestValue = 1,

        /// <summary>
        /// "New" — recently added.
        /// </summary>
        New = 2,

        /// <summary>
        /// "Sale" — discounted.
        /// </summary>
        Sale = 3,
    }

    /// <summary>
    /// Outcome of a claim attempt.
    /// </summary>
    public enum StoreClaimResult
    {
        /// <summary>
        /// The rewards reached the player.
        /// </summary>
        Claimed = 0,

        /// <summary>
        /// An IAP offer; the purchase was forwarded to the store and the outcome is the
        /// billing verdict, not a delivery.
        /// </summary>
        /// <remarks>
        /// The entitlement itself arrives through <c>UniIap.OnPurchased</c> — the only
        /// path restores, deferred purchases and renewals ever take — not through this
        /// return value.
        /// </remarks>
        Purchased = 1,

        /// <summary>
        /// A rewarded offer; the ad was watched to completion and the reward granted.
        /// </summary>
        Rewarded = 2,

        /// <summary>
        /// The offer is on cooldown; the calculator knows how long remains.
        /// </summary>
        OnCooldown = 3,

        /// <summary>
        /// The offer has hit its claim limit.
        /// </summary>
        LimitReached = 4,

        /// <summary>
        /// The rewarded ad did not complete (skipped or failed); nothing was granted.
        /// </summary>
        AdNotCompleted = 5,

        /// <summary>
        /// The rewarded ad had nothing loaded, or no ad provider is installed.
        /// </summary>
        AdNotReady = 6,

        /// <summary>
        /// A granter refused or failed; nothing was recorded, so the offer stays claimable.
        /// </summary>
        GrantFailed = 7,

        /// <summary>
        /// No store is loaded.
        /// </summary>
        NoStore = 8,

        /// <summary>
        /// The offer id does not exist in the loaded store.
        /// </summary>
        NoOffer = 9,

        /// <summary>
        /// The offer is missing the fields a granter needs.
        /// </summary>
        Rejected = 10,

        /// <summary>
        /// An IAP purchase the player dismissed.
        /// </summary>
        Cancelled = 11,

        /// <summary>
        /// An IAP offer the player already owns (a non-consumable bought before).
        /// </summary>
        AlreadyOwned = 12,

        /// <summary>
        /// An IAP purchase awaiting external approval, such as Ask-to-Buy.
        /// </summary>
        Deferred = 13,

        /// <summary>
        /// The IAP product is not sold on this store, or no billing provider is installed.
        /// </summary>
        Unavailable = 14,

        /// <summary>
        /// The IAP purchase failed for a reason the game cannot act on.
        /// </summary>
        PurchaseFailed = 15,
    }

    /// <summary>
    /// What a shop screen needs to know about one offer right now.
    /// </summary>
    public enum StoreOfferState
    {
        /// <summary>
        /// No store is loaded.
        /// </summary>
        None = 0,

        /// <summary>
        /// Claimable right now — a free offer off cooldown, an IAP, or a rewarded offer.
        /// </summary>
        Ready = 1,

        /// <summary>
        /// A free or rewarded offer waiting out its cooldown.
        /// </summary>
        OnCooldown = 2,

        /// <summary>
        /// The offer's claim limit has been reached.
        /// </summary>
        LimitReached = 3,
    }

    /// <summary>
    /// Raised after a delivery attempt failed; the offer stays claimable for retry.
    /// </summary>
    public readonly struct StoreDeliveryFailed : IEvent
    {
        /// <summary>
        /// The store the offer belongs to.
        /// </summary>
        public readonly string StoreId;

        /// <summary>
        /// The offer whose delivery failed.
        /// </summary>
        public readonly string OfferId;

        /// <summary>
        /// The reward id that was refused.
        /// </summary>
        public readonly string RewardId;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="storeId">The owning store id.</param>
        /// <param name="offerId">The offer whose delivery failed.</param>
        /// <param name="rewardId">The refused reward id.</param>
        public StoreDeliveryFailed(string storeId, string offerId, string rewardId)
        {
            StoreId = storeId;
            OfferId = offerId;
            RewardId = rewardId;
        }
    }

    /// <summary>
    /// Raised after an offer's rewards are delivered.
    /// </summary>
    /// <remarks>
    /// Struct event on the kit bus, so a toast, an analytics adapter and a quest tracker
    /// can all listen without knowing about each other. Raised only after delivery, never
    /// on a refusal.
    /// </remarks>
    public readonly struct StoreOfferClaimed : IEvent
    {
        /// <summary>
        /// The store the offer belongs to.
        /// </summary>
        public readonly string StoreId;

        /// <summary>
        /// The claimed offer id.
        /// </summary>
        public readonly string OfferId;

        /// <summary>
        /// How the offer is paid for.
        /// </summary>
        public readonly StoreOfferKind Kind;

        /// <summary>
        /// The idempotency id the delivery was recorded under.
        /// </summary>
        public readonly string GrantId;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="storeId">The owning store id.</param>
        /// <param name="offerId">The claimed offer id.</param>
        /// <param name="kind">How the offer is paid for.</param>
        /// <param name="grantId">The idempotency id.</param>
        public StoreOfferClaimed(string storeId, string offerId, StoreOfferKind kind,
            string grantId)
        {
            StoreId = storeId;
            OfferId = offerId;
            Kind = kind;
            GrantId = grantId;
        }
    }

    /// <summary>
    /// A reference to one offer, carried through the granter for logging and telemetry.
    /// </summary>
    public readonly struct StoreOfferRef : IEquatable<StoreOfferRef>
    {
        /// <summary>
        /// The store the offer belongs to.
        /// </summary>
        public readonly string StoreId;

        /// <summary>
        /// The offer id within the store.
        /// </summary>
        public readonly string OfferId;

        /// <summary>
        /// Creates a reference to one offer.
        /// </summary>
        /// <param name="storeId">The owning store id.</param>
        /// <param name="offerId">The offer id within the store.</param>
        public StoreOfferRef(string storeId, string offerId)
        {
            StoreId = storeId;
            OfferId = offerId;
        }

        /// <summary>
        /// Builds the idempotent grant id for one reward of this offer.
        /// </summary>
        /// <param name="storeId">The owning store id.</param>
        /// <param name="offerId">The offer id.</param>
        /// <param name="rewardId">The reward id within the offer.</param>
        /// <param name="claimKey">The claim discriminator — the claim number for free and
        /// rewarded offers, the store transaction id for IAP offers.</param>
        public static string GrantId(string storeId, string offerId, string rewardId,
            string claimKey) => $"store:{storeId}:{offerId}:{rewardId}:{claimKey}";

        /// <inheritdoc />
        public bool Equals(StoreOfferRef other) =>
            string.Equals(StoreId, other.StoreId, StringComparison.Ordinal) &&
            string.Equals(OfferId, other.OfferId, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is StoreOfferRef other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(StoreId, OfferId);

        /// <inheritdoc />
        public override string ToString() => $"{StoreId}/{OfferId}";
    }

    /// <summary>
    /// Everything a shop screen needs about one offer right now.
    /// </summary>
    /// <remarks>
    /// A struct so per-frame UI reads allocate nothing.
    /// </remarks>
    public readonly struct StoreOfferSnapshot
    {
        /// <summary>
        /// Gets the offer id.
        /// </summary>
        public string OfferId { get; }

        /// <summary>
        /// Gets how the offer is paid for.
        /// </summary>
        public StoreOfferKind Kind { get; }

        /// <summary>
        /// Gets the offer's current state.
        /// </summary>
        public StoreOfferState State { get; }

        /// <summary>
        /// Gets seconds until the offer can be claimed again, or 0 when claimable now.
        /// </summary>
        public long CooldownRemainingSeconds { get; }

        /// <summary>
        /// Gets how many times the offer has been claimed this session's save.
        /// </summary>
        public int ClaimCount { get; }

        /// <summary>
        /// Creates an offer snapshot.
        /// </summary>
        /// <param name="offerId">The offer id.</param>
        /// <param name="kind">How the offer is paid for.</param>
        /// <param name="state">The current state.</param>
        /// <param name="cooldownRemainingSeconds">Seconds until claimable again.</param>
        /// <param name="claimCount">How many times claimed.</param>
        public StoreOfferSnapshot(string offerId, StoreOfferKind kind, StoreOfferState state,
            long cooldownRemainingSeconds, int claimCount)
        {
            OfferId = offerId;
            Kind = kind;
            State = state;
            CooldownRemainingSeconds = cooldownRemainingSeconds;
            ClaimCount = claimCount;
        }
    }

    /// <summary>
    /// Everything a shop screen needs in one value.
    /// </summary>
    public readonly struct StoreSnapshot
    {
        /// <summary>
        /// Gets the store id, or null when none is loaded.
        /// </summary>
        public string StoreId { get; }

        /// <summary>
        /// Gets the per-offer state, in content order.
        /// </summary>
        public IReadOnlyList<StoreOfferSnapshot> Offers { get; }

        /// <summary>
        /// Creates the snapshot.
        /// </summary>
        /// <param name="storeId">The store id, or null.</param>
        /// <param name="offers">The per-offer state, in content order.</param>
        public StoreSnapshot(string storeId, IReadOnlyList<StoreOfferSnapshot> offers)
        {
            StoreId = storeId;
            Offers = offers;
        }
    }
}
