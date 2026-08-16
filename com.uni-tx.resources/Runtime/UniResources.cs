using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniTx.Resources
{
    /// <summary>
    /// Static facade for loading, instantiating and releasing assets.
    /// </summary>
    public static class UniResources
    {
        private static IResourceLoadingStrategy _strategy;

        /// <summary>
        /// Indicates whether a loading strategy has been installed.
        /// </summary>
        public static bool IsInitialized => _strategy != null;

        /// <summary>
        /// Initializes with the default Addressables-backed strategy.
        /// </summary>
        /// <param name="cToken">Token to cancel initialization.</param>
        public static UniTask InitializeAsync(CancellationToken cToken = default)
            => InitializeAsync(new AddressablesLoadingStrategy(), cToken);

        /// <summary>
        /// Initializes with a custom loading strategy.
        /// </summary>
        /// <param name="strategy">The strategy to install.</param>
        /// <param name="cToken">Token to cancel initialization.</param>
        public static UniTask InitializeAsync(IResourceLoadingStrategy strategy, CancellationToken cToken = default)
        {
            if (_strategy != null)
            {
                throw new InvalidOperationException(
                    "UniResources is already initialized. Call ResetAsync() before initializing again.");
            }

            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            return _strategy.InitializeAsync(cToken);
        }

        /// <summary>
        /// Releases every tracked asset and uninstalls the strategy.
        /// </summary>
        /// <param name="cToken">Token to cancel the reset.</param>
        public static async UniTask ResetAsync(CancellationToken cToken = default)
        {
            if (_strategy == null) return;

            var strategy = _strategy;
            // Clear first so a failure mid-reset cannot leave a half-torn-down strategy
            // installed and serving loads.
            _strategy = null;

            await strategy.ResetAsync(cToken);
        }

        /// <summary>
        /// Loads a single asset by key.
        /// </summary>
        /// <typeparam name="TObject">The asset type to load.</typeparam>
        /// <param name="key">The address of the asset.</param>
        /// <param name="progress">Optional progress reporter, 0..1.</param>
        /// <param name="cToken">Token to cancel the load.</param>
        public static UniTask<TObject> LoadAssetAsync<TObject>(string key, IProgress<float> progress = default,
            CancellationToken cToken = default)
            where TObject : UnityEngine.Object
            => Strategy.LoadAssetAsync<TObject>(key, progress, cToken);

        /// <summary>
        /// Releases a previously loaded asset.
        /// </summary>
        /// <typeparam name="TObject">The asset type to release.</typeparam>
        /// <param name="asset">The asset to release.</param>
        public static void DisposeAsset<TObject>(TObject asset)
            where TObject : UnityEngine.Object
            => Strategy.DisposeAsset(asset);

        /// <summary>
        /// Loads every asset carrying any of the given labels.
        /// </summary>
        /// <typeparam name="TObject">The asset type to load.</typeparam>
        /// <param name="labels">Labels to union.</param>
        /// <param name="progress">Optional progress reporter, 0..1.</param>
        /// <param name="cToken">Token to cancel the load.</param>
        public static UniTask<AssetGroup<TObject>> LoadAssetGroupAsync<TObject>(IEnumerable<string> labels,
            IProgress<float> progress = default, CancellationToken cToken = default)
            where TObject : UnityEngine.Object
            => Strategy.LoadAssetGroupAsync<TObject>(labels, progress, cToken);

        /// <summary>
        /// Releases a previously loaded asset group.
        /// </summary>
        /// <typeparam name="TObject">The asset type contained in the group.</typeparam>
        /// <param name="assetGroup">The group to release.</param>
        public static void DisposeAssetGroup<TObject>(AssetGroup<TObject> assetGroup)
            where TObject : UnityEngine.Object
            => Strategy.DisposeAssetGroup(assetGroup);

        /// <summary>
        /// Instantiates a prefab by key and returns one of its components.
        /// </summary>
        /// <typeparam name="TComponent">Component expected on the prefab root.</typeparam>
        /// <param name="key">The address of the prefab.</param>
        /// <param name="parent">Optional parent for the new instance.</param>
        /// <param name="progress">Optional progress reporter, 0..1.</param>
        /// <param name="cToken">Token to cancel the instantiation.</param>
        public static UniTask<TComponent> CreateInstanceAsync<TComponent>(string key, Transform parent = null,
            IProgress<float> progress = default, CancellationToken cToken = default)
            => Strategy.CreateInstanceAsync<TComponent>(key, parent, progress, cToken);

        /// <summary>
        /// Releases a previously created instance.
        /// </summary>
        /// <param name="instance">The instance to release.</param>
        /// <returns><c>true</c> when the instance was owned by the active strategy.</returns>
        public static bool DisposeInstance(GameObject instance) => Strategy.DisposeInstance(instance);

        /// <summary>
        /// Reports how many bytes must be downloaded before the given labels can be used.
        /// </summary>
        /// <param name="labels">Labels to measure.</param>
        /// <param name="cToken">Token to cancel the query.</param>
        /// <returns>Bytes still to download; zero when everything is already cached.</returns>
        public static UniTask<long> GetDownloadSizeAsync(IEnumerable<string> labels,
            CancellationToken cToken = default)
            => Strategy.GetDownloadSizeAsync(labels, cToken);

        /// <summary>
        /// Downloads and caches everything the given labels depend on.
        /// </summary>
        /// <param name="labels">Labels to pre-download.</param>
        /// <param name="progress">Optional progress reporter, 0..1.</param>
        /// <param name="cToken">Token to cancel the download.</param>
        public static UniTask PreloadAsync(IEnumerable<string> labels, IProgress<float> progress = default,
            CancellationToken cToken = default)
            => Strategy.PreloadAsync(labels, progress, cToken);

        /// <summary>
        /// Deletes cached downloaded content, forcing a re-download on next load.
        /// </summary>
        /// <param name="cToken">Token to cancel the operation.</param>
        public static UniTask ClearDownloadCacheAsync(CancellationToken cToken = default)
            => Strategy.ClearDownloadCacheAsync(cToken);

        /// <summary>
        /// Unloads assets no longer referenced by anything in memory.
        /// </summary>
        /// <param name="cToken">Token to cancel the operation.</param>
        public static UniTask UnloadUnusedAssetsAsync(CancellationToken cToken = default)
            => UnityEngine.Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: cToken);

        private static IResourceLoadingStrategy Strategy => _strategy
            ?? throw new InvalidOperationException(
                "UniResources is not initialized. Call UniResources.InitializeAsync() first — " +
                "UniTxStep does this during bootstrap.");
    }
}
