using System;

namespace UniTx.Store
{
    /// <summary>
    /// Pure store rules: whether an offer is on cooldown, at its claim limit, or ready.
    /// </summary>
    /// <remarks>
    /// No I/O, no state of its own — everything is derived from the offer definition, the
    /// saved record and the current time, so the rules can be unit-tested without the Unity
    /// engine.
    /// </remarks>
    public static class StoreCalculator
    {
        /// <summary>
        /// Evaluates an offer's current state against its record and the clock.
        /// </summary>
        /// <param name="offer">The offer definition.</param>
        /// <param name="record">The player's record, or null when none exists.</param>
        /// <param name="nowUnix">The current time, in unix seconds.</param>
        /// <returns>The state the offer is in.</returns>
        public static StoreOfferState EvaluateState(StoreOfferData offer,
            StoreOfferRecord record, long nowUnix)
        {
            if (offer == null || !offer.IsValid) return StoreOfferState.None;

            if (IsLimitReached(offer, record)) return StoreOfferState.LimitReached;

            if (IsOnCooldown(offer, record, nowUnix)) return StoreOfferState.OnCooldown;

            // An offer that has been claimed before but is claimable again (a repeatable
            // free offer off cooldown) still reads Ready — claimability wins, and the
            // snapshot's ClaimCount tells the UI it was taken before.
            return StoreOfferState.Ready;
        }

        /// <summary>
        /// Indicates whether a claim would be allowed right now.
        /// </summary>
        /// <param name="offer">The offer definition.</param>
        /// <param name="record">The player's record, or null when none exists.</param>
        /// <param name="nowUnix">The current time, in unix seconds.</param>
        public static bool CanClaim(StoreOfferData offer, StoreOfferRecord record,
            long nowUnix)
        {
            if (offer == null || !offer.IsValid) return false;

            if (IsLimitReached(offer, record)) return false;

            return !IsOnCooldown(offer, record, nowUnix);
        }

        /// <summary>
        /// Computes the seconds until the offer can be claimed again.
        /// </summary>
        /// <param name="offer">The offer definition.</param>
        /// <param name="record">The player's record, or null when none exists.</param>
        /// <param name="nowUnix">The current time, in unix seconds.</param>
        /// <returns>0 when claimable now, otherwise the remaining wait.</returns>
        public static long RemainingCooldownSeconds(StoreOfferData offer,
            StoreOfferRecord record, long nowUnix)
        {
            if (offer == null || record == null || offer.CooldownSeconds <= 0) return 0;

            var readyAt = record.LastClaimUnix + offer.CooldownSeconds;

            return Math.Max(0, readyAt - nowUnix);
        }

        /// <summary>
        /// Indicates whether the offer has hit its total claim limit.
        /// </summary>
        /// <param name="offer">The offer definition.</param>
        /// <param name="record">The player's record, or null when none exists.</param>
        public static bool IsLimitReached(StoreOfferData offer, StoreOfferRecord record)
        {
            if (offer == null || offer.MaxClaims <= 0) return false;

            var count = record?.ClaimCount ?? 0;

            return count >= offer.MaxClaims;
        }

        /// <summary>
        /// Indicates whether the offer is still waiting out its cooldown.
        /// </summary>
        /// <param name="offer">The offer definition.</param>
        /// <param name="record">The player's record, or null when none exists.</param>
        /// <param name="nowUnix">The current time, in unix seconds.</param>
        public static bool IsOnCooldown(StoreOfferData offer, StoreOfferRecord record,
            long nowUnix)
        {
            if (offer == null || offer.CooldownSeconds <= 0 || record == null) return false;

            return nowUnix < record.LastClaimUnix + offer.CooldownSeconds;
        }
    }
}
