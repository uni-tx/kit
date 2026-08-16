using System;
using System.Runtime.CompilerServices;

namespace UniTx.Core
{
    /// <summary>
    /// Extension methods for <see cref="DateTime"/>
    /// </summary>
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Converts a <see cref="DateTime"/> object to a Unix timestamp.
        /// </summary>
        /// <param name="dateTime">The <see cref="DateTime"/> object to convert</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ToUnixTimestamp(this DateTime dateTime)
        {
            var timeSpan = dateTime.ToUniversalTime() - DateTime.UnixEpoch;
            return (long)timeSpan.TotalSeconds;
        }
    }
}