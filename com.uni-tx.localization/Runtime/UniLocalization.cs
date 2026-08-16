using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace UniTx.Localization
{
    /// <summary>
    /// Static facade over Unity's Localization package.
    /// </summary>
    /// <remarks>
    /// Deliberately thin. <c>com.unity.localization</c> is free, in the Unity registry, and
    /// already solves string and asset tables, pluralization, Smart Strings and XLIFF/CSV
    /// import — none of which is worth reimplementing. This adds only what the kit's
    /// conventions need: UniTask-shaped async, a keyed lookup that does not throw on a
    /// missing entry, and a locale-changed event that survives scene loads.
    /// </remarks>
    public static class UniLocalization
    {
        /// <summary>
        /// Raised after the active locale changes.
        /// </summary>
        public static event Action<Locale> OnLocaleChanged;

        /// <summary>
        /// Indicates whether the localization system has finished loading.
        /// </summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>
        /// Gets the active locale, or null before initialization.
        /// </summary>
        public static Locale CurrentLocale => LocalizationSettings.SelectedLocale;

        /// <summary>
        /// Gets the active locale's culture code, e.g. "en", or an empty string.
        /// </summary>
        public static string CurrentCode => CurrentLocale != null ? CurrentLocale.Identifier.Code : string.Empty;

        /// <summary>
        /// Gets every locale the project ships.
        /// </summary>
        public static IReadOnlyList<Locale> AvailableLocales =>
            (IReadOnlyList<Locale>)LocalizationSettings.AvailableLocales?.Locales ?? Array.Empty<Locale>();

        /// <summary>
        /// Waits for the localization system to finish loading.
        /// </summary>
        /// <param name="cToken">Token to cancel the wait.</param>
        /// <remarks>
        /// Await this in a loading step. Reading a localized string before the system is
        /// ready returns the key rather than the translation, which ships as visible
        /// placeholder text.
        /// </remarks>
        public static async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            await LocalizationSettings.InitializationOperation.ToUniTask(cancellationToken: cToken);

            IsInitialized = true;
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        }

        /// <summary>
        /// Releases the locale-changed subscription.
        /// </summary>
        public static void Reset()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
            OnLocaleChanged = null;
            IsInitialized = false;
        }

        /// <summary>
        /// Switches the active locale by culture code and waits for tables to reload.
        /// </summary>
        /// <param name="localeCode">Culture code, e.g. "en", "tr", "pt-BR".</param>
        /// <param name="cToken">Token to cancel the switch.</param>
        /// <returns><c>true</c> when a matching locale was found and applied.</returns>
        public static async UniTask<bool> SetLocaleAsync(string localeCode, CancellationToken cToken = default)
        {
            if (string.IsNullOrWhiteSpace(localeCode)) return false;

            var locale = LocalizationSettings.AvailableLocales?.GetLocale(new LocaleIdentifier(localeCode));

            if (locale == null)
            {
                UniStatics.LogWarning($"Locale '{localeCode}' is not in the project's available locales.", null);
                return false;
            }

            LocalizationSettings.SelectedLocale = locale;

            // Selecting a locale kicks off table reloads; returning before they finish means
            // the next lookup still serves the old language.
            await LocalizationSettings.InitializationOperation.ToUniTask(cancellationToken: cToken);
            return true;
        }

        /// <summary>
        /// Selects the locale that best matches the device language.
        /// </summary>
        /// <param name="cToken">Token to cancel the switch.</param>
        /// <returns><c>true</c> when a match was applied.</returns>
        public static UniTask<bool> SetDeviceLocaleAsync(CancellationToken cToken = default)
        {
            var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            return SetLocaleAsync(code, cToken);
        }

        /// <summary>
        /// Looks up a localized string.
        /// </summary>
        /// <param name="table">String table name.</param>
        /// <param name="key">Entry key within the table.</param>
        /// <param name="cToken">Token to cancel the lookup.</param>
        /// <returns>The translation, or the key itself when there is no entry.</returns>
        /// <remarks>
        /// Returns the key instead of throwing on a miss: a missing translation should show
        /// up as visible placeholder text in QA, not take down the screen that renders it.
        /// </remarks>
        public static async UniTask<string> GetStringAsync(string table, string key,
            CancellationToken cToken = default)
        {
            if (string.IsNullOrEmpty(table) || string.IsNullOrEmpty(key)) return key ?? string.Empty;

            try
            {
                var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, key);
                var value = await operation.ToUniTask(cancellationToken: cToken);

                return string.IsNullOrEmpty(value) ? key : value;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                UniStatics.LogWarning($"Localization lookup '{table}/{key}' failed: {ex.Message}", null);
                return key;
            }
        }

        /// <summary>
        /// Looks up a localized string and substitutes Smart String arguments.
        /// </summary>
        /// <param name="table">String table name.</param>
        /// <param name="key">Entry key within the table.</param>
        /// <param name="arguments">Smart String arguments.</param>
        /// <param name="cToken">Token to cancel the lookup.</param>
        public static async UniTask<string> GetStringAsync(string table, string key, object[] arguments,
            CancellationToken cToken = default)
        {
            if (string.IsNullOrEmpty(table) || string.IsNullOrEmpty(key)) return key ?? string.Empty;

            try
            {
                var operation = LocalizationSettings.StringDatabase
                    .GetLocalizedStringAsync(table, key, arguments);

                var value = await operation.ToUniTask(cancellationToken: cToken);

                return string.IsNullOrEmpty(value) ? key : value;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                UniStatics.LogWarning($"Localization lookup '{table}/{key}' failed: {ex.Message}", null);
                return key;
            }
        }

        /// <summary>
        /// Loads a localized asset, e.g. a locale-specific sprite or audio clip.
        /// </summary>
        /// <typeparam name="TObject">The asset type.</typeparam>
        /// <param name="table">Asset table name.</param>
        /// <param name="key">Entry key within the table.</param>
        /// <param name="cToken">Token to cancel the load.</param>
        public static async UniTask<TObject> GetAssetAsync<TObject>(string table, string key,
            CancellationToken cToken = default)
            where TObject : UnityEngine.Object
        {
            var operation = LocalizationSettings.AssetDatabase.GetLocalizedAssetAsync<TObject>(table, key);

            return await operation.ToUniTask(cancellationToken: cToken);
        }

        private static void HandleLocaleChanged(Locale locale) => OnLocaleChanged.SafeInvoke(locale);
    }
}
