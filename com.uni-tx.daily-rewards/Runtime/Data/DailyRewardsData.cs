using System;
using System.Collections.Generic;
using System.Text;
using UniTx.Content;
using UnityEngine;

namespace UniTx.DailyRewards
{
    /// <summary>
    /// One daily reward calendar's static definition, loaded as JSON content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Static because it is the same for every player: a balance patch replaces this file
    /// without touching a single save. Everything per-player lives in
    /// <see cref="DailyRewardsSavedData"/>, whose save key stays stable while this content
    /// key can be re-pointed at a newer calendar.
    /// </para>
    /// <para>
    /// The <see cref="Mode"/> decides what a missed day costs. In <see cref="DailyRewardsMode.Calendar"/>
    /// the position follows the wall clock and a missed day is simply skipped; in
    /// <see cref="DailyRewardsMode.Streak"/> it resets to day one, so the day-7 reward
    /// genuinely requires seven consecutive logins. <see cref="Loop"/> decides what happens
    /// after the last slot: wrap back to the first, or stop paying out entirely.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DailyRewardsData : IData
    {
        [Tooltip("Unique calendar id. Part of the recorded claim key, so changing it on a " +
                 "live calendar restarts every player's position.")]
        [SerializeField] private string _id;

        [Tooltip("Player-facing calendar name, or a localization key.")]
        [SerializeField] private string _displayName;

        [Tooltip("What a missed day costs: skip it (Calendar) or reset to day one (Streak).")]
        [SerializeField] private DailyRewardsMode _mode = DailyRewardsMode.Calendar;

        [Tooltip("Wrap back to day one after the last slot. Off makes this a one-time " +
                 "calendar that stops paying out once every slot is claimed.")]
        [SerializeField] private bool _loop = true;

        [Tooltip("The reward ladder, one slot per day. Slots are sorted on load, so the " +
                 "authoring order in the file does not matter.")]
        [SerializeField] private DailyRewardSlotData[] _slots;

        [NonSerialized] private bool _isPrepared;
        [NonSerialized] private DailyRewardSlotData[] _sortedSlots;

        /// <inheritdoc />
        public string Id => _id;

        /// <summary>
        /// Gets the player-facing calendar name or localization key.
        /// </summary>
        public string DisplayName => _displayName;

        /// <summary>
        /// Gets what a missed day costs.
        /// </summary>
        public DailyRewardsMode Mode => _mode;

        /// <summary>
        /// Indicates whether the calendar wraps back to day one after the last slot.
        /// </summary>
        public bool Loop => _loop;

        /// <summary>
        /// Gets the reward ladder, ordered by day number.
        /// </summary>
        public IReadOnlyList<DailyRewardSlotData> Slots
        {
            get
            {
                Prepare();
                return _sortedSlots;
            }
        }

        /// <summary>
        /// Gets how many slots the calendar has.
        /// </summary>
        public int SlotCount
        {
            get
            {
                Prepare();
                return _sortedSlots.Length;
            }
        }

        /// <summary>
        /// Returns the slot at the given 0-based index, or null.
        /// </summary>
        /// <param name="index">The 0-based slot index.</param>
        public DailyRewardSlotData GetSlot(int index)
        {
            Prepare();

            return index >= 0 && index < _sortedSlots.Length ? _sortedSlots[index] : null;
        }

        /// <summary>
        /// Reports authoring mistakes that would misbehave at runtime rather than fail loudly.
        /// </summary>
        /// <returns>A human-readable summary, or an empty string when the calendar is sound.</returns>
        /// <remarks>
        /// Content arrives as JSON a designer edits, so it is validated rather than trusted.
        /// These are the failures that would otherwise show up as a reward nobody can claim
        /// or a calendar that silently skips days.
        /// </remarks>
        public string DescribeProblems()
        {
            Prepare();

            var problems = new StringBuilder();

            if (string.IsNullOrWhiteSpace(_id)) Append(problems, "calendar id is blank");
            if (_sortedSlots.Length == 0) Append(problems, "no slots are defined");

            var seenDays = new HashSet<int>();
            var seenRewards = new HashSet<string>();

            foreach (var slot in _sortedSlots)
            {
                if (slot == null) continue;

                if (!seenDays.Add(slot.Day))
                {
                    // Two slots showing the same day number confuse a day-cell UI, and the
                    // sorted order is not the authoring order.
                    Append(problems, $"day {slot.Day} is defined more than once");
                }

                if (!seenRewards.Add(slot.RewardId))
                {
                    // Telemetry keys on the reward id, so a duplicate is indistinguishable
                    // from a re-claim of the first one.
                    Append(problems, $"reward id '{slot.RewardId}' appears more than once");
                }

                if (!slot.IsValid) Append(problems, $"day {slot.Day} has an incomplete reward");
            }

            return problems.ToString();
        }

        private static void Append(StringBuilder builder, string problem)
        {
            if (builder.Length > 0) builder.Append("; ");

            builder.Append(problem);
        }

        private void Prepare()
        {
            if (_isPrepared) return;

            _isPrepared = true;

            // Sorted once here rather than assumed: JSON is hand-edited, and the service
            // treats the array index as the calendar position.
            _sortedSlots = _slots == null
                ? Array.Empty<DailyRewardSlotData>()
                : (DailyRewardSlotData[])_slots.Clone();

            Array.Sort(_sortedSlots, static (left, right) =>
            {
                var byDay = left.Day.CompareTo(right.Day);
                return byDay != 0 ? byDay : string.CompareOrdinal(left.RewardId, right.RewardId);
            });
        }
    }
}
