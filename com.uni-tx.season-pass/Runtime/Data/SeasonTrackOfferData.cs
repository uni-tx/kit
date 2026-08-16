using System;
using UnityEngine;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// How one paid track is sold this season.
    /// </summary>
    /// <remarks>
    /// Both payment routes are optional and independent. A season can sell the premium track
    /// for money only, for gems only, or for either — and the same definition covers a season
    /// where the track is given away, by leaving both blank and unlocking it externally.
    /// </remarks>
    [Serializable]
    public sealed class SeasonTrackOfferData
    {
        [Tooltip("Which track this offer unlocks.")]
        [SerializeField] private SeasonTrack _track = SeasonTrack.Premium;

        [Tooltip("Store product id. Blank means this track is not sold for money. The season " +
                 "pass never contacts a store itself — the IAP bridge maps this id back here.")]
        [SerializeField] private string _productId;

        [Tooltip("In-game currency id charged through the wallet. Blank means no currency sale.")]
        [SerializeField] private string _currencyId;

        [Tooltip("How much of that currency the track costs.")]
        [SerializeField, Min(0)] private int _currencyCost;

        [Tooltip("Tier skips included with the purchase, granted immediately.")]
        [SerializeField, Min(0)] private int _includedTierSkips;

        /// <summary>
        /// Gets the track this offer unlocks.
        /// </summary>
        public SeasonTrack Track => _track;

        /// <summary>
        /// Gets the store product id, or an empty string when not sold for money.
        /// </summary>
        public string ProductId => _productId;

        /// <summary>
        /// Gets the in-game currency id, or an empty string when not sold for currency.
        /// </summary>
        public string CurrencyId => _currencyId;

        /// <summary>
        /// Gets the currency cost.
        /// </summary>
        public int CurrencyCost => _currencyCost;

        /// <summary>
        /// Gets how many tier skips come bundled with the purchase.
        /// </summary>
        public int IncludedTierSkips => _includedTierSkips;

        /// <summary>
        /// Indicates whether this track can be bought with in-game currency.
        /// </summary>
        public bool SellsForCurrency => !string.IsNullOrWhiteSpace(_currencyId) && _currencyCost > 0;

        /// <summary>
        /// Indicates whether this track can be bought from a store.
        /// </summary>
        public bool SellsForMoney => !string.IsNullOrWhiteSpace(_productId);
    }
}
