namespace UniTx.Serialization
{
    /// <summary>
    /// A persistable data object identified by an id, which is also its file name.
    /// </summary>
    public interface ISavedData
    {
        /// <summary>
        /// Gets or sets the unique identifier, used as the save file name.
        /// </summary>
        /// <remarks>
        /// Settable because the service assigns it after loading. Previously the id was
        /// injected by synthesizing <c>{"_id":"…"}</c> JSON, which silently assumed every
        /// implementation had a serialized field named exactly <c>_id</c> — types that did
        /// not got an instance with a null <see cref="Id"/> that then failed to save.
        /// </remarks>
        string Id { get; set; }

        /// <summary>
        /// Gets or sets the Unix timestamp of the last successful write.
        /// </summary>
        long ModifiedTimestamp { get; set; }
    }
}
