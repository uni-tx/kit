using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniTx.Ladder
{
    /// <summary>
    /// One rung of the ladder: a cumulative step threshold and the rewards it pays.
    /// </summary>
    /// <remarks>
    /// The <see cref="Steps"/> value is <b>cumulative</b> — the total steps climbed must
    /// reach it for the rung to unlock. Rungs are authoring-ordered by threshold on load,
    /// so a designer writes "10, 25, 50" and the ladder reads "first rung at 10 steps,
    /// second at 25, grand prize at 50".
    /// </remarks>
    [Serializable]
    public sealed class LadderRungData
    {
        [Tooltip("Unique rung id within the ladder. Part of the recorded claim key, so " +
                 "changing it on a live rung restarts every player's progress.")]
        [SerializeField] private string _id;

        [Tooltip("Player-facing rung name, or a localization key.")]
        [SerializeField] private string _displayName;

        [Tooltip("Addressables address of the rung icon, loaded on demand by the UI.")]
        [SerializeField] private string _iconAddress;

        [Tooltip("Cumulative steps that must be climbed to reach this rung. Values are " +
                 "sorted on load; the last rung in the sorted order is the grand prize.")]
        [SerializeField] private int _steps;

        [Tooltip("The rewards granted when this rung is claimed.")]
        [SerializeField] private LadderRewardData[] _rewards;

        /// <summary>
        /// Gets the unique rung id within the ladder.
        /// </summary>
        public string Id => _id;

        /// <summary>
        /// Gets the player-facing rung name or localization key.
        /// </summary>
        public string DisplayName => _displayName;

        /// <summary>
        /// Gets the Addressables address of the rung icon.
        /// </summary>
        public string IconAddress => _iconAddress;

        /// <summary>
        /// Gets the cumulative steps that unlock this rung.
        /// </summary>
        public int Steps => _steps;

        /// <summary>
        /// Gets the rewards granted on claim.
        /// </summary>
        public IReadOnlyList<LadderRewardData> Rewards => _rewards;

        /// <summary>
        /// Indicates whether the rung is missing the fields it needs to work.
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(_id) &&
            _steps > 0 &&
            _rewards is { Length: > 0 };
    }
}
