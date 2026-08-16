namespace UniTx.SeasonPass
{
    /// <summary>
    /// Merges a device record with whatever a backend believes.
    /// </summary>
    /// <remarks>
    /// Every rule here resolves <i>upward</i>: the higher XP total wins, ownership is a union,
    /// a claim recorded on either side stays recorded. That asymmetry is deliberate. A brief
    /// disconnect that hands back a stale server read must never snap a player's tier
    /// backwards — progress that visibly rewinds is read as lost progress even when the next
    /// sync repairs it, and a reward already delivered must not become claimable again.
    /// </remarks>
    public static class SeasonPassReconciler
    {
        /// <summary>
        /// Merges a remote record into the local one, keeping the better of the two.
        /// </summary>
        /// <param name="local">The device record, mutated in place.</param>
        /// <param name="remote">The backend's record. Null is a no-op.</param>
        /// <returns><c>true</c> when the local record changed.</returns>
        public static bool Reconcile(SeasonPassSavedData local, SeasonPassSavedData remote)
        {
            if (local == null || remote == null) return false;

            // A record for a different season says nothing about this one. Merging across a
            // rollover would resurrect the previous season's claims against the new ladder.
            if (!string.Equals(local.SeasonId, remote.SeasonId, System.StringComparison.Ordinal))
            {
                return false;
            }

            var changed = false;

            if (remote.TotalXp > local.TotalXp)
            {
                local.RaiseXpTo(remote.TotalXp);
                changed = true;
            }

            if (remote.HighestOwnedTrack > local.HighestOwnedTrack)
            {
                local.GrantTrack(remote.HighestOwnedTrack);
                changed = true;
            }

            foreach (var claimKey in remote.ClaimedKeys)
            {
                if (local.HasClaimed(claimKey)) continue;

                local.RecordClaim(claimKey);
                changed = true;
            }

            var bankedDelta = remote.BankedTierSkips - local.BankedTierSkips;

            if (bankedDelta > 0)
            {
                local.BankTierSkips(bankedDelta);
                changed = true;
            }

            var purchasedDelta = remote.PurchasedTierSkips - local.PurchasedTierSkips;

            if (purchasedDelta > 0)
            {
                // Purchase counts drive the season's buy limit, so the higher figure is the
                // safe one: undercounting would let a player exceed the cap.
                local.RecordTierSkipPurchase(purchasedDelta);
                changed = true;
            }

            return changed;
        }
    }
}
