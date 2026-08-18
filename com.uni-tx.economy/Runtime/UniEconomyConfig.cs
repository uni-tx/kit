using UnityEngine;

namespace UniTx.Economy
{
    /// <summary>
    /// Economy policy, created via <c>Assets ▸ Create ▸ UniTx ▸ Economy ▸ Config</c>.
    /// </summary>
    /// <remarks>
    /// Policy lives here rather than in the economy JSON because it is a product decision
    /// that holds across every economy — whether a mutation is flushed to disk immediately,
    /// which economy is shown first — while the JSON is content a designer retunes per season.
    /// </remarks>
    [CreateAssetMenu(fileName = "UniEconomyConfig", menuName = "UniTx/Economy/Config")]
    public sealed class UniEconomyConfig : ScriptableObject
    {
        /// <summary>
        /// Resources path the service falls back to when no config is supplied.
        /// </summary>
        public const string DefaultResourcePath = "UniEconomyConfig";

        [Header("Storage")]
        [Tooltip("Prefix of the save ids each economy's progress is stored under. The " +
                 "economy id is appended, so N economies never collide on disk.")]
        [SerializeField] private string _savePrefix = "economy:";

        [Tooltip("Write to disk immediately after an exchange or purchase instead of " +
                 "waiting for the next batch. Costs one flush at the moment a player would " +
                 "notice losing.")]
        [SerializeField] private bool _flushOnCheckpoint = true;

        [Header("Selection")]
        [Tooltip("Economy selected by default, so the facade has one before the UI asks. " +
                 "Leave blank to select the first registered economy.")]
        [SerializeField] private string _defaultEconomyId;

        [Header("Diagnostics")]
        [Tooltip("Log every exchange and purchase. Noisy, but the practical way to " +
                 "diagnose a ledger issue on a device.")]
        [SerializeField] private bool _verboseLogging;

        /// <summary>
        /// Gets the prefix of the save ids each economy's progress is stored under.
        /// </summary>
        public string SavePrefix => string.IsNullOrWhiteSpace(_savePrefix)
            ? "economy:"
            : _savePrefix;

        /// <summary>
        /// Indicates whether exchanges and purchases are flushed to disk immediately.
        /// </summary>
        public bool FlushOnCheckpoint => _flushOnCheckpoint;

        /// <summary>
        /// Gets the economy selected by default, or an empty string for the first registered.
        /// </summary>
        public string DefaultEconomyId => _defaultEconomyId;

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
            if (string.IsNullOrWhiteSpace(_defaultEconomyId)) return string.Empty;

            return $"economy '{_defaultEconomyId}' is the default, so it is selected on " +
                   "startup even when other economies are registered";
        }
    }
}
