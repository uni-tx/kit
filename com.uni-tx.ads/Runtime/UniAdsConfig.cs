using System;
using UnityEngine;

namespace UniTx.Ads
{
    /// <summary>
    /// Ad unit ids and pacing, created via <c>Assets ▸ Create ▸ UniTx ▸ Ads ▸ Config</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Drop your own ids in here; no code changes are needed to ship. Every id field is
    /// blank by default rather than pre-filled with someone else's unit — a stray real id
    /// in a template is how a build ends up serving ads that pay a stranger.
    /// </para>
    /// <para>
    /// In development builds <see cref="UseTestAds"/> is on, so the app key and unit ids are
    /// ignored and the provider serves test inventory. That keeps real impressions out of
    /// your dashboards while you develop — invalid-traffic flags from testing against live
    /// units are a genuine risk to an ad account.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "UniAdsConfig", menuName = "UniTx/Ads/Config")]
    public sealed class UniAdsConfig : ScriptableObject
    {
        /// <summary>
        /// Resources path the ads facade falls back to when none is supplied.
        /// </summary>
        public const string DefaultResourcePath = "UniAdsConfig";

        [Header("App keys")]
        [Tooltip("LevelPlay Android app key, from the LevelPlay dashboard.")]
        [SerializeField] private string _androidAppKey = string.Empty;

        [Tooltip("LevelPlay iOS app key, from the LevelPlay dashboard.")]
        [SerializeField] private string _iosAppKey = string.Empty;

        [Header("Ad unit ids — Android")]
        [SerializeField] private AdUnitIds _androidUnits = new();

        [Header("Ad unit ids — iOS")]
        [SerializeField] private AdUnitIds _iosUnits = new();

        [Header("Testing")]
        [Tooltip("Serve test ads instead of live inventory. Forced on in development " +
                 "builds and in the editor regardless of this setting.")]
        [SerializeField] private bool _useTestAdsInReleaseBuilds;

        [Tooltip("Log every request, fill and failure. Noisy, but the only practical way to " +
                 "diagnose a no-fill on device.")]
        [SerializeField] private bool _verboseLogging = true;

        [Header("Pacing")]
        [Tooltip("Minimum seconds between interstitials. 0 disables the cooldown.")]
        [SerializeField, Min(0f)] private float _interstitialCooldown = 45f;

        [Tooltip("Minimum seconds between app-open ads.")]
        [SerializeField, Min(0f)] private float _appOpenCooldown = 60f;

        [Tooltip("Skip the app-open ad on the very first launch, while the player is still " +
                 "deciding whether they like the game.")]
        [SerializeField] private bool _skipAppOpenOnFirstLaunch = true;

        [Header("Layout")]
        [Tooltip("Offset banners and MRECs by the device safe area. LevelPlay's own " +
                 "respectSafeArea is Android-only, so without this an iOS bottom banner " +
                 "sits under the home indicator.")]
        [SerializeField] private bool _respectSafeArea = true;

        [SerializeField] private AdPosition _defaultBannerPosition = AdPosition.BottomCenter;
        [SerializeField] private AdPosition _defaultMrecPosition = AdPosition.Center;

        /// <summary>
        /// Gets the app key for the current runtime platform.
        /// </summary>
        public string AppKey =>
#if UNITY_IOS
            _iosAppKey;
#else
            _androidAppKey;
#endif

        /// <summary>
        /// Gets the ad unit ids for the current runtime platform.
        /// </summary>
        public AdUnitIds Units =>
#if UNITY_IOS
            _iosUnits;
#else
            _androidUnits;
#endif

