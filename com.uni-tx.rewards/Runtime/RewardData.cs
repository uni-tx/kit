using System;
using UniTx.Content;
using UnityEngine;

namespace UniTx.Rewards
{
    /// <summary>
    /// One reward definition, loaded as JSON content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Static because it is the same for every player: what a reward hands out is decided
    /// by content, never by a save. The <see cref="IRewardService"/> routes it to the
    /// handler for its <see cref="Kind"/> — currency rewards land in the currency system,
    /// item, cosmetic and booster rewards land on a registered entity, and anything else
    /// goes to whatever handler the game installs.
    /// </para>
    /// <para>
    /// The <c>ItemId</c> is interpreted by the handler: a currency id for
    /// <see cref="RewardKind.Currency"/>, an entity id for the entity-backed kinds.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class RewardData : IData
    {
        [Tooltip("Unique reward id. Referenced by content and used in telemetry.")]
        [SerializeField] private string _id;

        [Tooltip("What kind of thing this reward is — decides which handler delivers it.")]
        [SerializeField] private RewardKind _kind = RewardKind.Currency;

        [Tooltip("Currency id, entity id or cosmetic id — whatever the handler expects.")]
        [SerializeField] private string _itemId;

        [Tooltip("How many. Ignored for one-off cosmetics.")]
        [SerializeField, Min(1)] private int _amount = 1;

        [Tooltip("Addressables address of the reward icon, loaded on demand by the UI.")]
        [SerializeField] private string _iconAddress;

        /// <inheritdoc />
        public string Id => _id;

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
        /// Indicates whether the reward carries the fields a handler needs.
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(_id) && !string.IsNullOrWhiteSpace(_itemId) && _amount > 0;

        /// <summary>
        /// Creates a reward from explicit values.
        /// </summary>
        /// <remarks>
        /// For mapping rewards from another system (a season pass slot, a server message)
        /// onto the generic definition. Content normally arrives through JSON instead.
        /// </remarks>
        /// <param name="id">The reward id.</param>
        /// <param name="kind">What kind of thing it is.</param>
        /// <param name="itemId">The granted item or currency id.</param>
        /// <param name="amount">How many units.</param>
        /// <param name="iconAddress">The icon address, or null.</param>
        public RewardData(string id, RewardKind kind, string itemId, int amount, string iconAddress)
        {
            _id = id;
            _kind = kind;
            _itemId = itemId;
            _amount = amount;
            _iconAddress = iconAddress;
        }

        /// <summary>
        /// Parameterless constructor required by <c>JsonUtility</c>.
        /// </summary>
        public RewardData()
        {
        }
    }
}
