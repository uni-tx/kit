using System;

namespace UniTx.Content
{
    /// <summary>
    /// Base contract for all content data objects loaded from JSON.
    /// </summary>
    public interface IData
    {
        /// <summary>
        /// Gets the unique identifier of the data object.
        /// </summary>
        string Id { get; }
    }

    /// <summary>
    /// JSON wrapper used to deserialize an array of <typeparamref name="T"/> items.
    /// </summary>
    [Serializable]
    public class JsonArray<T>
    {
        /// <summary>
        /// The deserialized items.
        /// </summary>
        public T[] Items;
    }
}
