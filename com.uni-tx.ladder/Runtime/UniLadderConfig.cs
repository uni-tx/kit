using UnityEngine;

namespace UniTx.Ladder
{
    /// <summary>
    /// Ladder policy, created via <c>Assets ▸ Create ▸ UniTx ▸ Ladder ▸ Config</c>.
    /// </summary>
    /// <remarks>
    /// Policy lives here rather than in the ladder JSON because it is a product decision
    /// that holds across every ladder event — whether a claim is flushed to disk
    /// immediately, which ladder is selected — while the JSON is content a designer retunes
    /// per event.
    /// </remarks>
    [CreateAssetMenu(fileName = "UniLadderConfig", menuName = "UniTx/Ladder/Config")]
    public sealed class UniLadderConfig : ScriptableObject
    {
        /// <summary>
        /// Resources path the service falls back to when no config is supplied.
        /// </summary>
        public const string DefaultResourcePath = "UniLadderConfig";

        [Header("Storage")]
        [Tooltip("Save id the player's climb is stored under. Stable across ladder " +
                 "versions — the ladder id lives inside the save, not in its name.")]
        [SerializeField] private string _saveId = LadderSavedData.DefaultSaveId;

        [Tooltip("Write to disk immediately after a claim instead of waiting for the next " +
                 "batch. Costs one flush at the moment a player would notice losing.")]
        [SerializeField] private bool _flushOnCheckpoint = true;

        [Header("Ladder selection")]
        [Tooltip("Pin a specific ladder id instead of picking the first registered one. For " +
                 "A/B testing or when several events coexist; leave blank otherwise.")]
        [SerializeField] private string _forcedLadderId;

        [Header("Diagnostics")]
        [Tooltip("Log every step report, claim and refresh. Noisy, but the practical way to " +
                 "diagnose a threshold or delivery issue on a device.")]
        [SerializeField] private bool _verboseLogging;

        /// <summary>
        /// Gets the save id progress is stored under.
        /// </summary>
        public string SaveId => string.IsNullOrWhiteSpace(_saveId)
            ? LadderSavedData.DefaultSaveId
            : _saveId;

        /// <summary>
        /// Indicates whether claims are flushed to disk immediately.
        /// </summary>
        public bool FlushOnCheckpoint => _flushOnCheckpoint;

        /// <summary>
        /// Gets the ladder id to force, or an empty string to select the first registered.
        /// </summary>
        public string ForcedLadderId => _forcedLadderId;

        /// <summary>
        /// Indicates whether verbose diagnostics are logged.
        /// </summary>
        public bool VerboseLogging => _verboseLogging;

        /// <summary>
        /// Reports settings that will not behave the way they read.
        /// </summary>
        /// <returns>A human-readable summary, or an empty string when the config is sound.</returns>
        public string DescribeProblems()
        {
            if (string.IsNullOrWhiteSpace(_forcedLadderId)) return string.Empty;

            return $"ladder '{_forcedLadderId}' is force-selected, so other registered " +
                   "ladders are ignored";
        }
    }
}
