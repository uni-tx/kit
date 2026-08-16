using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace UniTx.Localization.Samples
{
    /// <summary>
    /// Switching locale at runtime and binding localized strings to UI.
    /// </summary>
    /// <remarks>
    /// <b>Setup:</b> create localization settings via
    /// <b>Edit ▸ Project Settings ▸ Localization</b>, add locales, and create a String Table
    /// Collection named to match <see cref="_tableName"/>.
    /// </remarks>
    public sealed class LocalizationSample : MonoBehaviour
    {
        [SerializeField] private string _tableName = "UI";
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _greetingLabel;

        private CancellationTokenSource _cts;

        private async void Start()
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            try
            {
                // Await this in a loading step. Reading a localized string before the system
                // is ready returns the key, which ships as visible placeholder text.
                await UniLocalization.InitializeAsync(_cts.Token);

                // Match the device language on first run. Persist the player's explicit
                // choice separately and prefer it on later launches.
                await UniLocalization.SetDeviceLocaleAsync(_cts.Token);

                Debug.Log($"Locale: {UniLocalization.CurrentCode} " +
                          $"({UniLocalization.AvailableLocales.Count} available)");

                // Rebind whenever the language changes, so open screens update in place
                // instead of only after a scene reload.
                UniLocalization.OnLocaleChanged += _ => RefreshLabelsAsync().Forget();

                await RefreshLabelsAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected when the object is destroyed during startup.
            }
        }

        private void OnDestroy()
        {
            _cts.SafeCancelAndDispose();
            UniLocalization.Reset();
        }

        private async UniTask RefreshLabelsAsync()
        {
            // Returns the key rather than throwing when an entry is missing, so a gap in the
            // translations shows up as visible placeholder text in QA instead of taking down
            // the screen that renders it.
            var title = await UniLocalization.GetStringAsync(_tableName, "menu_title", _cts.Token);

            // Smart String arguments — pluralization and interpolation are handled by the
            // Localization package rather than by string concatenation at the call site.
            var greeting = await UniLocalization.GetStringAsync(
                _tableName, "greeting_with_name", new object[] { "Ada" }, _cts.Token);

            if (_titleLabel != null) _titleLabel.text = title;
            if (_greetingLabel != null) _greetingLabel.text = greeting;

            Debug.Log($"{title} / {greeting}");
        }

        /// <summary>
        /// Switches to a specific language, e.g. from a settings dropdown.
        /// </summary>
        /// <param name="localeCode">Culture code such as "en", "tr" or "pt-BR".</param>
        public void SelectLanguage(string localeCode) => SelectLanguageAsync(localeCode).Forget();

        private async UniTaskVoid SelectLanguageAsync(string localeCode)
        {
            // Awaits the table reload, so the next lookup is guaranteed to be in the new
            // language rather than still serving the old one.
            var applied = await UniLocalization.SetLocaleAsync(localeCode, _cts.Token);

            if (!applied) Debug.LogWarning($"Locale '{localeCode}' is not available in this build.");
        }

        /// <summary>
        /// Loads a locale-specific asset, e.g. a translated title graphic.
        /// </summary>
        [ContextMenu("Load Localized Sprite")]
        public void LoadLocalizedSprite() => LoadLocalizedSpriteAsync().Forget();

        private async UniTaskVoid LoadLocalizedSpriteAsync()
        {
            var sprite = await UniLocalization.GetAssetAsync<Sprite>("Art", "title_logo", _cts.Token);

            Debug.Log($"Localized sprite: {(sprite == null ? "missing" : sprite.name)}");
        }
    }
}
