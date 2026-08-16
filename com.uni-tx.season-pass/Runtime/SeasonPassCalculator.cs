using System.Collections.Generic;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// Tier maths derived purely from a season definition and a total XP figure.
    /// </summary>
    /// <remarks>
    /// Deliberately static and side-effect free. Every rule that decides what a player has
    /// unlocked lives here, so it can be exercised exhaustively in EditMode without a save
    /// file, a clock or a scene — and so the answer cannot drift between the UI, the claim
    /// path and a server that reimplements it.
    /// </remarks>
    public static class SeasonPassCalculator
    {
        /// <summary>
        /// Returns the highest tier reached, including repeatable bonus tiers.
        /// </summary>
        /// <param name="season">The season definition.</param>
        /// <param name="totalXp">Total season XP earned.</param>
        /// <returns>The 1-based tier number, or zero when below the first tier.</returns>
        public static int GetTier(SeasonPassData season, int totalXp) =>
            GetProgress(season, totalXp).Tier;

        /// <summary>
        /// Returns where the player sits within the tier ladder.
        /// </summary>
        /// <param name="season">The season definition.</param>
        /// <param name="totalXp">Total season XP earned.</param>
        public static TierProgress GetProgress(SeasonPassData season, int totalXp)
        {
            if (season == null) return new TierProgress(0, totalXp, 0, 0, 0, false);

            var tiers = season.Tiers;

            if (tiers.Count == 0) return new TierProgress(0, totalXp, 0, 0, 0, true);

            if (totalXp < 0) totalXp = 0;

            var index = FindTierIndex(tiers, totalXp);

            // Below the first threshold: tier 0, filling towards tier 1.
            if (index < 0)
            {
                return new TierProgress(0, totalXp, totalXp, tiers[0].RequiredXp, 0, false);
            }

            var isLast = index == tiers.Count - 1;

            if (!isLast)
            {
                var current = tiers[index].RequiredXp;
                var next = tiers[index + 1].RequiredXp;

                return new TierProgress(tiers[index].Tier, totalXp, totalXp - current,
                    next - current, 0, false);
            }

            var finalThreshold = tiers[^1].RequiredXp;
            var bonusXp = season.BonusTierXp;

            if (bonusXp <= 0)
            {
                // Ladder finished with no bonus tiers: the bar is full and stays full.
                return new TierProgress(tiers[^1].Tier, totalXp, 0, 0, 0, true);
            }

            var overflow = totalXp - finalThreshold;
            var bonusTiers = overflow / bonusXp;

            return new TierProgress(tiers[^1].Tier + bonusTiers, totalXp, overflow % bonusXp,
                bonusXp, bonusTiers, true);
        }

        /// <summary>
        /// Returns the cumulative XP a tier requires, including bonus tiers past the ladder.
        /// </summary>
        /// <param name="season">The season definition.</param>
        /// <param name="tier">The 1-based tier number.</param>
        /// <returns>The XP threshold, or zero when the tier cannot be resolved.</returns>
        public static int GetRequiredXp(SeasonPassData season, int tier)
        {
            if (season == null || tier <= 0) return 0;

            var tiers = season.Tiers;

            if (tiers.Count == 0) return 0;

            foreach (var candidate in tiers)
            {
                if (candidate.Tier == tier) return candidate.RequiredXp;
            }

            var maxTier = tiers[^1].Tier;

            if (tier <= maxTier || season.BonusTierXp <= 0) return 0;

            return tiers[^1].RequiredXp + (tier - maxTier) * season.BonusTierXp;
        }

        /// <summary>
        /// Returns how much XP would take the player from their total to the next tier.
        /// </summary>
        /// <param name="season">The season definition.</param>
        /// <param name="totalXp">Total season XP earned.</param>
        /// <returns>The XP needed, or zero when no further tier exists.</returns>
        /// <remarks>
        /// This is what a tier skip is worth. Converting a skip into exactly this much XP
        /// keeps total XP the single source of truth for tier standing, rather than adding a
        /// second counter the claim path would have to remember to consult.
        /// </remarks>
        public static int GetXpToNextTier(SeasonPassData season, int totalXp) =>
            GetProgress(season, totalXp).XpToNextTier;

        /// <summary>
        /// Collects every reward unlocked at or below a tier, across all tracks.
        /// </summary>
        /// <param name="season">The season definition.</param>
        /// <param name="tier">The highest tier reached.</param>
        /// <param name="buffer">Buffer to fill. Cleared first.</param>
        /// <returns>How many rewards were written.</returns>
        /// <remarks>
        /// Fills a caller-owned buffer rather than returning a new list, because a season pass
        /// screen re-evaluates this every time it refreshes and a per-frame allocation on a
        /// mid-range Android device is exactly the cost the playable-ads budget forbids.
        /// </remarks>
        public static int CollectUnlockedRewards(SeasonPassData season, int tier,
            List<SeasonRewardRef> buffer)
        {
            if (buffer == null) return 0;

            buffer.Clear();

            if (season == null || tier <= 0) return 0;

            foreach (var ladderTier in season.Tiers)
            {
                if (ladderTier.Tier > tier) continue;

                foreach (var reward in ladderTier.Rewards)
                {
                    if (reward == null || !reward.IsValid) continue;

                    buffer.Add(new SeasonRewardRef(season.Id, ladderTier.Tier, reward.Track, reward.RewardId));
                }
            }

            var maxTier = season.MaxTier;

            if (tier <= maxTier || season.BonusTierXp <= 0) return buffer.Count;

            // Every bonus tier pays the same repeatable rewards, distinguished by tier number.
            for (var bonusTier = maxTier + 1; bonusTier <= tier; bonusTier++)
            {
                foreach (var reward in season.BonusTierRewards)
                {
                    if (reward == null || !reward.IsValid) continue;

                    buffer.Add(new SeasonRewardRef(season.Id, bonusTier, reward.Track, reward.RewardId));
                }
            }

            return buffer.Count;
        }

        /// <summary>
        /// Returns the index of the highest tier whose threshold the XP total has passed.
        /// </summary>
        /// <param name="tiers">Tiers ordered by ascending required XP.</param>
        /// <param name="totalXp">Total season XP earned.</param>
        /// <returns>The index, or -1 when below the first tier.</returns>
        private static int FindTierIndex(IReadOnlyList<SeasonTierData> tiers, int totalXp)
        {
            // Binary search rather than a scan: a 100-tier pass re-evaluated on every XP
            // grant and every UI refresh is a hot path on the season screen.
            var low = 0;
            var high = tiers.Count - 1;
            var found = -1;

            while (low <= high)
            {
                var mid = low + ((high - low) >> 1);

                if (tiers[mid].RequiredXp <= totalXp)
                {
                    found = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return found;
        }
    }
}
