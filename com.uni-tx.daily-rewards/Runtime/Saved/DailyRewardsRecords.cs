using System;
using UnityEngine;

namespace UniTx.DailyRewards
{
    /// <summary>
    /// One collected claim, kept in a capped archive.
    /// </summary>
    /// <remarks>
    /// Purely informational — a "this is what you collected and when" record for a history
    /// screen or a telemetry export. Nothing in the service reads it back to make a
    /// decision; the position and idempotency state live in dedicated fields on
    /// <see cref="DailyRewardsSavedData"/>.
    /// </remarks>
    [Serializable]
    public sealed class DailyClaimRecord
    {
        [SerializeField] private string _calendarId;
        [SerializeField] private int _day;
        [SerializeField] private int _slotIndex;
        [SerializeField] private long _dayStartUnix;
        [SerializeField] private int _streak;

        /// <summary>
        /// Gets the calendar the claim belongs to.
        /// </summary>
        public string CalendarId => _calendarId;

        /// <summary>
        /// Gets the 1-based day number that was claimed.
        /// </summary>
        public int Day => _day;

        /// <summary>
        /// Gets the 0-based slot index that was claimed.
        /// </summary>
        public int SlotIndex => _slotIndex;

        /// <summary>
        /// Gets the day boundary the claim belonged to.
        /// </summary>
        public long DayStartUnix => _dayStartUnix;

        /// <summary>
        /// Gets the streak after this claim.
        /// </summary>
        public int Streak => _streak;

        /// <summary>
        /// Creates a claim record.
        /// </summary>
        /// <param name="calendarId">The owning calendar id.</param>
        /// <param name="day">The 1-based day number.</param>
        /// <param name="slotIndex">The 0-based slot index.</param>
        /// <param name="dayStartUnix">The day boundary the claim belonged to.</param>
        /// <param name="streak">The streak after the claim.</param>
        public DailyClaimRecord(string calendarId, int day, int slotIndex, long dayStartUnix, int streak)
        {
            _calendarId = calendarId;
            _day = day;
            _slotIndex = slotIndex;
            _dayStartUnix = dayStartUnix;
            _streak = streak;
        }

        /// <summary>
        /// Parameterless constructor required by <c>JsonUtility</c>.
        /// </summary>
        public DailyClaimRecord()
        {
        }
    }
}
