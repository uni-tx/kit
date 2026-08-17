using UniTx.Events;

namespace UniTx.Currency
{
    /// <summary>
    /// What kind of currency this is.
    /// </summary>
    /// <remarks>
    /// The soft/hard split exists for tuning and UI, not enforcement: a game may spend
    /// either however it likes. Soft currency powers the core loop and is meant to flow
    /// freely; hard currency is usually premium and scarce.
    /// </remarks>
    public enum CurrencyKind
    {
        /// <summary>
        /// Earned through play — coins, stars, energy.
        /// </summary>
        Soft = 0,

        /// <summary>
        /// Bought or granted — gems, crystals, premium points.
        /// </summary>
        Hard = 1,
    }

    /// <summary>
    /// Outcome of a currency grant.
    /// </summary>
    public enum CurrencyGrantResult
    {
        /// <summary>
        /// The balance was increased.
        /// </summary>
        Granted = 0,

        /// <summary>
        /// The same grant id was already applied; nothing changed.
        /// </summary>
        Duplicate = 1,

        /// <summary>
        /// Some or all of the amount was dropped by the currency's maximum balance.
        /// </summary>
        Capped = 2,

        /// <summary>
        /// No currency with that id is registered.
        /// </summary>
        UnknownCurrency = 3,

        /// <summary>
        /// The amount was zero or negative.
        /// </summary>
        Rejected = 4,
    }

    /// <summary>
    /// Raised after a currency balance changes.
    /// </summary>
    /// <remarks>
    /// Struct event on the kit bus, so a HUD, a toast and an analytics adapter can all
    /// listen without knowing about each other — and without boxing on every raise.
    /// </remarks>
    public readonly struct CurrencyChanged : IEvent
    {
        /// <summary>
        /// The currency whose balance changed.
        /// </summary>
        public readonly string CurrencyId;

        /// <summary>
        /// The balance before the change.
        /// </summary>
        public readonly int OldBalance;

        /// <summary>
        /// The balance after the change.
        /// </summary>
        public readonly int NewBalance;

        /// <summary>
        /// Who caused it — a grant, a spend, or whatever the caller passed.
        /// </summary>
        public readonly string Reason;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="currencyId">The currency id.</param>
        /// <param name="oldBalance">The balance before.</param>
        /// <param name="newBalance">The balance after.</param>
        /// <param name="reason">The cause, for analytics and debugging.</param>
        public CurrencyChanged(string currencyId, int oldBalance, int newBalance, string reason)
        {
            CurrencyId = currencyId;
            OldBalance = oldBalance;
            NewBalance = newBalance;
            Reason = reason;
        }
    }
}
