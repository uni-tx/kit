using System;
using UnityEngine;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// A whitelisted way to earn season XP, with its own daily ceiling.
    /// </summary>
    /// <remarks>
    /// The whitelist is the client-side half of the anti-abuse story: a call site that passes
    /// an unrecognised source id is rejected rather than silently trusted, so a typo or a
    /// tampered call cannot mint XP. The ceiling caps how much a single loop can farm per day.
    /// </remarks>
    [Serializable]
    public sealed class SeasonXpSourceData
    {
        [Tooltip("Id passed to GrantXpAsync. Anything not listed here is refused.")]
        [SerializeField] private string _sourceId;

        [Tooltip("XP granted per event when the caller does not pass an explicit amount.")]
        [SerializeField] private int _xpPerEvent = 10;

        [Tooltip("Most XP this source can contribute in one UTC day. 0 means uncapped.")]
        [SerializeField, Min(0)] private int _dailyCap;

        [Tooltip("Restrict this source to players who own a paid track.")]
        [SerializeField] private bool _requiresPaidTrack;

        /// <summary>
        /// Gets the source id callers pass to grant XP.
        /// </summary>
        public string SourceId => _sourceId;

        /// <summary>
        /// Gets the default XP per event.
        /// </summary>
        public int XpPerEvent => _xpPerEvent;

        /// <summary>
        /// Gets the daily ceiling, or zero when uncapped.
        /// </summary>
        public int DailyCap => _dailyCap;

        /// <summary>
        /// Indicates whether the source is restricted to paid tracks.
        /// </summary>
        public bool RequiresPaidTrack => _requiresPaidTrack;
    }
}
