using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.Resources;
using UnityEngine;

namespace UniTx.Content
{
    /// <summary>
    /// Loads JSON content files through <see cref="UniResources"/> and serves the parsed data.
    /// </summary>
    public sealed class ContentService : IContentService, IContentLoader
    {
        private readonly Dictionary<string, IData> _dataRegistry = new();

        /// <summary>
        /// Gets how many data objects are currently loaded.
        /// </summary>
        public int Count => _dataRegistry.Count;

        /// <inheritdoc />
        public UniTask LoadContentAsync(IEnumerable<string> labels, CancellationToken cToken = default)
            => ProcessContentAsync(labels, new LoadStrategy(_dataRegistry), cToken);

        /// <inheritdoc />
        public UniTask UnloadContentAsync(IEnumerable<string> labels, CancellationToken cToken = default)
            => ProcessContentAsync(labels, new UnloadStrategy(_dataRegistry), cToken);

        /// <inheritdoc />
        public T GetData<T>(string key)
            where T : IData
            => TryGetData<T>(key, out var data)
                ? data
                : throw new KeyNotFoundException(
                    $"No content with id '{key}' of type {typeof(T).Name} is loaded. " +
                    "Load the label that contains it before querying.");

        /// <inheritdoc />
        public bool TryGetData<T>(string key, out T data)
            where T : IData
        {
            if (key != null && _dataRegistry.TryGetValue(key, out var found) && found is T typed)
            {
                data = typed;
                return true;
            }

            data = default;
            return false;
        }

        /// <inheritdoc />
        public IEnumerable<T> GetData<T>(IEnumerable<string> keys)
            where T : IData
            => keys == null ? Enumerable.Empty<T>() : keys.Select(GetData<T>);

        /// <inheritdoc />
        public IEnumerable<T> GetAllData<T>()
            where T : IData
            => _dataRegistry.Values.OfType<T>();

        /// <summary>
        /// Drops every loaded data object.
        /// </summary>
        public void Clear() => _dataRegistry.Clear();

        private async UniTask ProcessContentAsync(IEnumerable<string> labels, IProcessStrategy strategy,
            CancellationToken cToken = default)
        {
            var files = await UniResources.LoadAssetGroupAsync<TextAsset>(labels, cToken: cToken);

            try
            {
                foreach (var file in files)
                {
                    foreach (var data in ParseFile(file))
                    {
                        strategy.Process(data);
                    }
                }
            }
            finally
            {
                // In a finally block: a malformed content file used to leak the whole
                // Addressables group, keeping every TextAsset resident for the session.
                UniResources.DisposeAssetGroup(files);
            }
        }

        private static IEnumerable<IData> ParseFile(TextAsset file)
        {
            var loader = ContentRegistry.GetLoader(file.name);

            if (loader == null)
            {
                UniStatics.LogWarning(
                    $"Content file '{file.name}' is not registered against a type; skipping. " +
                    $"Call ContentRegistry.Register<T>(\"{file.name}\") during bootstrap.", null);
                return Enumerable.Empty<IData>();
            }

            // JsonUtility only deserializes an object at the root, so a top-level JSON array
            // has to be wrapped before parsing.
            return loader.Load($"{{\"Items\":{file.text}}}");
        }
    }
}
