namespace UniTx.Serialization
{
    /// <summary>
    /// Saves and loads <see cref="ISavedData"/> instances to persistent storage.
    /// </summary>
    public interface ISerialisationService
    {
        /// <summary>
        /// Queues the data for the next automatic save batch.
        /// </summary>
        /// <param name="data">The instance to persist.</param>
        /// <remarks>
        /// Batching means a value changed every frame costs one disk write per interval
        /// rather than one per frame. Call <see cref="Flush"/> when a write must land now.
        /// </remarks>
        void Save(ISavedData data);

        /// <summary>
        /// Loads the saved data with the given id, or a fresh instance if none exists.
        /// </summary>
        /// <typeparam name="T">The concrete saved-data type.</typeparam>
        /// <param name="id">The unique identifier of the entry.</param>
        /// <returns>The loaded instance, or a new one when no save file exists.</returns>
        T Load<T>(string id)
            where T : ISavedData, new();

        /// <summary>
        /// Writes every queued entry to disk immediately.
        /// </summary>
        /// <returns>How many entries were written.</returns>
        /// <remarks>
        /// Called automatically on pause and quit. Call it explicitly before a checkpoint an
        /// interrupted session must not lose — a purchase, or a level completion.
        /// </remarks>
        int Flush();

        /// <summary>
        /// Deletes the save file for the given id.
        /// </summary>
        /// <param name="id">The unique identifier of the entry.</param>
        void Delete(string id);
    }
}
