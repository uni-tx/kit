using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Ads
{
    /// <summary>
    /// Static facade over the game's ad provider.
    /// </summary>
    /// <remarks>
    /// Pacing, overlap protection, consent gating and safe-area offsetting live here rather
    /// than in each adapter, so every provider behaves the same way and a new adapter cannot
    /// forget one of them.
    /// </remarks>
    public static class UniAds
    {
        private static readonly Dictionary<AdFormat, float> LastShownAt = new();

        private static IAdsProvider _provider;
        private static UniAdsConfig _config;
        private static bool _isShowing;
        private static bool _hasConsent = true;
        private static bool _hasShownAppOpen;

        /// <summary>
        /// Gets the active provider, or null when none is installed.
        /// </summary>
        public static IAdsProvider Provider => _provider;

        /// <summary>
        /// Gets the active configuration, or null before initialization.
        /// </summary>
        public static UniAdsConfig Config => _config;

        /// <summary>
        /// Indicates whether a full-screen ad is on screen right now.
        /// </summary>
        public static bool IsShowing => _isShowing;

        /// <summary>
        /// Indicates whether the player has consented to personalized ads.
        /// </summary>
        public static bool HasConsent => _hasConsent;

        /// <summary>
        /// Gets or sets the minimum seconds between interstitials.
        /// </summary>
        /// <remarks>
        /// Enforced centrally. Back-to-back interstitials are the fastest way to lose
        /// retention, and every call site remembering to check is not a realistic assumption.
        /// </remarks>
        public static float InterstitialCooldown { get; set; } = 45f;

        /// <summary>
        /// Gets or sets the minimum seconds between app-open ads.
        /// </summary>
        public static float AppOpenCooldown { get; set; } = 60f;

        /// <summary>
        /// Raised whenever a full-screen ad finishes, with its result.
        /// </summary>
        public static event Action<AdFormat, AdShowResult> OnAdClosed;

        /// <summary>
        /// Gets screen pixels per density-independent pixel.
        /// </summary>
        /// <remarks>
        /// <see cref="Screen.dpi"/> returns 0 on devices that do not report it, so the
        /// fallback is an explicit 1:1 rather than arithmetic on a bogus value. Clamping the
        /// *result* to at least 1 would silently mis-scale genuinely low-density screens,
        /// and dividing a clamped dpi by 160 would turn an unknown density into 0.00625 —
        /// a 160x placement error.
        /// </remarks>
        public static float PixelsPerDp => Screen.dpi > 0f ? Screen.dpi / 160f : 1f;

        /// <summary>
        /// Installs a provider and initializes it.
        /// </summary>
        /// <param name="provider">The adapter to install.</param>
        /// <param name="config">Ad unit ids and pacing. Falls back to Resources/UniAdsConfig.</param>
        /// <param name="cToken">Token to cancel initialization.</param>
        public static async UniTask InitializeAsync(IAdsProvider provider, UniAdsConfig config = null,
            CancellationToken cToken = default)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _config = config != null
                ? config
                : Resources.Load<UniAdsConfig>(UniAdsConfig.DefaultResourcePath);

            if (_config != null)
            {
                InterstitialCooldown = _config.InterstitialCooldown;
                AppOpenCooldown = _config.AppOpenCooldown;

                var missing = _config.DescribeMissingUnits();

                // Reported once at startup rather than per request: a blank id is a valid
                // choice (plenty of games ship without banners), but a *forgotten* one looks
                // identical to no-fill at the call site.
                if (!string.IsNullOrEmpty(missing))
                {
                    UniStatics.LogInfo($"No ad unit id configured for: {missing}.", null, Color.yellow);
                }

                if (_config.UseTestAds)
                {
                    UniStatics.LogWarning(
                        "Ads are in TEST mode — no real impressions will be served. This is " +
                        "forced in the editor and development builds.", null);
                }
            }
            else
            {
                UniStatics.LogWarning(
                    "No UniAdsConfig supplied and none found at Resources/UniAdsConfig; " +
                    "ad unit ids will be empty.", null);
            }

            try
            {
                await _provider.InitializeAsync(_config, cToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // A failed ad SDK must not block startup — the game is still playable
                // without ads, and IsReady simply keeps returning false.
                UniStatics.LogError($"Ads provider '{_provider.Name}' failed to initialize: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Removes the provider and clears pacing, consent and session state.
        /// </summary>
        public static void Reset()
        {
            _provider = null;
            _config = null;
            _isShowing = false;
            _hasShownAppOpen = false;
            _hasConsent = true;
            InterstitialCooldown = 45f;
            AppOpenCooldown = 60f;
            OnAdClosed = null;
            LastShownAt.Clear();
        }

        /// <remarks>
        /// With <b>Enter Play Mode Options ▸ Reload Domain</b> disabled — the default
        /// fast-enter setup — statics survive between play sessions. Without this, a
        /// cooldown timestamp, a withdrawn consent flag, or "the app-open ad already
        /// played" would carry into the next run and the second Play would behave
        /// differently from the first.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();

        /// <summary>
        /// Indicates whether the provider can serve a format at all.
        /// </summary>
        /// <param name="format">The format to check.</param>
        /// <remarks>
        /// Gate feature visibility on this. It is permanent, unlike <see cref="IsReady"/>.
        /// </remarks>
        public static bool Supports(AdFormat format) => _provider != null && _provider.Supports(format);

        /// <summary>
        /// Indicates whether a format is loaded and ready.
        /// </summary>
        /// <param name="format">The format to check.</param>
        public static bool IsReady(AdFormat format)
            => _provider != null && _provider.Supports(format) && _provider.IsReady(format);

        /// <summary>
        /// Preloads an ad.
        /// </summary>
        /// <param name="format">The format to load.</param>
        /// <param name="cToken">Token to cancel the load.</param>
        public static UniTask LoadAsync(AdFormat format, CancellationToken cToken = default)
            => _provider != null && _provider.Supports(format)
                ? _provider.LoadAsync(format, cToken)
                : UniTask.CompletedTask;

        /// <summary>
        /// Shows a rewarded ad and reports whether the reward was earned.
        /// </summary>
        /// <param name="placementName">Optional placement name, for reporting.</param>
        /// <param name="cToken">Token to cancel the request.</param>
        /// <remarks>
        /// Grant only when <see cref="AdShowResult.ShouldReward"/> is true, and persist the
        /// grant before the next frame — players background the app the instant an ad closes.
        /// </remarks>
        public static UniTask<AdShowResult> ShowRewardedAsync(string placementName = null,
            CancellationToken cToken = default)
            // Rewarded ads are opt-in, so interstitial pacing must not block them.
            => ShowFullScreenAsync(AdFormat.Rewarded, placementName, 0f, cToken);

        /// <summary>
        /// Shows an interstitial, respecting <see cref="InterstitialCooldown"/>.
        /// </summary>
        /// <param name="placementName">Optional placement name, for reporting.</param>
        /// <param name="cToken">Token to cancel the request.</param>
        public static UniTask<AdShowResult> ShowInterstitialAsync(string placementName = null,
            CancellationToken cToken = default)
            => ShowFullScreenAsync(AdFormat.Interstitial, placementName, InterstitialCooldown, cToken);

        /// <summary>
        /// Shows an app-open ad, if the provider supports one.
        /// </summary>
        /// <param name="placementName">Optional placement name, for reporting.</param>
        /// <param name="cToken">Token to cancel the request.</param>
        /// <remarks>
        /// <para>
        /// Returns <see cref="AdResult.Unsupported"/> on providers without the format —
        /// LevelPlay among them, whose ad units are rewarded, interstitial, banner and native.
        /// </para>
        /// <para>
        /// Skipped on first launch when configured: an ad before the player has seen the
        /// game is the single most uninstall-prone impression you can serve.
        /// </para>
        /// </remarks>
        public static async UniTask<AdShowResult> ShowAppOpenAsync(string placementName = null,
            CancellationToken cToken = default)
        {
            // Capability is checked before any pacing rule. A provider that can never serve
            // this format must say so, or the caller sees a generic failure, treats it as
            // no-fill, and retries forever.
            if (_provider == null) return AdShowResult.NotReady;

            if (!_provider.Supports(AdFormat.AppOpen))
            {
                return AdShowResult.Unsupported(AdFormat.AppOpen, _provider.Name);
            }

            if (_config != null && _config.SkipAppOpenOnFirstLaunch && !_hasShownAppOpen && IsFirstLaunch())
            {
                _hasShownAppOpen = true;
                return AdShowResult.Failed("Skipped on first launch.");
            }

            var result = await ShowFullScreenAsync(AdFormat.AppOpen, placementName, AppOpenCooldown, cToken);

            if (result.Result != AdResult.Unsupported) _hasShownAppOpen = true;

            return result;
        }

        /// <summary>
        /// Shows a banner.
        /// </summary>
        /// <param name="placement">Where to anchor it. Defaults to the configured position.</param>
        /// <param name="cToken">Token to cancel the request.</param>
        public static UniTask<AdShowResult> ShowBannerAsync(AdPlacement? placement = null,
            CancellationToken cToken = default)
            => ShowInlineAsync(AdFormat.Banner,
                placement ?? AdPlacement.At(_config?.DefaultBannerPosition ?? AdPosition.BottomCenter), cToken);

        /// <summary>
        /// Shows an MREC — a 300x250 inline unit for menus and results screens.
        /// </summary>
        /// <param name="placement">Where to anchor it. Defaults to the configured position.</param>
        /// <param name="cToken">Token to cancel the request.</param>
        public static UniTask<AdShowResult> ShowMrecAsync(AdPlacement? placement = null,
            CancellationToken cToken = default)
            => ShowInlineAsync(AdFormat.Mrec,
                placement ?? AdPlacement.At(_config?.DefaultMrecPosition ?? AdPosition.Center), cToken);

        /// <summary>
        /// Hides the banner without destroying it.
        /// </summary>
        public static void HideBanner() => _provider?.HideInline(AdFormat.Banner);

        /// <summary>
        /// Hides the MREC without destroying it.
        /// </summary>
        public static void HideMrec() => _provider?.HideInline(AdFormat.Mrec);

        /// <summary>
        /// Destroys an inline ad and releases its native view.
        /// </summary>
        /// <param name="format">Either <see cref="AdFormat.Banner"/> or <see cref="AdFormat.Mrec"/>.</param>
        public static void DestroyInline(AdFormat format) => _provider?.DestroyInline(format);

        /// <summary>
        /// Records or withdraws the player's personalized-ads consent.
        /// </summary>
        /// <param name="hasConsent">Whether the player consented.</param>
        public static void SetConsent(bool hasConsent)
        {
            _hasConsent = hasConsent;
            _provider?.SetConsent(hasConsent);
        }

        /// <summary>
        /// Gets the safe-area inset, in dp, for an inline ad at the given placement.
        /// </summary>
        /// <param name="placement">Where the ad is anchored.</param>
        /// <returns>The inset to push the ad clear of cutouts and the home indicator.</returns>
        /// <remarks>
        /// Only the edges the placement actually touches are inset, so a bottom-centre banner
        /// is not pushed inward by a landscape notch on the left.
        /// </remarks>
        public static Vector2 GetSafeAreaInsetDp(AdPlacement placement)
        {
            if (_config == null || !_config.RespectSafeArea) return Vector2.zero;

            var insets = UniSafeArea.Insets;

            if (insets.IsZero) return Vector2.zero;

            var (left, right, bottom, top) = insets.ToPixels(Screen.width, Screen.height);

            // dp, not pixels: the SDK positions native views in density-independent units.
            var density = PixelsPerDp;

            if (placement.IsCustom) return Vector2.zero;

            var horizontal = placement.Position switch
            {
                AdPosition.TopLeft or AdPosition.CenterLeft or AdPosition.BottomLeft => left,
                AdPosition.TopRight or AdPosition.CenterRight or AdPosition.BottomRight => right,
                _ => 0f,
            };

            var vertical = placement.Position switch
            {
                AdPosition.TopLeft or AdPosition.TopCenter or AdPosition.TopRight => top,
                AdPosition.BottomLeft or AdPosition.BottomCenter or AdPosition.BottomRight => bottom,
                _ => 0f,
            };

            return new Vector2(horizontal / density, vertical / density);
        }

        private static async UniTask<AdShowResult> ShowInlineAsync(AdFormat format, AdPlacement placement,
            CancellationToken cToken)
        {
            if (_provider == null) return AdShowResult.NotReady;

            if (!_provider.Supports(format)) return AdShowResult.Unsupported(format, _provider.Name);

            try
            {
                return await _provider.ShowInlineAsync(format, placement, GetSafeAreaInsetDp(placement), cToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                UniStatics.LogError($"Ads provider '{_provider.Name}' threw showing {format}: {ex.Message}", null);
                return AdShowResult.Failed(ex.Message);
            }
        }

        private static async UniTask<AdShowResult> ShowFullScreenAsync(AdFormat format, string placementName,
            float cooldown, CancellationToken cToken)
        {
            if (_provider == null) return AdShowResult.NotReady;

            if (!_provider.Supports(format)) return AdShowResult.Unsupported(format, _provider.Name);

            // Two overlapping requests is a real failure mode: a double-tapped button, or
            // gameplay and a UI screen both requesting on the same frame.
            if (_isShowing) return AdShowResult.Failed("Another ad is already showing.");

            if (IsOnCooldown(format, cooldown)) return AdShowResult.Failed("Placement is on cooldown.");

            if (!_provider.IsReady(format)) return AdShowResult.NotReady;

            _isShowing = true;

            try
            {
                var result = await _provider.ShowAsync(format, placementName, cToken);

                LastShownAt[format] = Time.realtimeSinceStartup;
                OnAdClosed.SafeInvoke(format, result);

                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                UniStatics.LogError($"Ads provider '{_provider.Name}' threw showing {format}: {ex.Message}", null);
                return AdShowResult.Failed(ex.Message);
            }
            finally
            {
                _isShowing = false;
            }
        }

        private static bool IsOnCooldown(AdFormat format, float cooldown)
        {
            if (cooldown <= 0f) return false;

            if (!LastShownAt.TryGetValue(format, out var lastShown)) return false;

            // realtimeSinceStartup, not Time.time: an ad pauses the game, so a timeScale-based
            // clock stops and the cooldown would never expire.
            return Time.realtimeSinceStartup - lastShown < cooldown;
        }

        /// <summary>
        /// PlayerPrefs key recording that the app has been launched before.
        /// </summary>
        /// <remarks>
        /// Exposed so tests and a "reset onboarding" debug menu can clear it. Deleting it
        /// makes the next launch look like a first install to the app-open gate.
        /// </remarks>
        public const string FirstLaunchKey = "UniTx.Ads.HasLaunched";

        private static bool IsFirstLaunch()
        {
            if (PlayerPrefs.GetInt(FirstLaunchKey, 0) == 1) return false;

            PlayerPrefs.SetInt(FirstLaunchKey, 1);
            PlayerPrefs.Save();
            return true;
        }
    }
}
