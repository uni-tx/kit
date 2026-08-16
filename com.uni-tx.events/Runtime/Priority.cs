namespace UniTx.Events
{
    /// <summary>
    /// Order in which listeners are invoked. Higher priority runs first.
    /// </summary>
    /// <remarks>
    /// Values are laid out so ascending numeric order *is* dispatch order, which lets the
    /// bus compare two <see cref="Priority"/> values directly instead of going through a
    /// lookup table. <see cref="Medium"/> stays 0 so an unspecified priority still means
    /// "middle of the pack".
    /// </remarks>
    public enum Priority
    {
        /// <summary>
        /// Runs first.
        /// </summary>
        Highest = -2,

        /// <summary>
        /// Runs before <see cref="Medium"/>.
        /// </summary>
        High = -1,

        /// <summary>
        /// The default when no priority is given.
        /// </summary>
        Medium = 0,

        /// <summary>
        /// Runs after <see cref="Medium"/>.
        /// </summary>
        Low = 1,

        /// <summary>
        /// Runs last.
        /// </summary>
        Lowest = 2,
    }
}
