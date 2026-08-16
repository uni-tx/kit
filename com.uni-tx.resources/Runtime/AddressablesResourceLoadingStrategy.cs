using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UniTx.Resources
{
    /// <summary>
    /// Default <see cref="IResourceLoadingStrategy"/>, backed by Addressables.
    /// </summary>
    /// <remarks>
    /// Handle-to-UniTask conversion comes from UniTask's own <c>UniTask.Addressables</c>
    /// assembly, which the package auto-enables when Addressables is installed. The kit
    /// previously carried a hand-written <c>IUniTaskSource</c> implementation of the same
    /// thing — one more pooled-task integration to keep correct for no benefit.
    /// </remarks>
    internal sealed class AddressablesLoadingStrategy : IResourceLoadingStrategy
    {
        private readonly Dictionary<Guid, AsyncOperationHandle> _groupHandles = new();
        private readonly Dictionary<object, AsyncOperationHandle> _assetHandles = new();

        /// <inheritdoc />
        public UniTask InitializeAsync(CancellationToken cToken = default)
            // autoReleaseHandle: the locator handle is not needed after startup, and holding
            // it just keeps the initialization operation alive for the session.
            => Addressables.InitializeAsync(true).ToUniTask(cancellationToken: cToken);

        /// <inheritdoc />
        public async UniTask ResetAsync(CancellationToken cToken = default)
        {
            foreach (var handle in _groupHandles.Values)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }

            foreach (var handle in _assetHandles.Values)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }

            _groupHandles.Clear();
            _assetHandles.Clear();

            // Deliberately NOT clearing the dependency cache here. That wipes downloaded
            // bundles, so the next launch re-downloads content the player already has — a
            // real cost on mobile data. Use ClearDownloadCacheAsync when that is actually
            // what you want.
            await UnityEngine.Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: cToken);
        }

        /// <inheritdoc />
        public async UniTask<TObject> LoadAssetAsync<TObject>(string key, IProgress<float> progress = null,
            CancellationToken cToken = default)
            where TObject : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Asset key cannot be null or empty.", nameof(key));
            }

            var handle = Addressables.LoadAssetAsync<TObject>(key);
            // autoReleaseWhenCanceled: without it a cancelled load leaks its handle and the
            // asset stays resident for the rest of the session.
            var asset = await handle.ToUniTask(progress, cancellationToken: cToken, autoReleaseWhenCanceled: true);

            if (asset != null) _assetHandles[asset] = handle;

            return asset;
        }

        /// <inheritdoc />
        public void DisposeAsset<TObject>(TObject asset)
            where TObject : UnityEngine.Object
        {
            if (asset == null) return;

            if (_assetHandles.Remove(asset, out var handle))
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return;
            }

            // Loaded through another route (an AssetReference, say) — release by asset.
            Addressables.Release(asset);
        }

        /// <inheritdoc />
        public async UniTask<AssetGroup<TObject>> LoadAssetGroupAsync<TObject>(IEnumerable<string> labels,
            IProgress<float> progress = null, CancellationToken cToken = default)
            where TObject : UnityEngine.Object
        {
            if (labels == null) throw new ArgumentNullException(nameof(labels));

            // Materialize once: the caller may hand us a LINQ query, and enumerating it twice
            // (validation, then Addressables) can yield different results.
            var keys = labels as IReadOnlyList<string> ?? new List<string>(labels);

            if (keys.Count == 0)
            {
                throw new ArgumentException("At least one label is required.", nameof(labels));
            }

            for (var i = 0; i < keys.Count; i++)
            {
                if (string.IsNullOrEmpty(keys[i]))
                {
                    throw new ArgumentException($"Label at index {i} is null or empty.", nameof(labels));
                }
            }

            var handle = Addressables.LoadAssetsAsync<TObject>(keys, null, Addressables.MergeMode.Union);
            var result = await handle.ToUniTask(progress, cancellationToken: cToken, autoReleaseWhenCanceled: true);

            var assetGroup = new AssetGroup<TObject>(result);
            _groupHandles[assetGroup.Id] = handle;

            return assetGroup;
        }

        /// <inheritdoc />
        public void DisposeAssetGroup<TObject>(AssetGroup<TObject> assetGroup)
            where TObject : UnityEngine.Object
        {
            if (assetGroup == null) return;

            if (!_groupHandles.Remove(assetGroup.Id, out var handle))
            {
                UniStatics.LogWarning(
                    $"Asset group '{assetGroup.Id}' is not tracked by this strategy — already disposed?", this);
                return;
            }

            assetGroup.Dispose();

            if (handle.IsValid()) Addressables.Release(handle);
        }

        /// <inheritdoc />
        public async UniTask<TComponent> CreateInstanceAsync<TComponent>(string key, Transform parent = null,
            IProgress<float> progress = null, CancellationToken cToken = default)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Instance key cannot be null or empty.", nameof(key));
            }

            var handle = Addressables.InstantiateAsync(key, parent);
            var instance = await handle.ToUniTask(progress, cancellationToken: cToken, autoReleaseWhenCanceled: true);

            var component = instance.GetComponent<TComponent>();

            if (component == null)
            {
                // Release before throwing, or the failed instantiation stays in the scene.
                Addressables.ReleaseInstance(instance);
                throw new MissingComponentException(
                    $"Prefab at key '{key}' has no {typeof(TComponent).Name} component.");
            }

            return component;
        }

        /// <inheritdoc />
        public bool DisposeInstance(GameObject instance)
            => instance != null && Addressables.ReleaseInstance(instance);

        /// <inheritdoc />
        public async UniTask<long> GetDownloadSizeAsync(IEnumerable<string> labels,
            CancellationToken cToken = default)
        {
            if (labels == null) throw new ArgumentNullException(nameof(labels));

            var keys = labels as IReadOnlyList<string> ?? new List<string>(labels);

            if (keys.Count == 0) return 0L;

            // Union so a key present in several labels is counted once, matching how the
            // group would actually be loaded.
            var handle = Addressables.GetDownloadSizeAsync((IEnumerable)keys);

            try
            {
                return await handle.ToUniTask(cancellationToken: cToken);
            }
            finally
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
        }

        /// <inheritdoc />
        public async UniTask PreloadAsync(IEnumerable<string> labels, IProgress<float> progress = null,
            CancellationToken cToken = default)
        {
            if (labels == null) throw new ArgumentNullException(nameof(labels));

            var keys = labels as IReadOnlyList<string> ?? new List<string>(labels);

            if (keys.Count == 0) return;

            var handle = Addressables.DownloadDependenciesAsync((IEnumerable)keys,
                Addressables.MergeMode.Union, false);

            try
            {
                await handle.ToUniTask(progress, cancellationToken: cToken);
            }
            finally
            {
                // autoRelease is false above so progress can be observed to completion;
                // releasing here keeps the bundles cached but drops the operation handle.
                if (handle.IsValid()) Addressables.Release(handle);
            }
        }

        /// <inheritdoc />
        public async UniTask ClearDownloadCacheAsync(CancellationToken cToken = default)
            => await Addressables.ClearDependencyCacheAsync((string)null, true)
                .ToUniTask(cancellationToken: cToken);
    }
}
