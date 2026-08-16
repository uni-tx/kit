using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Resources.Samples
{
    /// <summary>
    /// Loading single assets, label groups and prefab instances — and releasing all of them.
    /// </summary>
    /// <remarks>
    /// <b>Setup:</b> mark some assets Addressable and set the keys and label below to match.
    /// </remarks>
    public sealed class ResourceLoadingSample : MonoBehaviour
    {
        [SerializeField] private string _spriteKey = "Icons/Coin";
        [SerializeField] private string _prefabKey = "Enemies/Slime";
        [SerializeField] private string _label = "level-01";

        private CancellationTokenSource _cts;
        private Sprite _sprite;
        private AssetGroup<TextAsset> _levelData;
        private GameObject _spawned;

        private async void Start()
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            // UniTxStep does this during bootstrap.
            if (!UniResources.IsInitialized) await UniResources.InitializeAsync(_cts.Token);

            try
            {
                await LoadSingleAssetAsync();
                await LoadGroupByLabelAsync();
                await InstantiatePrefabAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected when the object is destroyed mid-load.
            }
        }

        private void OnDestroy()
        {
            _cts.SafeCancelAndDispose();

            // Release everything this object owns. Addressables reference-counts, so an
            // asset loaded and never released stays resident for the whole session.
            if (!UniResources.IsInitialized) return;

            if (_sprite != null) UniResources.DisposeAsset(_sprite);
            if (_levelData != null) UniResources.DisposeAssetGroup(_levelData);
            if (_spawned != null) UniResources.DisposeInstance(_spawned);
        }

        private async UniTask LoadSingleAssetAsync()
        {
            // IProgress drives a loading bar. Progress<T> allocates, so build it once here
            // rather than inside a per-frame or per-item path.
            var progress = new Progress<float>(p => Debug.Log($"Sprite {p:P0}"));

            _sprite = await UniResources.LoadAssetAsync<Sprite>(_spriteKey, progress, _cts.Token);

            Debug.Log($"Loaded sprite: {_sprite.name}");
        }

        private async UniTask LoadGroupByLabelAsync()
        {
            // One request for everything carrying a label — the usual "load this level's
            // content" shape, and far fewer round trips than one key at a time.
            _levelData = await UniResources.LoadAssetGroupAsync<TextAsset>(new[] { _label }, cToken: _cts.Token);

            Debug.Log($"Loaded {_levelData.Count} text asset(s) for '{_label}'");

            foreach (var file in _levelData)
            {
                Debug.Log($"  {file.name} ({file.text.Length} chars)");
            }
        }

        private async UniTask InstantiatePrefabAsync()
        {
            // Returns the requested component from the new instance, so there is no
            // GetComponent dance at the call site.
            var enemy = await UniResources.CreateInstanceAsync<Transform>(_prefabKey, transform,
                cToken: _cts.Token);

            _spawned = enemy.gameObject;
            Debug.Log($"Spawned {_spawned.name}");
        }

        /// <summary>
        /// Forces a re-download of cached content.
        /// </summary>
        /// <remarks>
        /// Deliberately separate from ResetAsync: a routine reset must not throw away
        /// bundles the player already spent mobile data downloading.
        /// </remarks>
        [ContextMenu("Clear Download Cache")]
        public void ClearDownloadCache() => UniResources.ClearDownloadCacheAsync(_cts.Token).Forget();
    }
}
