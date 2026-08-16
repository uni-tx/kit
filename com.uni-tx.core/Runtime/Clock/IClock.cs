using System;

namespace UniTx.Core
{
    /// <summary>
    /// Provides the current time, from the device or from a server.
    /// </summary>
    /// <remarks>
    /// Depend on this rather than <see cref="DateTime"/> directly. A player can move the
    /// device clock forward to skip a timer, so anything gated on real elapsed time —
    /// daily rewards, energy refills, timed offers — needs a source they cannot edit.
    /// See <see cref="ServerClock"/>.
    /// </remarks>
    public interface IClock
    {
        /// <summary>
        /// Gets the current UTC date and time.
        /// </summary>
        DateTime UtcNow { get; }

        /// <summary>
        /// Gets the current Unix timestamp in seconds.
        /// Represents the number of seconds elapsed since 1970-01-01T00:00:00Z.
        /// </summary>
        long UnixTimestampNow { get; }
    }
}