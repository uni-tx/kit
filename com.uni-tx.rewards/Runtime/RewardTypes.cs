using UniTx.Events;

namespace UniTx.Rewards
{
    /// <summary>
    /// What a reward actually is, so the service can route it.
    /// </summary>
    /// <remarks>
    /// Enum values are stable and ordered to match <c>SeasonRewardKind</c>, so a season
    /// pass reward maps onto a generic reward without a translation table.
    /// </remarks>
    public enum RewardKind
    {
        /// <summary>
        /// Soft or hard currency, granted by amount into the currency system.
        /// </summary>
        Currency = 0,

        /// <summary>
        /// A consumable or inventory item, granted to an entity.
        /// </summary>
        Item = 1,

        /// <summary>
        /// A permanent unlock — skin, emote, avatar.
        /// </summary>
        Cosmetic = 2,

        /// <summary>
        /// A timed multiplier or boost.
        /// </summary>
        Booster = 3,

        /// <summary>
        /// Anything game-specific; the game's handler interprets the item id.
        /// </summary>
        Custom = 4,
    }

    /// <summary>
    /// Outcome of a reward delivery.
    /// </summary>
    public enum RewardGrantResult
    {
        /// <summary>
        /// The reward reached the player.
        /// </summary>
        Granted = 0,

        /// <summary>
        /// The handler refused or failed; the reward was not delivered.
        /// </summary>
        Failed = 1,

        /// <summary>
        /// The reward definition was missing the fields a handler needs.
        /// </summary>
        Rejected = 2,
    }

    /// <summary>
    /// Raised after a reward is delivered.
    /// </summary>
    /// <remarks>
    /// Struct event on the kit bus, so a toast, a quest tracker and an analytics adapter
    /// can all listen without knowing about each other.
    /// </remarks>
    public readonly struct RewardGranted : IEvent
    {
        /// <summary>
        /// The reward definition id.
        /// </summary>
        public readonly string RewardId;

        /// <summary>
        /// What kind of thing was granted.
        /// </summary>
        public readonly RewardKind Kind;

        /// <summary>
        /// The game-side id of the granted item or currency.
        /// </summary>
        public readonly string ItemId;

        /// <summary>
        /// How many units the reward definition asked for.
        /// </summary>
        /// <remarks>
        /// The requested amount, not necessarily the delivered one: a currency grant
        /// trimmed by the currency's cap still reports the full reward amount, because a
        /// handler only reports whether it delivered, not how much. A UI that must show
        /// the exact change should read the balance (or
        /// <c>CurrencyChanged</c>) rather than this field.
        /// </remarks>
        public readonly int Amount;

        /// <summary>
        /// The idempotency id the delivery was recorded under, if any.
        /// </summary>
        public readonly string GrantId;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="rewardId">The reward definition id.</param>
        /// <param name="kind">The reward kind.</param>
        /// <param name="itemId">The granted item or currency id.</param>
        /// <param name="amount">How many units.</param>
        /// <param name="grantId">The idempotency id, or null.</param>
        public RewardGranted(string rewardId, RewardKind kind, string itemId, int amount, string grantId)
        {
            RewardId = rewardId;
            Kind = kind;
            ItemId = itemId;
            Amount = amount;
            GrantId = grantId;
        }
    }
}
