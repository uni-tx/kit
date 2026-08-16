using System;
using System.Globalization;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// Date handling shared by season definitions, quests and daily caps.
    /// </summary>
    /// <remarks>
    /// Everything here is UTC and culture-invariant on purpose. A season that started at
    /// "the same time" in two time zones is two different seasons, and a device set to a
    /// non-Gregorian calendar parses an ISO date differently under the current culture —
    /// both surface as content that mysteriously fails to load on a subset of devices.
    /// </remarks>
    public static class SeasonPassTime
    {
        private const long SecondsPerDay = 86400L;

        /// <summary>
        /// Parses an ISO 8601 UTC timestamp, returning null when blank or malformed.
        /// </summary>
        /// <param name="iso">The timestamp, e.g. <c>2026-09-01T00:00:00Z</c>.</param>
        /// <returns>The parsed UTC time, or null.</returns>
        public static DateTime? ParseUtc(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return null;

            var styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;

            return DateTime.TryParse(iso, CultureInfo.InvariantCulture, styles, out var parsed)
                ? parsed
                : null;
        }

        /// <summary>
        /// Converts a Unix timestamp in seconds to UTC.
        /// </summary>
        /// <param name="unixSeconds">Seconds since the epoch.</param>
        public static DateTime FromUnix(long unixSeconds) =>
            DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;

        /// <summary>
        /// Returns the Unix timestamp of the UTC midnight that starts the given moment's day.
        /// </summary>
        /// <param name="unixSeconds">Seconds since the epoch.</param>
        /// <remarks>
        /// Daily caps reset on a fixed wall-clock boundary rather than 24 hours after the last
        /// grant. A rolling window lets a player drift the reset earlier every day until it
        /// lands wherever they play, which quietly doubles the intended daily ceiling.
        /// </remarks>
        public static long StartOfUtcDay(long unixSeconds)
        {
            // Floor division: C# truncates toward zero, which would round pre-epoch values the
            // wrong way. Timestamps here are always positive, but the guard costs nothing.
            var days = unixSeconds >= 0
                ? unixSeconds / SecondsPerDay
                : (unixSeconds - SecondsPerDay + 1) / SecondsPerDay;

            return days * SecondsPerDay;
        }

        /// <summary>
        /// Returns the Unix timestamp of the UTC Monday midnight that starts the given moment's week.
        /// </summary>
        /// <param name="unixSeconds">Seconds since the epoch.</param>
        public static long StartOfUtcWeek(long unixSeconds)
        {
            var startOfDay = StartOfUtcDay(unixSeconds);
            var dayOfWeek = FromUnix(startOfDay).DayOfWeek;

            // DayOfWeek starts at Sunday; ISO weeks start on Monday, so Sunday is 6 days in.
            var daysSinceMonday = dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;

            return startOfDay - daysSinceMonday * SecondsPerDay;
        }
    }
}
