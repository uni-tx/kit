using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Resources
{
    /// <summary>
    /// Loads, instantiates and releases assets. Swap the implementation to change backend.
    /// </summary>
    public interface IResourceLoadingStrategy : IInitializableAsync, IResettableAsync
    {
        /// <summary>
        /// Loads a single asset by key.
        /// </summary>
        /// <typeparam name="TObject">The asset type to load.</typeparam>
        /// <param name="key">The address of the asset.</param>
        /// <param name="progress">Optional progress reporter, 0..1.</param>
        /// <param name="cToken">Token to cancel the load.</param>
        UniTask<TObject> LoadAssetAsync<TObject>(string key, IProgress<float> progress = default,
            CancellationToken cToken = default)
            where TObject : UnityEngine.Object;

        /// <summary>
        /// Releases a previously loaded asset.
        /// </summary>
        /// <typeparam name="TObject">The asset type to release.</typeparam>
        /// <param name="asset">The asset to release.</param>
        void DisposeAsset<TObject>(TObject asset)
            where TObject : UnityEngine.Object;

        /// <summary>
        /// Loads every asset carrying any of the given labels.
        /// </summary>
        /// <typeparam name="TObject">The asset type to load.</typeparam>
        /// <param name="labels">Labels to union.</param>
        /// <param name="progress">Optional progress reporter, 0..1.</param>
        /// <param name="cToken">Token to cancel the load.</param>
        UniTask<AssetGroup<TObject>> LoadAssetGroupAsync<TObject>(IEnumerable<string> labels,
            IProgress<float> progress = default, CancellationToken cToken = default)
            where TObject : UnityEngine.Object;

        /// <summary>
        /// Releases a previously loaded asset group.
        /// </summary>
        /// <typeparam name="TObject">The asset type contained in the group.</typeparam>
        /// <param name="assetGroup">The group to release.</param>
        void DisposeAssetGroup<TObject>(AssetGroup<TObject> assetGroup)
            where TObject : UnityEngine.Object;

        /// <summary>
        /// Instantiates a prefab by key and returns one of its components.
        /// </summary>
        /// <typeparam name="TComponent">Component expected on the prefab root.</typeparam>
        /// <param name="key">The address of the prefab.</param>
        /// <param name="parent">Optional parent for the new instance.</param>
        /// <param name="progress">Optional progress reporter, 0..1.</param>
        /// <param name="cToken">Token to cancel the instantiation.</param>
        UniTask<TComponent> CreateInstanceAsync<TComponent>(string key, Transform parent = null,
            IProgress<float> progress = default, CancellationToken cToken = default);

        /// <summary>
        /// Releases a previously created instance.
        /// </summary>
        /// <param name="instance">The instance to release.</param>
        /// <returns><c>true</c> when the instance was owned by this strategy.</returns>
        bool DisposeInstance(GameObject instance);

        /// <summary>
        /// Reports how many bytes must be downloaded before the given labels can be used.
        /// </summary>
        /// <param name="labels">Labels to measure.</param>
        /// <param name="cToken">Token to cancel the query.</param>
        /// <returns>Bytes still to download; zero when everything is already cached.</returns>
        /// <remarks>
        /// Check this before loading remote content on mobile. Without it the first load
        /// silently stalls on a cellular connection with no progress and no way for the
        /// player to decline — and app stores treat unannounced large downloads as a
        /// review problem.
        /// </remarks>
        UniTask<long> GetDownloadSizeAsync(IEnumerable<string> labels, CancellationToken cToken = default);

        /// <summary>
        /// Downloads and caches everything the given labels depend on.
        /// </summary>
        /// <param name="labels">Labels to pre-download.</param>
        /// <param name="progress">Optional progress reporter, 0..1.</param>
        /// <param name="cToken">Token to cancel the download.</param>
        /// <remarks>
        /// Pair with <see cref="GetDownloadSizeAsync"/> to show a size prompt and a progress
        /// bar, so the wait is visible and interruptible rather than a frozen loading screen.
        /// </remarks>
        UniTask PreloadAsync(IEnumerable<string> labels, IProgress<float> progress = default,
            CancellationToken cToken = default);

        /// <summary>
        /// Deletes cached downloaded content, forcing a re-download on next load.
        /// </summary>
        /// <param name="cToken">Token to cancel the operation.</param>
        /// <remarks>
        /// Separate from <see cref="IResettableAsync.ResetAsync"/> on purpose: a routine
        /// reset must not throw away bundles the player already paid mobile data to
        /// download. Call this only for an explicit "clear cache" action or a forced
        /// content repair.
        /// </remarks>
        UniTask ClearDownloadCacheAsync(CancellationToken cToken = default);
    }
}
