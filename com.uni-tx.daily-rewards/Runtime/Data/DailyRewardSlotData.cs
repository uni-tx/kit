using System;
using UniTx.Rewards;
using UnityEngine;

namespace UniTx.DailyRewards
{
    /// <summary>
    /// One day's reward on the calendar.
    /// </summary>
    /// <remarks>
    /// <c>JsonUtility</c> maps fields, not properties, so every value that must survive a
    /// round trip is a serialized field exposed through a read-only property. The reward
    /// fields mirror the generic <see cref="RewardData"/> so the default granter can map the
    /// slot onto the kit's reward service without a translation table.
    /// </remarks>
    [Serializable]
    public sealed class DailyRewardSlotData
    {
        [Tooltip("1-based day number shown in the UI. Slots are sorted on load, so the " +
                 "authoring order in the file does not matter.")]
        [SerializeField] private int _day;

        [Tooltip("Unique reward id within the calendar. Part of telemetry.")]
        [SerializeField] private string _rewardId;

        [Tooltip("How the granter should route the reward.")]
        [SerializeField] private RewardKind _kind;

        [Tooltip("Currency id, item id or cosmetic id — whatever the game's granter expects.")]
        [SerializeField] private string _itemId;

        [Tooltip("How many. Ignored for one-off cosmetics.")]
        [SerializeField] private int _amount = 1;

        [Tooltip("Addressables address of the reward icon, loaded on demand by the UI.")]
        [SerializeField] private string _iconAddress;

        [Tooltip("Marks the calendar's headline reward (the day-7 chest) so the UI can " +
                 "feature it.")]
        [SerializeField] private bool _isMilestone;

        /// <summary>
        /// Gets the 1-based day number shown in the UI.
        /// </summary>
        public int Day => _day;

        /// <summary>
        /// Gets the reward id, unique within the calendar.
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
        /// Indicates whether this is the calendar's featured reward.
        /// </summary>
        public bool IsMilestone => _isMilestone;

        /// <summary>
        /// Indicates whether the slot is missing the fields a granter needs.
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(_rewardId) && !string.IsNullOrWhiteSpace(_itemId) && _amount > 0;
    }
}
