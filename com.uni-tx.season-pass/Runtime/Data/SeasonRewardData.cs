using System;
using UnityEngine;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// One reward slot on one track of one tier.
    /// </summary>
    /// <remarks>
    /// <c>JsonUtility</c> maps fields, not properties, so every value that must survive a
    /// round trip is a serialized field exposed through a read-only property.
    /// </remarks>
    [Serializable]
    public sealed class SeasonRewardData
    {
        [Tooltip("Unique within its tier and track. Part of the recorded claim key, so " +
                 "renaming it in a live season makes the reward claimable a second time.")]
        [SerializeField] private string _rewardId;

        [Tooltip("Which track has to be owned for this reward to be claimable.")]
        [SerializeField] private SeasonTrack _track;

        [Tooltip("How the granter should route the reward.")]
        [SerializeField] private SeasonRewardKind _kind;

        [Tooltip("Currency id, item id or cosmetic id — whatever the game's granter expects.")]
        [SerializeField] private string _itemId;

        [Tooltip("How many. Ignored for one-off cosmetics.")]
        [SerializeField] private int _amount = 1;

        [Tooltip("Addressables address of the reward icon, loaded on demand by the UI.")]
        [SerializeField] private string _iconAddress;

        [Tooltip("Marks the season's headline reward so the UI can feature it.")]
        [SerializeField] private bool _isHighlight;

        /// <summary>
        /// Gets the reward id, unique within its tier and track.
        /// </summary>
        public string RewardId => _rewardId;

        /// <summary>
        /// Gets the track this reward belongs to.
        /// </summary>
        public SeasonTrack Track => _track;

        /// <summary>
        /// Gets what kind of thing this reward is.
        /// </summary>
        public SeasonRewardKind Kind => _kind;

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
        /// Indicates whether this is the season's featured reward.
        /// </summary>
        public bool IsHighlight => _isHighlight;

        /// <summary>
        /// Indicates whether the reward is missing the fields a granter needs.
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(_rewardId) && !string.IsNullOrWhiteSpace(_itemId) && _amount > 0;
    }
}
