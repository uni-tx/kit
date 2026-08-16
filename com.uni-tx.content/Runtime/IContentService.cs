using System.Collections.Generic;

namespace UniTx.Content
{
    /// <summary>
    /// Provides read access to registered content data objects by key or type.
    /// </summary>
    public interface IContentService
    {
        /// <summary>
        /// Retrieves the data object registered under the given key.
        /// </summary>
        /// <typeparam name="T">The data type to cast to.</typeparam>
        /// <param name="key">The unique key identifying the data object.</param>
        /// <returns>The data object of type <typeparamref name="T"/>.</returns>
        T GetData<T>(string key)
            where T : IData;

        /// <summary>
        /// Retrieves the data object registered under the given key, without throwing.
        /// </summary>
        /// <typeparam name="T">The data type to cast to.</typeparam>
        /// <param name="key">The unique key identifying the data object.</param>
        /// <param name="data">The matching data object, or <c>default</c>.</param>
        /// <returns><c>true</c> when a match of the requested type was found.</returns>
        bool TryGetData<T>(string key, out T data)
            where T : IData;

        /// <summary>
        /// Retrieves the data objects registered under the given keys.
        /// </summary>
        /// <typeparam name="T">The data type to cast to.</typeparam>
        /// <param name="keys">The keys identifying the data objects.</param>
        /// <returns>An enumerable of the requested data objects.</returns>
        IEnumerable<T> GetData<T>(IEnumerable<string> keys)
            where T : IData;

        /// <summary>
        /// Retrieves all data objects of the given type.
        /// </summary>
        /// <typeparam name="T">The data type to filter by.</typeparam>
        /// <returns>An enumerable of all matching data objects.</returns>
        IEnumerable<T> GetAllData<T>()
            where T : IData;
    }
}
