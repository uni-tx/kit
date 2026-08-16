using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.Resources;
using UniTx.SpriteLoader;
using UnityEngine;
using UnityEngine.UI;

namespace UniTx.SpriteLoader.Samples
{
    /// <summary>
    /// Loading Addressable sprites into uGUI images, including a recycled list row.
    /// </summary>
    /// <remarks>
    /// <b>Setup:</b> add an <see cref="ImageSpriteLoader"/> to a GameObject with an
    /// <see cref="Image"/>, and set its Format to something like <c>Icons/{0}</c>.
    /// </remarks>
    public sealed class SpriteLoadingSample : MonoBehaviour
    {
        [SerializeField] private ImageSpriteLoader _iconLoader;
        [SerializeField] private string[] _itemIds = { "sword", "shield", "potion" };

        private CancellationTokenSource _cts;
        private int _index;

        private void Awake()
            => _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

        private async void Start()
        {
            if (!UniResources.IsInitialized) await UniResources.InitializeAsync(_cts.Token);

            await ShowNextAsync();
        }

        private void OnDestroy() => _cts.SafeCancelAndDispose();

        /// <summary>
        /// Cycles to the next icon.
        /// </summary>
        /// <remarks>
        /// Calling this faster than sprites load is safe: each call cancels the in-flight
        /// load, so a slower earlier request cannot land after a newer one and leave the
        /// wrong icon showing — the classic recycled-list-row bug. The previous sprite is
        /// released only once the new one has arrived, so the Image never points at a
        /// released asset.
        /// </remarks>
        [ContextMenu("Show Next Icon")]
        public void ShowNext() => ShowNextAsync().Forget();

        private async UniTask ShowNextAsync()
        {
            if (_iconLoader == null || _itemIds.Length == 0) return;

            var id = _itemIds[_index++ % _itemIds.Length];

            try
            {
                // Fills the loader's Format string: "Icons/{0}" + "sword" -> "Icons/sword".
                await _iconLoader.LoadSpriteAsync(new[] { id }, _cts.Token);

                Debug.Log($"Showing {id}");
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer request, or the object was destroyed.
            }
        }

        /// <summary>
        /// Loads by an explicit key, bypassing the format string.
        /// </summary>
        [ContextMenu("Load Explicit Key")]
        public void LoadExplicitKey()
            => _iconLoader.LoadKeyAsync("Icons/legendary_sword", _cts.Token).Forget();

        /// <summary>
        /// Releases the sprite and hides the image, e.g. when a list row scrolls out of view.
        /// </summary>
        [ContextMenu("Unload")]
        public void Unload() => _iconLoader.UnloadSprite();
    }
}
