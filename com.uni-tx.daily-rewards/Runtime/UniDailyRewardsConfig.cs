using UnityEngine;

namespace UniTx.DailyRewards
{
    /// <summary>
    /// Daily rewards policy, created via <c>Assets ▸ Create ▸ UniTx ▸ Daily Rewards ▸ Config</c>.
    /// </summary>
    /// <remarks>
    /// Policy lives here rather than in the calendar JSON because it is a product decision
    /// that holds across every calendar — when the day resets, whether a claim is flushed to
    /// disk immediately — while the JSON is content a designer retunes per calendar.
    /// </remarks>
    [CreateAssetMenu(fileName = "UniDailyRewardsConfig", menuName = "UniTx/Daily Rewards/Config")]
    public sealed class UniDailyRewardsConfig : ScriptableObject
    {
        /// <summary>
        /// Resources path the service falls back to when no config is supplied.
        /// </summary>
        public const string DefaultResourcePath = "UniDailyRewardsConfig";

        [Header("Storage")]
        [Tooltip("Save id the player's progress is stored under. Stable across calendar " +
                 "versions — the calendar id lives inside the save, not in its name.")]
        [SerializeField] private string _saveId = DailyRewardsSavedData.DefaultSaveId;

        [Tooltip("Write to disk immediately after a claim instead of waiting for the next " +
                 "batch. Costs one flush at the moment a player would notice losing.")]
        [SerializeField] private bool _flushOnCheckpoint = true;

        [Header("Reset")]
        [Tooltip("Hour of day (UTC) the calendar resets at. 0 = UTC midnight; a game whose " +
                 "day starts at 9 a.m. local should set 9 (or whatever its server uses).")]
        [SerializeField, Range(0, 23)] private int _resetHourUtc;

        [Header("Calendar selection")]
        [Tooltip("Pin a specific calendar id instead of picking the first registered one. " +
                 "For A/B testing or when several calendars coexist; leave blank otherwise.")]
        [SerializeField] private string _forcedCalendarId;

        [Header("Diagnostics")]
        [Tooltip("Log every claim and refresh. Noisy, but the practical way to diagnose a " +
                 "reset-hour or streak issue on a device.")]
        [SerializeField] private bool _verboseLogging;

        /// <summary>
        /// Gets the save id progress is stored under.
        /// </summary>
        public string SaveId => string.IsNullOrWhiteSpace(_saveId)
            ? DailyRewardsSavedData.DefaultSaveId
            : _saveId;

        /// <summary>
        /// Indicates whether claims are flushed to disk immediately.
        /// </summary>
        public bool FlushOnCheckpoint => _flushOnCheckpoint;

        /// <summary>
        /// Gets the hour of day (UTC) the calendar resets at.
        /// </summary>
        public int ResetHourUtc => DailyRewardsTime.ClampHour(_resetHourUtc);

        /// <summary>
        /// Gets the calendar id to force, or an empty string to select the first registered.
        /// </summary>
        public string ForcedCalendarId => _forcedCalendarId;

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

            if (!string.IsNullOrWhiteSpace(_forcedCalendarId))
            {
                problems = Append(problems,
                    $"calendar '{_forcedCalendarId}' is force-selected, so other registered " +
                    "calendars are ignored");
            }

            return problems;
        }

        private static string Append(string problems, string problem) =>
            problems.Length == 0 ? problem : $"{problems}; {problem}";
    }
}
