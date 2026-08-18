using System;
using UniTx.Rewards;
using UnityEngine;

namespace UniTx.Store
{
    /// <summary>
    /// One reward an offer grants on claim.
    /// </summary>
    /// <remarks>
    /// The fields mirror the generic <see cref="RewardData"/> so the default granter can map
    /// the offer onto the kit's reward service without a translation table.
    /// </remarks>
    [Serializable]
    public sealed class StoreRewardData
    {
        [Tooltip("Unique reward id within the offer. Part of the idempotent grant id, so " +
                 "changing it on a live offer re-delivers the reward once.")]
        [SerializeField] private string _rewardId;

        [Tooltip("How the granter should route the reward.")]
        [SerializeField] private RewardKind _kind;

        [Tooltip("Currency id, item id or cosmetic id — whatever the game's granter expects.")]
        [SerializeField] private string _itemId;

        [Tooltip("How many. Ignored for one-off cosmetics.")]
        [SerializeField] private int _amount = 1;

        [Tooltip("Addressables address of the reward icon, loaded on demand by the UI.")]
        [SerializeField] private string _iconAddress;

        /// <summary>
        /// Gets the reward id, unique within the offer.
        /// </summary>
        public string RewardId => _rewardId;

        /// <summary>
        /// Gets what kind of thing this reward is.
        /// </summary>
        public RewardKind Kind => _kind;

        /// <summary>
        /// Gets the game-side id of the granted item or currency.
        /// </summary>
        public string ItemId => _itemId;

        /// <summary>
        /// Gets how many units are granted.
        /// </summary>
        public int Amount => _amount;

        /// <summary>
        /// Gets the Addressables address of the icon.
        /// </summary>
        public string IconAddress => _iconAddress;

        /// <summary>
        /// Indicates whether the reward is missing the fields a granter needs.
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(_rewardId) &&
            !string.IsNullOrWhiteSpace(_itemId) &&
            _amount > 0;
    }
}
