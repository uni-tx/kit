using UnityEngine;

namespace UniTx.Quests
{
    /// <summary>
    /// Quest policy, created via <c>Assets ▸ Create ▸ UniTx ▸ Quests ▸ Config</c>.
    /// </summary>
    /// <remarks>
    /// Policy lives here rather than in the quest JSON because it is a product decision
    /// that holds across every board — when the day and week reset, whether a claim is
    /// flushed to disk immediately — while the JSON is content a designer retunes per board.
    /// </remarks>
    [CreateAssetMenu(fileName = "UniQuestsConfig", menuName = "UniTx/Quests/Config")]
    public sealed class UniQuestsConfig : ScriptableObject
    {
        /// <summary>
        /// Resources path the service falls back to when no config is supplied.
        /// </summary>
        public const string DefaultResourcePath = "UniQuestsConfig";

        [Header("Storage")]
        [Tooltip("Save id the player's progress is stored under. Stable across board " +
                 "versions — the set id lives inside the save, not in its name.")]
        [SerializeField] private string _saveId = QuestsSavedData.DefaultSaveId;

        [Tooltip("Write to disk immediately after a claim instead of waiting for the next " +
                 "batch. Costs one flush at the moment a player would notice losing.")]
        [SerializeField] private bool _flushOnCheckpoint = true;

        [Header("Reset")]
        [Tooltip("Hour of day (UTC) daily and weekly quests reset at. 0 = UTC midnight; a " +
                 "game whose day starts at 9 a.m. local should set 9 (or whatever its server uses).")]
        [SerializeField, Range(0, 23)] private int _resetHourUtc;

        [Tooltip("Day of the week weekly quests reset on. 0 = Sunday, 6 = Saturday; " +
                 "1 = Monday is the common liveops choice.")]
        [SerializeField, Range(0, 6)] private int _weekStartDay = 1;

        [Header("Board selection")]
        [Tooltip("Pin a specific set id instead of picking the first registered one. For A/B " +
                 "testing or when several boards coexist; leave blank otherwise.")]
        [SerializeField] private string _forcedSetId;

        [Header("Diagnostics")]
        [Tooltip("Log every report, claim and refresh. Noisy, but the practical way to " +
                 "diagnose a reset-hour or prerequisite issue on a device.")]
        [SerializeField] private bool _verboseLogging;

        /// <summary>
        /// Gets the save id progress is stored under.
        /// </summary>
        public string SaveId => string.IsNullOrWhiteSpace(_saveId)
            ? QuestsSavedData.DefaultSaveId
            : _saveId;

        /// <summary>
        /// Indicates whether claims are flushed to disk immediately.
        /// </summary>
        public bool FlushOnCheckpoint => _flushOnCheckpoint;

        /// <summary>
        /// Gets the hour of day (UTC) quests reset at.
        /// </summary>
        public int ResetHourUtc => QuestTime.ClampHour(_resetHourUtc);

        /// <summary>
        /// Gets the day of the week weekly quests reset on.
        /// </summary>
        public int WeekStartDay => QuestTime.ClampWeekDay(_weekStartDay);

        /// <summary>
        /// Gets the set id to force, or an empty string to select the first registered.
        /// </summary>
        public string ForcedSetId => _forcedSetId;

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

            if (!string.IsNullOrWhiteSpace(_forcedSetId))
            {
                problems = Append(problems,
                    $"set '{_forcedSetId}' is force-selected, so other registered boards " +
                    "are ignored");
            }

            return problems;
        }

        private static string Append(string problems, string problem) =>
            problems.Length == 0 ? problem : $"{problems}; {problem}";
    }
}
