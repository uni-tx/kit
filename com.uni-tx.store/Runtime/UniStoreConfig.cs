using UnityEngine;

namespace UniTx.Store
{
    /// <summary>
    /// Store policy, created via <c>Assets ▸ Create ▸ UniTx ▸ Store ▸ Config</c>.
    /// </summary>
    /// <remarks>
    /// Policy lives here rather than in the store JSON because it is a product decision
    /// that holds across every sale — whether a claim is flushed to disk immediately,
    /// which store is selected — while the JSON is content a designer retunes per sale.
    /// </remarks>
    [CreateAssetMenu(fileName = "UniStoreConfig", menuName = "UniTx/Store/Config")]
    public sealed class UniStoreConfig : ScriptableObject
    {
        /// <summary>
        /// Resources path the service falls back to when no config is supplied.
        /// </summary>
        public const string DefaultResourcePath = "UniStoreConfig";

        [Header("Storage")]
        [Tooltip("Save id the player's claims are stored under. Stable across store " +
                 "versions — the store id lives inside the save, not in its name.")]
        [SerializeField] private string _saveId = StoreSavedData.DefaultSaveId;

        [Tooltip("Write to disk immediately after a claim instead of waiting for the next " +
                 "batch. Costs one flush at the moment a player would notice losing.")]
        [SerializeField] private bool _flushOnCheckpoint = true;

        [Header("Store selection")]
        [Tooltip("Pin a specific store id instead of picking the first registered one. For " +
                 "A/B testing or when several shops coexist; leave blank otherwise.")]
        [SerializeField] private string _forcedStoreId;

        [Header("Diagnostics")]
        [Tooltip("Log every claim and refresh. Noisy, but the practical way to diagnose a " +
                 "delivery or cooldown issue on a device.")]
        [SerializeField] private bool _verboseLogging;

        /// <summary>
        /// Gets the save id progress is stored under.
        /// </summary>
        public string SaveId => string.IsNullOrWhiteSpace(_saveId)
            ? StoreSavedData.DefaultSaveId
            : _saveId;

        /// <summary>
        /// Indicates whether claims are flushed to disk immediately.
        /// </summary>
        public bool FlushOnCheckpoint => _flushOnCheckpoint;

        /// <summary>
        /// Gets the store id to force, or an empty string to select the first registered.
        /// </summary>
        public string ForcedStoreId => _forcedStoreId;

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
            if (string.IsNullOrWhiteSpace(_forcedStoreId)) return string.Empty;

            return $"store '{_forcedStoreId}' is force-selected, so other registered " +
                   "stores are ignored";
        }
    }
}
