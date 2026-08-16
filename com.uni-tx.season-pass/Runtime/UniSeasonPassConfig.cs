using UnityEngine;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// Season pass policy, created via <c>Assets ▸ Create ▸ UniTx ▸ Season Pass ▸ Config</c>.
    /// </summary>
    /// <remarks>
    /// Policy lives here rather than in the season JSON because it is a product decision that
    /// holds across every season — whether unclaimed rewards are forgiven, whether progress is
    /// allowed offline — while the JSON is content a designer retunes per season.
    /// </remarks>
    [CreateAssetMenu(fileName = "UniSeasonPassConfig", menuName = "UniTx/Season Pass/Config")]
    public sealed class UniSeasonPassConfig : ScriptableObject
    {
        /// <summary>
        /// Resources path the facade falls back to when no config is supplied.
        /// </summary>
        public const string DefaultResourcePath = "UniSeasonPassConfig";

        [Header("Storage")]
        [Tooltip("Save id the player's progress is stored under. Stable across seasons — the " +
                 "season id lives inside the save, not in its name.")]
        [SerializeField] private string _saveId = SeasonPassSavedData.DefaultSaveId;

        [Tooltip("Write to disk immediately after a claim, purchase or rollover instead of " +
                 "waiting for the next batch. Costs one flush at moments a player would " +
                 "notice losing.")]
        [SerializeField] private bool _flushOnCheckpoint = true;

        [Tooltip("How many finished seasons to keep in the archive. 0 keeps every one.")]
        [SerializeField, Min(0)] private int _maxArchiveEntries = 8;

        [Header("Season selection")]
        [Tooltip("Pin a specific season id instead of picking by date. For testing a season " +
                 "before its start date; leave blank to let the dates decide.")]
        [SerializeField] private string _forcedSeasonId;

        [Header("Expiry")]
        [Tooltip("What happens to rewards a player unlocked but never claimed when the " +
                 "season closes.")]
        [SerializeField] private SeasonExpiryPolicy _expiryPolicy = SeasonExpiryPolicy.AutoGrant;

        [Tooltip("Deliver every reward the moment its tier unlocks, with no claim tap. " +
                 "Removes the collect ritual most passes are built around, so it is off by " +
                 "default.")]
        [SerializeField] private bool _autoClaim;

        [Header("Offline")]
        [Tooltip("Apply XP locally while the backend is unreachable and replay it on the next " +
                 "sync. Turning this off makes progression require connectivity.")]
        [SerializeField] private bool _allowOfflineGrants = true;

        [Tooltip("Sync with the backend on every refresh. Off means the game syncs explicitly.")]
        [SerializeField] private bool _syncOnRefresh = true;

        [Header("Diagnostics")]
        [Tooltip("Log every grant, claim and phase change. Noisy, but the practical way to " +
                 "diagnose a rollover on a device.")]
        [SerializeField] private bool _verboseLogging;

        /// <summary>
        /// Gets the save id progress is stored under.
        /// </summary>
        public string SaveId => string.IsNullOrWhiteSpace(_saveId)
            ? SeasonPassSavedData.DefaultSaveId
            : _saveId;

        /// <summary>
        /// Indicates whether checkpoints are flushed to disk immediately.
        /// </summary>
        public bool FlushOnCheckpoint => _flushOnCheckpoint;

        /// <summary>
        /// Gets how many finished seasons are kept, or zero for all of them.
        /// </summary>
        public int MaxArchiveEntries => _maxArchiveEntries;

        /// <summary>
        /// Gets the season id to force, or an empty string to select by date.
        /// </summary>
        public string ForcedSeasonId => _forcedSeasonId;

        /// <summary>
        /// Gets what happens to unclaimed rewards when a season closes.
        /// </summary>
        public SeasonExpiryPolicy ExpiryPolicy => _expiryPolicy;

        /// <summary>
        /// Indicates whether rewards are delivered without a claim tap.
        /// </summary>
        public bool AutoClaim => _autoClaim;

        /// <summary>
        /// Indicates whether XP may be earned while the backend is unreachable.
        /// </summary>
        public bool AllowOfflineGrants => _allowOfflineGrants;

        /// <summary>
        /// Indicates whether every refresh also syncs with the backend.
        /// </summary>
        public bool SyncOnRefresh => _syncOnRefresh;

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
            var problems = string.Empty;

            if (_expiryPolicy == SeasonExpiryPolicy.GraceWindow && _autoClaim)
            {
                problems = Append(problems,
                    "auto-claim leaves nothing for the grace window to protect");
            }

            if (!string.IsNullOrWhiteSpace(_forcedSeasonId))
            {
                problems = Append(problems,
                    $"season '{_forcedSeasonId}' is force-selected, so live season dates are ignored");
            }

            if (!_allowOfflineGrants && !_syncOnRefresh)
            {
                problems = Append(problems,
                    "offline grants are disabled and refresh never syncs, so XP earned while " +
                    "disconnected is lost");
            }

            return problems;
        }

        private static string Append(string problems, string problem) =>
            problems.Length == 0 ? problem : $"{problems}; {problem}";
    }
}
