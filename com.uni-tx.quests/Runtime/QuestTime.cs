using System;

namespace UniTx.Quests
{
    /// <summary>
    /// Period-boundary math shared by the service, the calculator and the snapshot.
    /// </summary>
    /// <remarks>
    /// Everything here is UTC and culture-invariant on purpose. A device set to a
    /// non-Gregorian calendar parses an ISO date differently under the current culture,
    /// and a reset that happens at "the same local time" in two time zones is two
    /// different resets — both surface as quests that wipe at the wrong moment.
    /// </remarks>
    public static class QuestTime
    {
        /// <summary>
        /// Seconds in one day.
        /// </summary>
        public const long SecondsPerDay = 86400L;

        /// <summary>
        /// Seconds in one week.
        /// </summary>
        public const long SecondsPerWeek = SecondsPerDay * 7L;

        /// <summary>
        /// Converts a Unix timestamp in seconds to UTC.
        /// </summary>
        /// <param name="unixSeconds">Seconds since the epoch.</param>
        public static DateTime FromUnix(long unixSeconds) =>
            DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;

        /// <summary>
        /// Returns the Unix timestamp of the boundary that starts the given moment's day,
        /// offset by a reset hour.
        /// </summary>
        /// <param name="unixSeconds">Seconds since the epoch.</param>
        /// <param name="resetHourUtc">The hour of day (UTC) the day resets at, 0-23.</param>
        /// <remarks>
        /// Quests reset on a fixed wall-clock boundary rather than 24 hours after the
        /// last report. A rolling window would let a player drift the reset earlier every
        /// day until it lands whenever they play, which quietly doubles the intended daily
        /// ceiling. The hour is a product decision — midnight UTC is the default.
        /// </remarks>
        public static long StartOfDay(long unixSeconds, int resetHourUtc)
        {
            var offset = ClampHour(resetHourUtc) * 3600L;
            var shifted = unixSeconds - offset;

            // Floor division: C# truncates toward zero, which would round pre-epoch values
            // the wrong way. Timestamps here are always positive, but the guard costs nothing.
            var days = shifted >= 0
                ? shifted / SecondsPerDay
                : (shifted - SecondsPerDay + 1) / SecondsPerDay;

            return days * SecondsPerDay + offset;
        }

        /// <summary>
        /// Returns the Unix timestamp of the boundary that starts the given moment's week,
        /// offset by a reset hour and a week-start day.
        /// </summary>
        /// <param name="unixSeconds">Seconds since the epoch.</param>
        /// <param name="resetHourUtc">The hour of day (UTC) the week resets at, 0-23.</param>
        /// <param name="weekStartDay">The day the week starts on, 0 (Sunday) to 6 (Saturday).</param>
        public static long StartOfWeek(long unixSeconds, int resetHourUtc, int weekStartDay)
        {
            var dayStart = StartOfDay(unixSeconds, resetHourUtc);

            // Unix epoch (1970-01-01) was a Thursday; the day-of-week offset counts forward
            // from that anchor so the mapping is culture-invariant.
            var dayOfWeek = (int)(dayStart / SecondsPerDay % 7 + 4) % 7;

            var diff = dayOfWeek - ClampWeekDay(weekStartDay);
            if (diff < 0) diff += 7;

            return dayStart - diff * SecondsPerDay;
        }

        /// <summary>
        /// Returns the period boundary a quest of the given cadence belongs to, or zero
        /// for one-time quests.
        /// </summary>
        /// <param name="reset">The quest's cadence.</param>
        /// <param name="unixSeconds">The observed time.</param>
        /// <param name="resetHourUtc">The reset hour, 0-23.</param>
        /// <param name="weekStartDay">The week-start day, 0-6.</param>
        public static long GetPeriodStart(QuestReset reset, long unixSeconds,
            int resetHourUtc, int weekStartDay) => reset switch
        {
            QuestReset.Daily => StartOfDay(unixSeconds, resetHourUtc),
            QuestReset.Weekly => StartOfWeek(unixSeconds, resetHourUtc, weekStartDay),
            _ => 0L,
        };

        /// <summary>
        /// Returns whole days between two day-start boundaries.
        /// </summary>
        /// <param name="fromDayStart">The earlier boundary.</param>
        /// <param name="toDayStart">The later boundary.</param>
        public static long DaysBetween(long fromDayStart, long toDayStart) =>
            (toDayStart - fromDayStart) / SecondsPerDay;

        /// <summary>
        /// Clamps a reset hour into the valid 0-23 range.
        /// </summary>
        /// <param name="hour">The configured hour.</param>
        public static int ClampHour(int hour) => Math.Clamp(hour, 0, 23);

        /// <summary>
        /// Clamps a week-start day into the valid 0-6 range.
        /// </summary>
        /// <param name="day">The configured day.</param>
        public static int ClampWeekDay(int day) => Math.Clamp(day, 0, 6);
    }
}
