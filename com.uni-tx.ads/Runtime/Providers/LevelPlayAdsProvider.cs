#if UNITX_LEVELPLAY
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace UniTx.Ads.Providers
{
    /// <summary>
    /// <see cref="IAdsProvider"/> backed by Unity LevelPlay (formerly ironSource).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lives in its own assembly, constrained to <c>UNITX_LEVELPLAY</c>, so the whole thing
    /// is skipped when <c>com.unity.services.levelplay</c> is absent. The ads package itself
    /// takes no SDK dependency — a project on a different network pays nothing for this.
    /// </para>
    /// <para>
    /// Written against the 9.x API (<c>Unity.Services.LevelPlay</c>). Note the namespace: the
    /// published 8.x docs still say <c>com.unity3d.mediation</c>, which no longer exists.
    /// </para>
    /// <para>
    /// <b>LevelPlay has no app-open format.</b> Its ad units are rewarded, interstitial,
    /// banner and native, so <see cref="Supports"/> returns false for
    /// <see cref="AdFormat.AppOpen"/> and the facade reports it unsupported rather than
    /// leaving callers waiting on an ad that cannot arrive.
    /// </para>
    /// </remarks>
    public sealed class LevelPlayAdsProvider : IAdsProvider
    {
        private readonly Dictionary<AdFormat, LevelPlayBannerAd> _inlineAds = new();

        private LevelPlayInterstitialAd _interstitial;
        private LevelPlayRewardedAd _rewarded;
        private UniAdsConfig _config;
        private UniTaskCompletionSource<AdShowResult> _pendingShow;
        private UniTaskCompletionSource<bool> _initialization;

        /// <inheritdoc />
        public string Name => "LevelPlay";

        /// <inheritdoc />
        public bool Supports(AdFormat format) => format switch
        {
            AdFormat.Interstitial or AdFormat.Rewarded or AdFormat.Banner or AdFormat.Mrec => true,

            // Deliberate. LevelPlay's ad unit formats are rewarded, interstitial, banner and
            // native — there is no app-open unit. Reporting false lets the game hide the
            // feature instead of polling for an ad that can never arrive.
            AdFormat.AppOpen => false,

            _ => false,
        };

        /// <inheritdoc />
        public async UniTask InitializeAsync(UniAdsConfig config, CancellationToken cToken = default)
        {
            _config = config;

            var appKey = config?.AppKey;

            if (string.IsNullOrWhiteSpace(appKey))
            {
                UniStatics.LogError(
                    "No LevelPlay app key configured for this platform. Set it on the " +
                    "UniAdsConfig asset — ads will not serve without it.", this);
                return;
            }

            _initialization = new UniTaskCompletionSource<bool>();

            LevelPlay.OnInitSuccess += HandleInitSuccess;
            LevelPlay.OnInitFailed += HandleInitFailed;

            if (config.VerboseLogging) LevelPlay.SetAdaptersDebug(true);

            LevelPlay.Init(appKey);

            var initialized = await _initialization.Task.AttachExternalCancellation(cToken);

            if (!initialized) return;

            if (config.UseTestAds)
            {
                // ValidateIntegration writes an adapter/manifest report to the device log —
                // the fastest way to find a misconfigured network before blaming no-fill.
                LevelPlay.ValidateIntegration();

                UniStatics.LogWarning(
                    "LevelPlay is in TEST mode. Register this device on the LevelPlay " +
                    "dashboard, or call LaunchTestSuite() to verify placements. Testing " +
                    "against live ad units generates invalid traffic.", this);
            }

            CreateAdUnits();
        }

        /// <summary>
        /// Opens LevelPlay's on-device test suite for verifying placements and adapters.
        /// </summary>
        /// <remarks>
        /// Only meaningful on a device registered as a test device in the dashboard.
        /// </remarks>
        public void LaunchTestSuite() => LevelPlay.LaunchTestSuite();

        /// <inheritdoc />
        public bool IsReady(AdFormat format) => format switch
        {
            AdFormat.Interstitial => _interstitial?.IsAdReady() ?? false,
            AdFormat.Rewarded => _rewarded?.IsAdReady() ?? false,

            // Banners have no readiness query in the SDK; existence of the ad object is the
            // only signal available, and ShowAd on a not-yet-filled banner is a no-op.
            AdFormat.Banner or AdFormat.Mrec => _inlineAds.ContainsKey(format),

            _ => false,
        };

        /// <inheritdoc />
        public UniTask LoadAsync(AdFormat format, CancellationToken cToken = default)
        {
            switch (format)
            {
                case AdFormat.Interstitial:
                    _interstitial?.LoadAd();
                    break;
                case AdFormat.Rewarded:
                    _rewarded?.LoadAd();
                    break;
                case AdFormat.Banner:
                case AdFormat.Mrec:
                    GetOrCreateInline(format, AdPlacement.Default, Vector2.zero)?.LoadAd();
                    break;
            }

            // Not awaited by design: LevelPlay reports fill through its own events, and
            // awaiting here would duplicate that state machine for no benefit.
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public UniTask<AdShowResult> ShowAsync(AdFormat format, string placementName = null,
            CancellationToken cToken = default)
        {
            if (format is not (AdFormat.Interstitial or AdFormat.Rewarded))
            {
                return UniTask.FromResult(AdShowResult.Unsupported(format, Name));
            }

            // Ad SDKs are callback-based; a completion source bridges their callbacks into an
            // awaitable so calling code never writes a callback chain.
            _pendingShow = new UniTaskCompletionSource<AdShowResult>();

            if (format == AdFormat.Interstitial) _interstitial?.ShowAd(placementName);
            else _rewarded?.ShowAd(placementName);

            return _pendingShow.Task.AttachExternalCancellation(cToken);
        }

        /// <inheritdoc />
        public UniTask<AdShowResult> ShowInlineAsync(AdFormat format, AdPlacement placement,
            Vector2 safeAreaInsetDp, CancellationToken cToken = default)
        {
            if (format is not (AdFormat.Banner or AdFormat.Mrec))
            {
                return UniTask.FromResult(AdShowResult.Unsupported(format, Name));
            }

            var ad = GetOrCreateInline(format, placement, safeAreaInsetDp);

            if (ad == null)
            {
                return UniTask.FromResult(AdShowResult.Failed($"No {format} ad unit id configured."));
            }

            ad.LoadAd();
            ad.ShowAd();

            return UniTask.FromResult(AdShowResult.Completed);
        }

        /// <inheritdoc />
        public void HideInline(AdFormat format)
        {
            if (_inlineAds.TryGetValue(format, out var ad)) ad.HideAd();
        }

        /// <inheritdoc />
        public void DestroyInline(AdFormat format)
        {
            if (!_inlineAds.Remove(format, out var ad)) return;

            // Destroy, not just hide: a live banner keeps auto-refreshing and burning
            // impressions against a screen the player already left.
            ad.DestroyAd();
            ad.Dispose();
        }

        /// <inheritdoc />
        public void SetConsent(bool hasConsent) => LevelPlay.SetConsent(hasConsent);

        private void CreateAdUnits()
        {
            var units = _config.Units;

            if (!string.IsNullOrWhiteSpace(units.Interstitial))
            {
                _interstitial = new LevelPlayInterstitialAd(units.Interstitial);
                _interstitial.OnAdClosed += _ => Complete(AdShowResult.Completed);
                _interstitial.OnAdDisplayFailed += (_, error) => Complete(AdShowResult.Failed(error.ErrorMessage));
                _interstitial.OnAdLoadFailed += error => LogLoadFailure(AdFormat.Interstitial, error);
                _interstitial.LoadAd();
            }

            if (!string.IsNullOrWhiteSpace(units.Rewarded))
            {
                _rewarded = new LevelPlayRewardedAd(units.Rewarded);

                // OnAdRewarded is the only signal the reward was earned. OnAdClosed fires for
                // a skip too, so completing there would pay players who dismissed early —
                // Complete uses TrySetResult so the reward wins the race.
                _rewarded.OnAdRewarded += (_, _) => Complete(AdShowResult.Completed);
                _rewarded.OnAdClosed += _ => Complete(AdShowResult.Skipped);
                _rewarded.OnAdDisplayFailed += (_, error) => Complete(AdShowResult.Failed(error.ErrorMessage));
                _rewarded.OnAdLoadFailed += error => LogLoadFailure(AdFormat.Rewarded, error);
                _rewarded.LoadAd();
            }
        }

        private LevelPlayBannerAd GetOrCreateInline(AdFormat format, AdPlacement placement,
            Vector2 safeAreaInsetDp)
        {
            if (_inlineAds.TryGetValue(format, out var existing)) return existing;

            var adUnitId = _config?.GetUnitId(format);

            if (string.IsNullOrWhiteSpace(adUnitId)) return null;

            var size = format == AdFormat.Mrec
                // An MREC is a banner ad unit at 300x250 in LevelPlay, not a distinct format.
                ? LevelPlayAdSize.MEDIUM_RECTANGLE
                : LevelPlayAdSize.CreateAdaptiveAdSize();

            var config = new LevelPlayBannerAd.Config.Builder()
                .SetSize(size)
                .SetPosition(ToLevelPlayPosition(placement, safeAreaInsetDp))
                .SetDisplayOnLoad(true)
                // Android-only in the SDK, which is why the facade also computes an explicit
                // inset — otherwise an iOS bottom banner sits under the home indicator.
                .SetRespectSafeArea(_config?.RespectSafeArea ?? true)
                .Build();

            var ad = new LevelPlayBannerAd(adUnitId, config);
            ad.OnAdLoadFailed += error => LogLoadFailure(format, error);
            _inlineAds[format] = ad;

            return ad;
        }

        private static LevelPlayBannerPosition ToLevelPlayPosition(AdPlacement placement, Vector2 safeAreaInsetDp)
        {
            if (placement.IsCustom) return new LevelPlayBannerPosition(placement.Offset);

            // A non-zero inset has to become a custom coordinate: the nine named anchors sit
            // flush against the screen edge with no way to offset them.
            if (safeAreaInsetDp != Vector2.zero)
            {
                return new LevelPlayBannerPosition(OffsetFor(placement.Position, safeAreaInsetDp));
            }

            // LevelPlayBannerPosition is a class, not an enum, so these are object references
            // and cannot be used as switch case labels.
            return placement.Position switch
            {
                AdPosition.TopLeft => LevelPlayBannerPosition.TopLeft,
                AdPosition.TopCenter => LevelPlayBannerPosition.TopCenter,
                AdPosition.TopRight => LevelPlayBannerPosition.TopRight,
                AdPosition.CenterLeft => LevelPlayBannerPosition.CenterLeft,
                AdPosition.Center => LevelPlayBannerPosition.Center,
                AdPosition.CenterRight => LevelPlayBannerPosition.CenterRight,
                AdPosition.BottomLeft => LevelPlayBannerPosition.BottomLeft,
                AdPosition.BottomRight => LevelPlayBannerPosition.BottomRight,
                _ => LevelPlayBannerPosition.BottomCenter,
            };
        }

        private static Vector2 OffsetFor(AdPosition position, Vector2 insetDp)
        {
            // LevelPlay custom coordinates are dp measured from the top-left of the screen.
            var density = UniAds.PixelsPerDp;
            var widthDp = Screen.width / density;
            var heightDp = Screen.height / density;

            var x = position switch
            {
                AdPosition.TopLeft or AdPosition.CenterLeft or AdPosition.BottomLeft => insetDp.x,
                AdPosition.TopRight or AdPosition.CenterRight or AdPosition.BottomRight => widthDp - insetDp.x,
                _ => widthDp * 0.5f,
            };

            var y = position switch
            {
                AdPosition.TopLeft or AdPosition.TopCenter or AdPosition.TopRight => insetDp.y,
                AdPosition.BottomLeft or AdPosition.BottomCenter or AdPosition.BottomRight => heightDp - insetDp.y,
                _ => heightDp * 0.5f,
            };

            return new Vector2(x, y);
        }

        private void Complete(AdShowResult result)
        {
            // TrySetResult, not SetResult: a rewarded ad raises OnAdRewarded then OnAdClosed,
            // and the second must not overwrite the reward with a skip.
            _pendingShow?.TrySetResult(result);
            _pendingShow = null;
        }

        private void LogLoadFailure(AdFormat format, LevelPlayAdError error)
        {
            if (_config == null || !_config.VerboseLogging) return;

            // No-fill is normal and not an error — logging it as one trains people to ignore
            // the console, which is where a genuine misconfiguration would show up.
            UniStatics.LogInfo($"{format} load failed [{error.ErrorCode}]: {error.ErrorMessage}", this, Color.yellow);
        }

        private void HandleInitSuccess(LevelPlayConfiguration configuration)
        {
            Unsubscribe();
            _initialization?.TrySetResult(true);
        }

        private void HandleInitFailed(LevelPlayInitError error)
        {
            Unsubscribe();

            UniStatics.LogError($"LevelPlay init failed [{error.ErrorCode}]: {error.ErrorMessage}", this);

            // Resolve rather than fault: a failed ad SDK must not take startup down, and
            // IsReady simply keeps returning false.
            _initialization?.TrySetResult(false);
        }

        private void Unsubscribe()
        {
            LevelPlay.OnInitSuccess -= HandleInitSuccess;
            LevelPlay.OnInitFailed -= HandleInitFailed;
        }
    }
}
#endif
