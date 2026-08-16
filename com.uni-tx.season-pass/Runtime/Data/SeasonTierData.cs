using System;
using UnityEngine;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// One rung of the tier ladder and everything it hands out.
    /// </summary>
    [Serializable]
    public sealed class SeasonTierData
    {
        [Tooltip("1-based tier number. Shown to the player.")]
        [SerializeField] private int _tier;

        [Tooltip("Total season XP required to reach this tier — cumulative, not per-tier. " +
                 "Cumulative thresholds let the tier be derived from one monotonic number, " +
                 "which is what makes offline progress and server reconciliation safe.")]
        [SerializeField] private int _requiredXp;

        [Tooltip("Rewards on this tier, across all tracks.")]
        [SerializeField] private SeasonRewardData[] _rewards;

        /// <summary>
        /// Gets the 1-based tier number.
        /// </summary>
        public int Tier => _tier;

        /// <summary>
        /// Gets the cumulative season XP needed to reach this tier.
        /// </summary>
        public int RequiredXp => _requiredXp;

        /// <summary>
        /// Gets the rewards on this tier.
        /// </summary>
        public SeasonRewardData[] Rewards => _rewards ??= Array.Empty<SeasonRewardData>();
    }
}