        /// <summary>
        /// Indicates whether test inventory should be served.
        /// </summary>
        /// <remarks>
        /// Always true in the editor and in development builds. Testing against live ad
        /// units generates invalid traffic, which ad networks penalize accounts for.
        /// </remarks>
        public bool UseTestAds
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return _useTestAdsInReleaseBuilds;
#endif
            }
        }

        /// <summary>
        /// Gets whether the provider should log every request and result.
        /// </summary>
        public bool VerboseLogging => _verboseLogging;

        /// <summary>
        /// Gets the minimum seconds between interstitials.
        /// </summary>
        public float InterstitialCooldown => _interstitialCooldown;

        /// <summary>
        /// Gets the minimum seconds between app-open ads.
        /// </summary>
        public float AppOpenCooldown => _appOpenCooldown;

        /// <summary>
        /// Gets whether to skip the app-open ad on first launch.
        /// </summary>
        public bool SkipAppOpenOnFirstLaunch => _skipAppOpenOnFirstLaunch;

        /// <summary>
        /// Gets whether inline ads should be offset by the safe area.
        /// </summary>
        public bool RespectSafeArea => _respectSafeArea;

        /// <summary>
        /// Gets the default banner anchor.
        /// </summary>
        public AdPosition DefaultBannerPosition => _defaultBannerPosition;

        /// <summary>
        /// Gets the default MREC anchor.
        /// </summary>
        public AdPosition DefaultMrecPosition => _defaultMrecPosition;

        /// <summary>
        /// Returns the ad unit id for a format, or an empty string when unconfigured.
        /// </summary>
        /// <param name="format">The format to look up.</param>
        public string GetUnitId(AdFormat format) => Units.For(format);

        /// <summary>
        /// Reports which formats have no ad unit id configured.
        /// </summary>
        /// <returns>A human-readable summary, or an empty string when everything is set.</returns>
        /// <remarks>
        /// A blank id is not an error — plenty of games ship without banners — so this
        /// reports rather than throws, and the facade logs it once at startup.
        /// </remarks>
        public string DescribeMissingUnits()
        {
            var missing = string.Empty;

            foreach (AdFormat format in Enum.GetValues(typeof(AdFormat)))
            {
                if (!string.IsNullOrWhiteSpace(GetUnitId(format))) continue;

                missing += missing.Length == 0 ? format.ToString() : $", {format}";
            }

            return missing;
        }

        /// <summary>
        /// Ad unit ids for one platform.
        /// </summary>
        [Serializable]
        public sealed class AdUnitIds
        {
            [Tooltip("Ad unit id for interstitials. Leave blank if unused.")]
            [SerializeField] private string _interstitial = string.Empty;

            [Tooltip("Ad unit id for rewarded ads. Leave blank if unused.")]
            [SerializeField] private string _rewarded = string.Empty;

            [Tooltip("Ad unit id for banners. Leave blank if unused.")]
            [SerializeField] private string _banner = string.Empty;

            [Tooltip("Ad unit id for MRECs. In LevelPlay this is a banner ad unit sized " +
                     "300x250, so it needs its own unit — not the banner one.")]
            [SerializeField] private string _mrec = string.Empty;

            [Tooltip("Ad unit id for app-open ads. LevelPlay does not support this format; " +
                     "leave blank unless your provider does.")]
            [SerializeField] private string _appOpen = string.Empty;

            /// <summary>
            /// Gets the interstitial ad unit id.
            /// </summary>
            public string Interstitial => _interstitial;

            /// <summary>
            /// Gets the rewarded ad unit id.
            /// </summary>
            public string Rewarded => _rewarded;

            /// <summary>
            /// Gets the banner ad unit id.
            /// </summary>
            public string Banner => _banner;

            /// <summary>
            /// Gets the MREC ad unit id.
            /// </summary>
            public string Mrec => _mrec;

            /// <summary>
            /// Gets the app-open ad unit id.
            /// </summary>
            public string AppOpen => _appOpen;

            /// <summary>
            /// Returns the id for a format.
            /// </summary>
            /// <param name="format">The format to look up.</param>
            public string For(AdFormat format) => format switch
            {
                AdFormat.Interstitial => _interstitial,
                AdFormat.Rewarded => _rewarded,
                AdFormat.Banner => _banner,
                AdFormat.Mrec => _mrec,
                AdFormat.AppOpen => _appOpen,
                _ => string.Empty,
            };
        }
    }
}
