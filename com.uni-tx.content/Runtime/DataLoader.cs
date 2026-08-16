using System;
using System.Collections.Generic;
using System.Linq;

namespace UniTx.Content
{
    /// <summary>
    /// Deserializes a JSON array into a set of <see cref="IData"/> objects.
    /// </summary>
    internal interface IDataLoader
    {
        /// <summary>
        /// Parses the given JSON and returns the contained data objects.
        /// </summary>
        /// <param name="json">The JSON array to parse.</param>
        /// <returns>An enumerable of parsed data objects.</returns>
        IEnumerable<IData> Load(string json);
    }

    /// <summary>
    /// Typed <see cref="IDataLoader"/> that deserializes JSON arrays of <typeparamref name="TData"/>.
    /// </summary>
    internal sealed class DataLoader<TData> : IDataLoader
        where TData : IData
    {
        [Serializable]
        private class Wrapper
        {
            public TData[] Items;
        }

        public IEnumerable<IData> Load(string json)
        {
            var wrapper = UnityEngine.JsonUtility.FromJson<Wrapper>(json);
            return wrapper?.Items.Cast<IData>() ?? Array.Empty<IData>();
        }
    }
}
