using System;

namespace UniTx.Core
{
    /// <summary>
    /// A clock implementation that uses the local system time.
    /// </summary>
    public sealed class LocalClock : IClock
    {
        /// <inheritdoc />
        public DateTime UtcNow => DateTime.UtcNow;

        /// <inheritdoc />
        public long UnixTimestampNow => UtcNow.ToUnixTimestamp();
    }
}