using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniTx.Store
{
    /// <summary>
    /// One shop offer: what it costs (or how it is earned) and what it pays out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three kinds cover a mobile shop's surfaces: <see cref="StoreOfferKind.Iap"/>
    /// sells through the billing provider, <see cref="StoreOfferKind.Free"/> claims on a
    /// per-offer cooldown (the repeat-visit loop), and
    /// <see cref="StoreOfferKind.Rewarded"/> trades a completed ad for the rewards.
    /// </para>
    /// <para>
    /// The price of an IAP offer is never stored here — it must come from the store
    /// (<c>UniIap.GetPrice</c>), which returns the player's own currency and formatting.
    /// <see cref="PriceHint"/> is a display-only fallback for free/rewarded offers that
    /// show a "value" line, and is never the authority for an IAP row.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class StoreOfferData
    {
        [Tooltip("Unique offer id. Referenced by saves, telemetry and the UI.")]
        [SerializeField] private string _id;

        [Tooltip("How the offer is paid for: IAP, free-on-cooldown, or rewarded ad.")]
        [SerializeField] private StoreOfferKind _kind = StoreOfferKind.Free;

        [Tooltip("Player-facing offer name, or a localization key.")]
        [SerializeField] private string _displayName;

        [Tooltip("Optional section header the UI groups offers under, e.g. \"Daily Deals\".")]
        [SerializeField] private string _section;

        [Tooltip("Optional display badge: Best value, New or Sale. Presentation only.")]
        [SerializeField] private StoreBadge _badge;

        [Tooltip("Addressables address of the offer icon.")]
        [SerializeField] private string _iconAddress;

        [Tooltip("Billing product id for IAP offers. Ignored for free and rewarded offers.")]
        [SerializeField] private string _productId;

        [Tooltip("Display-only price line for free/rewarded offers, e.g. \"Value: $4.99\". " +
                 "Never the authority for an IAP row — that price comes from the store.")]
        [SerializeField] private string _priceHint;

        [Tooltip("Seconds a free or rewarded offer waits before it can be claimed again. " +
                 "0 claims whenever the limit allows.")]
        [SerializeField] private long _cooldownSeconds;

        [Tooltip("How many times the offer can be claimed in total. 0 = unlimited.")]
        [SerializeField] private int _maxClaims;

        [Tooltip("The rewards handed out on a successful claim.")]
        [SerializeField] private List<StoreRewardData> _rewards = new();

        /// <summary>
        /// Gets the offer id.
        /// </summary>
        public string Id => _id;

        /// <summary>
        /// Gets how the offer is paid for.
        /// </summary>
        public StoreOfferKind Kind => _kind;

        /// <summary>
        /// Gets the player-facing offer name or localization key.
        /// </summary>
        public string DisplayName => _displayName;

        /// <summary>
        /// Gets the optional section header, or null.
        /// </summary>
        public string Section => _section;

        /// <summary>
        /// Gets the display badge, or <see cref="StoreBadge.None"/>.
        /// </summary>
        public StoreBadge Badge => _badge;

        /// <summary>
        /// Gets the Addressables address of the icon.
        /// </summary>
        public string IconAddress => _iconAddress;

        /// <summary>
        /// Gets the billing product id for IAP offers, or null.
        /// </summary>
        public string ProductId => _productId;

        /// <summary>
        /// Gets the display-only price line for free/rewarded offers, or null.
        /// </summary>
        public string PriceHint => _priceHint;

        /// <summary>
        /// Gets the seconds between free/rewarded claims, or 0 for no cooldown.
        /// </summary>
        public long CooldownSeconds => _cooldownSeconds;

        /// <summary>
        /// Gets the total claim limit, or 0 for unlimited.
        /// </summary>
        public int MaxClaims => _maxClaims;

        /// <summary>
        /// Gets the rewards handed out on a successful claim.
        /// </summary>
        public IReadOnlyList<StoreRewardData> Rewards => _rewards;

        /// <summary>
        /// Indicates whether the offer has the fields a granter and UI need.
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(_id) &&
            _rewards.Count > 0;

        /// <summary>
        /// Reports authoring mistakes that would misbehave at runtime rather than fail loudly.
        /// </summary>
        /// <returns>A human-readable summary, or an empty string when the offer is sound.</returns>
        public string DescribeProblems()
        {
            if (!string.IsNullOrWhiteSpace(_id) && _rewards.Count > 0) return string.Empty;

            return $"offer '{(string.IsNullOrWhiteSpace(_id) ? "?" : _id)}' is missing an " +
                   "id or has no rewards";
        }
    }
}
