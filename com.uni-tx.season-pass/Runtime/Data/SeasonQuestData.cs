using System;
using UnityEngine;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// A challenge that pays season XP when completed.
    /// </summary>
    [Serializable]
    public sealed class SeasonQuestData
    {
        [Tooltip("Unique within the season. Progress is recorded against it.")]
        [SerializeField] private string _questId;

        [Tooltip("Player-facing description, or a localization key.")]
        [SerializeField] private string _description;

        [Tooltip("How often the quest resets.")]
        [SerializeField] private SeasonQuestScope _scope;

        [Tooltip("How much progress completes it.")]
        [SerializeField, Min(1)] private int _goal = 1;

        [Tooltip("Season XP paid on completion.")]
        [SerializeField, Min(0)] private int _xpReward = 100;

        [Tooltip("Restrict to players who own a paid track.")]
        [SerializeField] private bool _requiresPaidTrack;

        [Tooltip("ISO 8601 UTC. Blank means available from the season start.")]
        [SerializeField] private string _availableFromUtc;

        [Tooltip("ISO 8601 UTC. Blank means available until the season ends.")]
        [SerializeField] private string _availableUntilUtc;

        [NonSerialized] private bool _isPrepared;
        [NonSerialized] private DateTime? _availableFrom;
        [NonSerialized] private DateTime? _availableUntil;

        /// <summary>
        /// Gets the quest id.
        /// </summary>
        public string QuestId => _questId;

        /// <summary>
        /// Gets the player-facing description or localization key.
        /// </summary>
        public string Description => _description;

        /// <summary>
        /// Gets how often the quest resets.
        /// </summary>
        public SeasonQuestScope Scope => _scope;

        /// <summary>
        /// Gets the progress needed to complete it.
        /// </summary>
        public int Goal => Mathf.Max(1, _goal);

        /// <summary>
        /// Gets the season XP paid on completion.
        /// </summary>
        public int XpReward => _xpReward;

        /// <summary>
        /// Indicates whether the quest is restricted to paid tracks.
        /// </summary>
        public bool RequiresPaidTrack => _requiresPaidTrack;

        /// <summary>
        /// Indicates whether the quest is inside its availability window.
        /// </summary>
        /// <param name="utcNow">The current UTC time, from the kit clock.</param>
        public bool IsAvailableAt(DateTime utcNow)
        {
            Prepare();

            if (_availableFrom.HasValue && utcNow < _availableFrom.Value) return false;

            return !_availableUntil.HasValue || utcNow < _availableUntil.Value;
        }

        private void Prepare()
        {
            if (_isPrepared) return;

            _isPrepared = true;
            _availableFrom = SeasonPassTime.ParseUtc(_availableFromUtc);
            _availableUntil = SeasonPassTime.ParseUtc(_availableUntilUtc);
        }
    }
}
