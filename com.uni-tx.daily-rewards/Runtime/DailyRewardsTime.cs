using System;

namespace UniTx.DailyRewards
{
    /// <summary>
    /// Day-boundary math shared by the calendar, the service and the snapshot.
    /// </summary>
    /// <remarks>
    /// Everything here is UTC and culture-invariant on purpose. A device set to a
    /// non-Gregorian calendar parses an ISO date differently under the current culture,
    /// and a reset that happens at "the same local time" in two time zones is two
    /// different resets — both surface as rewards that unlock at the wrong moment.
    /// </remarks>
    public static class DailyRewardsTime
    {
        /// <summary>
        /// Seconds in one day.
        /// </summary>
        public const long SecondsPerDay = 86400L;

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
        /// <param name="resetHourUtc">The hour of day (UTC) the calendar resets at, 0-23.</param>
        /// <remarks>
        /// Daily rewards reset on a fixed wall-clock boundary rather than 24 hours after the
        /// last claim. A rolling window would let a player drift the reset earlier every day
        /// until it lands whenever they play, which quietly doubles the intended daily
        /// ceiling. The hour is a product decision — midnight UTC is the default, but a game
        /// whose day starts at 9 a.m. local should say so.
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
    }
}
