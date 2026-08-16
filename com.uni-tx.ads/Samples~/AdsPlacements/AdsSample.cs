using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Ads;
using UniTx.Core;
using UniTx.IoC;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.Ads.Samples
{
    /// <summary>
    /// Every placement type, and how to swap in a real network.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Setup.</b> Create a config via <b>Assets ▸ Create ▸ UniTx ▸ Ads ▸ Config</b>, put
    /// it at <c>Resources/UniAdsConfig</c>, and paste your own ad unit ids into it. Nothing
    /// here needs editing to ship. In the editor and development builds the config forces
    /// test mode, so live units are never hit.
    /// </para>
    /// <para>
    /// Runs on <see cref="NoOpAdsProvider"/> by default. To use LevelPlay, install
    /// <c>com.unity.services.levelplay</c> (9.0.0+) and register
    /// <c>new LevelPlayAdsProvider()</c> instead — the adapter compiles automatically once
    /// the SDK is present.
    /// </para>
    /// </remarks>
    public sealed class AdsSample : MonoBehaviour
    {
        [SerializeField] private UniAdsConfig _config;

        private CancellationTokenSource _cts;

        private void Awake()
            => _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

        private async void Start()
        {
            // Swap this line for `new LevelPlayAdsProvider()` once the SDK is installed.
            // NoOpAdsProvider is told AppOpen is unsupported so this sample exercises the
            // same path LevelPlay takes — it has no app-open ad unit.
            var provider = new NoOpAdsProvider(0.5f, AdResult.Completed, AdFormat.AppOpen);

            await UniAds.InitializeAsync(provider, _config, _cts.Token);

            // Consent must be recorded before requesting personalized ads (GDPR, ATT).
            UniAds.SetConsent(true);

            await UniAds.LoadAsync(AdFormat.Rewarded, _cts.Token);
            await UniAds.LoadAsync(AdFormat.Interstitial, _cts.Token);

            await ShowAppOpenIfAvailableAsync();

            await UniAds.ShowBannerAsync(cToken: _cts.Token);
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        // ------------------------------------------------------------------ app open

        private async UniTask ShowAppOpenIfAvailableAsync()
        {
            // Supports() is permanent capability, not fill. Gate the *feature* on it —
            // LevelPlay returns false here, so there is nothing to wait for.
            if (!UniAds.Supports(AdFormat.AppOpen))
            {
                Debug.Log("Provider has no app-open format — skipping that placement entirely.");
                return;
            }

            var result = await UniAds.ShowAppOpenAsync(cToken: _cts.Token);

            // Skipped on the very first launch by config, which is deliberate: an ad before
            // the player has seen the game is the most uninstall-prone impression there is.
            Debug.Log($"App-open: {result.Result} {result.Error}");
        }

        // ------------------------------------------------------------------ rewarded

        /// <summary>
        /// Shows a rewarded ad and grants only on completion.
        /// </summary>
        [ContextMenu("Show Rewarded")]
        public void ShowRewarded() => ShowRewardedAsync().Forget();

        private async UniTaskVoid ShowRewardedAsync()
        {
            // Gate the button on readiness so the player never taps into a dead end.
            if (!UniAds.IsReady(AdFormat.Rewarded))
            {
                Debug.Log("No rewarded ad available — disable the button.");
                return;
            }

            var result = await UniAds.ShowRewardedAsync("double_coins", _cts.Token);

            // Branch on ShouldReward, never on "the ad closed". Rewarding on close pays out
            // players who skipped at two seconds.
            if (!result.ShouldReward)
            {
                Debug.Log($"No reward: {result.Result} {result.Error}");
                return;
            }

            GrantAndPersistReward(100);

            UniAds.LoadAsync(AdFormat.Rewarded, _cts.Token).Forget();
        }

        private static void GrantAndPersistReward(int coins)
        {
            Debug.Log($"Granted {coins} coins.");

            // Persist immediately. Players background the app the instant an ad closes, and
            // a reward still sitting in the autosave queue is a support ticket.
            if (IoCStatics.IsInitialized &&
                IoCStatics.Resolver.TryResolve<ISerialisationService>(out var saves))
            {
                saves.Flush();
            }
        }

        // ------------------------------------------------------------------ inline

        /// <summary>
        /// Anchors a banner to the bottom, clear of the home indicator.
        /// </summary>
        [ContextMenu("Show Banner (bottom)")]
        public void ShowBannerBottom()
            // The facade offsets by the safe area automatically — LevelPlay's own
            // respectSafeArea is Android-only, so iOS would otherwise put this under the
            // home indicator.
            => UniAds.ShowBannerAsync(AdPlacement.At(AdPosition.BottomCenter), _cts.Token).Forget();

        /// <summary>
        /// Anchors a banner to the top-right corner.
        /// </summary>
        [ContextMenu("Show Banner (top-right)")]
        public void ShowBannerTopRight()
            => UniAds.ShowBannerAsync(AdPlacement.At(AdPosition.TopRight), _cts.Token).Forget();

        /// <summary>
        /// Places a banner at an exact coordinate.
        /// </summary>
        [ContextMenu("Show Banner (custom position)")]
        public void ShowBannerCustom()
            // dp, not pixels — a pixel coordinate lands somewhere different on every screen
            // density, which is how a banner ends up half off-screen on a high-DPI phone.
            => UniAds.ShowBannerAsync(AdPlacement.At(new Vector2(16f, 120f)), _cts.Token).Forget();

        /// <summary>
        /// Shows a 300x250 MREC centred, e.g. on a results screen.
        /// </summary>
        [ContextMenu("Show MREC")]
        public void ShowMrec()
            // An MREC is a banner ad unit at 300x250 in LevelPlay, so it needs its own ad
            // unit id — not the banner one.
            => UniAds.ShowMrecAsync(AdPlacement.At(AdPosition.Center), _cts.Token).Forget();

        /// <summary>
        /// Hides the banner but keeps it loaded, for a cheap re-show.
        /// </summary>
        [ContextMenu("Hide Banner")]
        public void HideBanner() => UniAds.HideBanner();

        /// <summary>
        /// Destroys the MREC when leaving the screen that owned it.
        /// </summary>
        [ContextMenu("Destroy MREC")]
        public void DestroyMrec()
            // Destroy rather than hide when leaving for good: a live inline ad keeps
            // auto-refreshing and burning impressions on a screen nobody is looking at.
            => UniAds.DestroyInline(AdFormat.Mrec);

        // ------------------------------------------------------------------ interstitial

        /// <summary>
        /// Shows an interstitial at a natural break, respecting the cooldown.
        /// </summary>
        [ContextMenu("Show Interstitial")]
        public void ShowInterstitial() => ShowInterstitialAsync().Forget();

        private async UniTaskVoid ShowInterstitialAsync()
        {
            var result = await UniAds.ShowInterstitialAsync("level_complete", _cts.Token);

            // A Failed result here usually means the cooldown blocked it — that is the
            // system working, not something to surface to the player.
            Debug.Log($"Interstitial: {result.Result} {result.Error}");
        }
    }
}
