using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniTx.Content
{
    /// <summary>
    /// Binds content file names to the data types they deserialize into.
    /// </summary>
    public static class ContentRegistry
    {
        private static readonly Dictionary<string, IDataLoader> Loaders = new();

        /// <summary>
        /// Gets the file names currently registered.
        /// </summary>
        public static IReadOnlyCollection<string> RegisteredFiles => Loaders.Keys;

        /// <summary>
        /// Registers a type so files named <paramref name="fileName"/> deserialize into it.
        /// </summary>
        /// <typeparam name="T">The concrete data type implementing <see cref="IData"/>.</typeparam>
        /// <param name="fileName">The content file name, without extension.</param>
        public static void Register<T>(string fileName)
            where T : class, IData
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("Content file name cannot be null or empty.", nameof(fileName));
            }

            var type = typeof(T);

            if (type.IsInterface || type.IsAbstract)
            {
                throw new InvalidOperationException(
                    $"Cannot register '{type.Name}': JsonUtility needs a concrete, instantiable type.");
            }

            Loaders[fileName] = new DataLoader<T>();
        }

        /// <summary>
        /// Removes the registration for a file name.
        /// </summary>
        /// <param name="fileName">The content file name to unregister.</param>
        /// <returns><c>true</c> when a registration was removed.</returns>
        public static bool Unregister(string fileName)
            => fileName != null && Loaders.Remove(fileName);

        /// <summary>
        /// Removes every registration.
        /// </summary>
        public static void Clear() => Loaders.Clear();

        internal static IDataLoader GetLoader(string fileName)
            => fileName != null && Loaders.TryGetValue(fileName, out var loader) ? loader : null;

        /// <remarks>
        /// With <b>Enter Play Mode Options ▸ Reload Domain</b> disabled, this dictionary would
        /// otherwise keep registrations — and the types they close over — from the previous
        /// play session.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Loaders.Clear();
    }
}
