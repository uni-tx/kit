using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.Resources;
using UnityEngine;
using UnityEngine.UI;

namespace UniTx.SpriteLoader
{
    /// <summary>
    /// Loads an Addressables sprite into a uGUI <see cref="Image"/>.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("UniTx/Image Sprite Loader")]
    public sealed class ImageSpriteLoader : MonoBehaviour
    {
        [Tooltip("Addressable key, with {0}, {1}… substituted from the LoadSpriteAsync arguments.")]
        [SerializeField] private string _format = "{0}";

        [Tooltip("Resize the RectTransform to the sprite's native pixel size after loading.")]
        [SerializeField] private bool _setNativeSize = true;

        [Tooltip("Hide the Image until a sprite has loaded, avoiding a white placeholder flash.")]
        [SerializeField] private bool _hideWhileLoading = true;

        private Image _image;
        private Sprite _sprite;
        private CancellationTokenSource _loadCts;

        /// <summary>
        /// Gets the currently loaded sprite, or null.
        /// </summary>
        public Sprite Sprite => _sprite;

        /// <summary>
        /// Loads the sprite whose key is the format string filled with <paramref name="args"/>.
        /// </summary>
        /// <param name="args">Values substituted into the key format.</param>
        /// <param name="cToken">Token to cancel the load.</param>
        public async UniTask LoadSpriteAsync(string[] args, CancellationToken cToken = default)
            => await LoadKeyAsync(string.Format(_format, args ?? Array.Empty<string>()), cToken);

        /// <summary>
        /// Loads the sprite at an explicit Addressables key, ignoring the format string.
        /// </summary>
        /// <param name="key">The Addressables key of the sprite.</param>
        /// <param name="cToken">Token to cancel the load.</param>
        public async UniTask LoadKeyAsync(string key, CancellationToken cToken = default)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Sprite key cannot be empty.", nameof(key));

            EnsureImage();

            // Supersede any in-flight load. Without this, rapidly rebinding a list row races
            // two loads and the slower one wins, showing the wrong sprite.
            _loadCts.SafeCancelAndDispose();
            _loadCts = CancellationTokenSource.CreateLinkedTokenSource(
                cToken, this.GetCancellationTokenOnDestroy());

            var token = _loadCts.Token;

            if (_hideWhileLoading) _image.enabled = false;

            Sprite loaded;

            try
            {
                loaded = await UniResources.LoadAssetAsync<Sprite>(key, cToken: token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested)
            {
                UniResources.DisposeAsset(loaded);
                return;
            }

            // Release the outgoing sprite only once the new one is in hand, so the Image is
            // never pointed at a released asset. The previous version overwrote _sprite and
            // leaked the old handle on every reload.
            ReleaseSprite();

            _sprite = loaded;
            _image.sprite = _sprite;

            if (_setNativeSize) _image.SetNativeSize();

            _image.enabled = true;
        }

        /// <summary>
        /// Releases the loaded sprite and clears the image.
        /// </summary>
        public void UnloadSprite()
        {
            _loadCts.SafeCancelAndDispose();
            _loadCts = null;

            ReleaseSprite();

            EnsureImage();
            _image.sprite = null;
            _image.enabled = false;
        }

        private void ReleaseSprite()
        {
            if (_sprite == null) return;

            UniResources.DisposeAsset(_sprite);
            _sprite = null;
        }

        private void EnsureImage()
        {
            // LoadSpriteAsync is routinely called from another component's Awake, which can
            // run before this one's.
            if (_image == null) _image = GetComponent<Image>();
        }

        private void Awake() => EnsureImage();

        private void OnDestroy()
        {
            _loadCts.SafeCancelAndDispose();
            _loadCts = null;

            // Releasing on destroy means a screen torn down mid-load does not strand its
            // Addressables handle for the rest of the session.
            if (_sprite != null && UniResources.IsInitialized) UniResources.DisposeAsset(_sprite);

            _sprite = null;
        }
    }
}
