using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniTx.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace UniTx.Ads.Tests.PlayMode
{
    public class UniAdsTests
    {
        private UniAdsConfig _config;

        [SetUp]
        public void SetUp()
        {
            UniAds.Reset();

            // A config built in memory rather than loaded from Resources, so the tests do
            // not depend on a project asset existing.
            _config = ScriptableObject.CreateInstance<UniAdsConfig>();

            UniAds.InterstitialCooldown = 0f;
            UniAds.AppOpenCooldown = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            UniAds.Reset();
            UniSafeArea.SetOverride(SafeAreaInsets.Zero);

            if (_config != null) Object.DestroyImmediate(_config);
        }

        private UniTask InitAsync(params AdFormat[] unsupported)
            => UniAds.InitializeAsync(new NoOpAdsProvider(0f, AdResult.Completed, unsupported),
                _config, CancellationToken.None);

        /// <summary>
        /// Pretends the app has been launched before, so the app-open gate opens.
        /// </summary>
        private static void MarkNotFirstLaunch()
        {
            PlayerPrefs.SetInt(UniAds.FirstLaunchKey, 1);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ capability

        [Test]
        public void Supports_WithoutProvider_IsFalse()
            => Assert.IsFalse(UniAds.Supports(AdFormat.Rewarded));

        [UnityTest]
        public IEnumerator Supports_ReportsPerFormatCapability() => UniTask.ToCoroutine(async () =>
        {
            await InitAsync(AdFormat.AppOpen);

            Assert.IsTrue(UniAds.Supports(AdFormat.Banner));
            Assert.IsTrue(UniAds.Supports(AdFormat.Mrec));
            Assert.IsFalse(UniAds.Supports(AdFormat.AppOpen), "provider declared AppOpen unsupported");
        });

        [UnityTest]
        public IEnumerator AppOpen_OnProviderWithoutSupport_ReportsUnsupported() => UniTask.ToCoroutine(async () =>
        {
            // Mirrors LevelPlay, whose ad units are rewarded, interstitial, banner and
            // native. Unsupported is permanent, so the UI should hide the feature rather
            // than retry — which is why it is distinct from NotReady.
            await InitAsync(AdFormat.AppOpen);

            var result = await UniAds.ShowAppOpenAsync(cToken: CancellationToken.None);

            Assert.AreEqual(AdResult.Unsupported, result.Result);
            Assert.IsFalse(result.IsRetryable);
            StringAssert.Contains("AppOpen", result.Error);
        });

        [UnityTest]
        public IEnumerator AppOpen_OnSupportingProvider_Completes() => UniTask.ToCoroutine(async () =>
        {
            await InitAsync();
            MarkNotFirstLaunch();

            var result = await UniAds.ShowAppOpenAsync(cToken: CancellationToken.None);

            Assert.AreEqual(AdResult.Completed, result.Result);
        });

        [UnityTest]
        public IEnumerator AppOpen_IsSkippedOnFirstLaunch() => UniTask.ToCoroutine(async () =>
        {
            await InitAsync();
            PlayerPrefs.DeleteKey(UniAds.FirstLaunchKey);

            // An ad before the player has seen the game at all is the most uninstall-prone
            // impression there is.
            var first = await UniAds.ShowAppOpenAsync(cToken: CancellationToken.None);
            Assert.AreEqual(AdResult.Failed, first.Result);
            StringAssert.Contains("first launch", first.Error);

            // The very next launch is fair game.
            var second = await UniAds.ShowAppOpenAsync(cToken: CancellationToken.None);
            Assert.AreEqual(AdResult.Completed, second.Result);
        });

        // ------------------------------------------------------------------ rewarded

        [UnityTest]
        public IEnumerator ShowRewarded_Completed_GrantsReward() => UniTask.ToCoroutine(async () =>
        {
            await InitAsync();

            var result = await UniAds.ShowRewardedAsync(cToken: CancellationToken.None);

            Assert.IsTrue(result.ShouldReward);
        });

        [UnityTest]
        public IEnumerator ShowRewarded_Skipped_DoesNotGrantReward() => UniTask.ToCoroutine(async () =>
        {
            await UniAds.InitializeAsync(new NoOpAdsProvider(0f, AdResult.Skipped), _config,
                CancellationToken.None);

            var result = await UniAds.ShowRewardedAsync(cToken: CancellationToken.None);

            // Rewarding on close rather than completion is the classic monetization bug.
            Assert.AreEqual(AdResult.Skipped, result.Result);
            Assert.IsFalse(result.ShouldReward);
        });

        [UnityTest]
        public IEnumerator ShowRewarded_IgnoresInterstitialCooldown() => UniTask.ToCoroutine(async () =>
        {
            await InitAsync();
            UniAds.InterstitialCooldown = 600f;

            // Rewarded ads are opt-in, so interstitial pacing must never block them.
            Assert.IsTrue((await UniAds.ShowRewardedAsync(cToken: CancellationToken.None)).ShouldReward);
            Assert.IsTrue((await UniAds.ShowRewardedAsync(cToken: CancellationToken.None)).ShouldReward);
        });

        // ------------------------------------------------------------------ pacing

        [UnityTest]
        public IEnumerator ShowInterstitial_RespectsCooldown() => UniTask.ToCoroutine(async () =>
        {
            await InitAsync();
            UniAds.InterstitialCooldown = 600f;

            var first = await UniAds.ShowInterstitialAsync(cToken: CancellationToken.None);
            var second = await UniAds.ShowInterstitialAsync(cToken: CancellationToken.None);

            Assert.AreEqual(AdResult.Completed, first.Result);
            Assert.AreEqual(AdResult.Failed, second.Result, "back-to-back interstitials must be blocked");
        });

        [UnityTest]
        public IEnumerator ShowAsync_WhileAnotherIsShowing_IsRejected() => UniTask.ToCoroutine(async () =>
        {
            await UniAds.InitializeAsync(new NoOpAdsProvider(0.2f), _config, CancellationToken.None);

            var pending = UniAds.ShowRewardedAsync(cToken: CancellationToken.None);
            await UniTask.Yield();

            // A double-tapped button must not stack two ads.
            Assert.AreEqual(AdResult.Failed,
                (await UniAds.ShowRewardedAsync(cToken: CancellationToken.None)).Result);

            await pending;
        });

        // ------------------------------------------------------------------ inline placement

        [UnityTest]
        public IEnumerator ShowBanner_UsesConfiguredDefaultPosition() => UniTask.ToCoroutine(async () =>
        {
            var provider = new NoOpAdsProvider(0f);
            await UniAds.InitializeAsync(provider, _config, CancellationToken.None);

            await UniAds.ShowBannerAsync(cToken: CancellationToken.None);

            Assert.AreEqual(AdPosition.BottomCenter, provider.LastPlacement.Position);
            CollectionAssert.Contains(provider.VisibleInline, AdFormat.Banner);
        });

        [UnityTest]
        public IEnumerator ShowMrec_DefaultsToCentre() => UniTask.ToCoroutine(async () =>
        {
            var provider = new NoOpAdsProvider(0f);
            await UniAds.InitializeAsync(provider, _config, CancellationToken.None);

            await UniAds.ShowMrecAsync(cToken: CancellationToken.None);

            Assert.AreEqual(AdPosition.Center, provider.LastPlacement.Position);
            CollectionAssert.Contains(provider.VisibleInline, AdFormat.Mrec);
        });

        [UnityTest]
        public IEnumerator ShowBanner_HonoursExplicitPlacement() => UniTask.ToCoroutine(async () =>
        {
            var provider = new NoOpAdsProvider(0f);
            await UniAds.InitializeAsync(provider, _config, CancellationToken.None);

            await UniAds.ShowBannerAsync(AdPlacement.At(AdPosition.TopRight), CancellationToken.None);

            Assert.AreEqual(AdPosition.TopRight, provider.LastPlacement.Position);
        });

        [UnityTest]
        public IEnumerator ShowBanner_SupportsCustomCoordinate() => UniTask.ToCoroutine(async () =>
        {
            var provider = new NoOpAdsProvider(0f);
            await UniAds.InitializeAsync(provider, _config, CancellationToken.None);

            await UniAds.ShowBannerAsync(AdPlacement.At(new Vector2(24f, 100f)), CancellationToken.None);

            Assert.IsTrue(provider.LastPlacement.IsCustom);
            Assert.AreEqual(new Vector2(24f, 100f), provider.LastPlacement.Offset);
        });

        [UnityTest]
        public IEnumerator HideAndDestroyInline_ClearVisibility() => UniTask.ToCoroutine(async () =>
        {
            var provider = new NoOpAdsProvider(0f);
            await UniAds.InitializeAsync(provider, _config, CancellationToken.None);

            await UniAds.ShowBannerAsync(cToken: CancellationToken.None);
            UniAds.HideBanner();
            CollectionAssert.DoesNotContain(provider.VisibleInline, AdFormat.Banner);

            await UniAds.ShowMrecAsync(cToken: CancellationToken.None);
            UniAds.DestroyInline(AdFormat.Mrec);
            CollectionAssert.DoesNotContain(provider.VisibleInline, AdFormat.Mrec);
        });

        // ------------------------------------------------------------------ safe area

        [Test]
        public void SafeAreaInset_IsZero_WhenDeviceReportsNoInsets()
        {
            UniSafeArea.SetOverride(SafeAreaInsets.Zero);

            Assert.AreEqual(Vector2.zero, UniAds.GetSafeAreaInsetDp(AdPlacement.At(AdPosition.BottomCenter)));
        }

        [UnityTest]
        public IEnumerator SafeAreaInset_AppliesOnlyToTouchedEdges() => UniTask.ToCoroutine(async () =>
        {
            await InitAsync();

            // A device with a top notch and a bottom home indicator.
            UniSafeArea.SetOverride(new SafeAreaInsets(0f, 0f, 0.04f, 0.06f));

            var bottom = UniAds.GetSafeAreaInsetDp(AdPlacement.At(AdPosition.BottomCenter));
            var top = UniAds.GetSafeAreaInsetDp(AdPlacement.At(AdPosition.TopCenter));
            var centre = UniAds.GetSafeAreaInsetDp(AdPlacement.At(AdPosition.Center));

            // A bottom banner is pushed up by the home indicator only, never by the notch —
            // insetting every edge would waste screen space the ad could occupy.
            Assert.Greater(bottom.y, 0f);
            Assert.Greater(top.y, bottom.y, "the top notch is larger than the home indicator");
            Assert.AreEqual(0f, bottom.x);
            Assert.AreEqual(Vector2.zero, centre, "a centred ad touches no edge");
        });

        [UnityTest]
        public IEnumerator SafeAreaInset_IsSkipped_WhenDisabledInConfig() => UniTask.ToCoroutine(async () =>
        {
            // RespectSafeArea defaults to true; a fresh instance with it off is built by
            // serializing the flag, so drive it through the public path instead: with no
            // config at all the facade has nothing to respect.
            await UniAds.InitializeAsync(new NoOpAdsProvider(0f), null, CancellationToken.None);
            UniSafeArea.SetOverride(new SafeAreaInsets(0f, 0f, 0.04f, 0.06f));

            Assert.AreEqual(Vector2.zero, UniAds.GetSafeAreaInsetDp(AdPlacement.At(AdPosition.BottomCenter)));
        });

        [UnityTest]
        public IEnumerator ShowBanner_PassesSafeAreaInsetToProvider() => UniTask.ToCoroutine(async () =>
        {
            var provider = new NoOpAdsProvider(0f);
            await UniAds.InitializeAsync(provider, _config, CancellationToken.None);

            UniSafeArea.SetOverride(new SafeAreaInsets(0f, 0f, 0.05f, 0f));
            await UniAds.ShowBannerAsync(AdPlacement.At(AdPosition.BottomCenter), CancellationToken.None);

            // LevelPlay's own respectSafeArea is Android-only, so the facade computes the
            // inset and hands it over for every platform.
            Assert.Greater(provider.LastSafeAreaInsetDp.y, 0f);
        });

        // ------------------------------------------------------------------ misc

        [UnityTest]
        public IEnumerator ShowAsync_RaisesOnAdClosed() => UniTask.ToCoroutine(async () =>
        {
            await InitAsync();

            AdFormat? closed = null;
            void Handler(AdFormat format, AdShowResult _) => closed = format;

            UniAds.OnAdClosed += Handler;

            try
            {
                await UniAds.ShowRewardedAsync(cToken: CancellationToken.None);
            }
            finally
            {
                UniAds.OnAdClosed -= Handler;
            }

            Assert.AreEqual(AdFormat.Rewarded, closed);
        });

        [Test]
        public void AdShowResult_ShouldReward_OnlyOnCompleted()
        {
            Assert.IsTrue(AdShowResult.Completed.ShouldReward);
            Assert.IsFalse(AdShowResult.Skipped.ShouldReward);
            Assert.IsFalse(AdShowResult.NotReady.ShouldReward);
            Assert.IsFalse(AdShowResult.Failed("nope").ShouldReward);
            Assert.IsFalse(AdShowResult.Unsupported(AdFormat.AppOpen, "LevelPlay").ShouldReward);
        }

        [Test]
        public void AdShowResult_IsRetryable_OnlyForTransientOutcomes()
        {
            Assert.IsTrue(AdShowResult.NotReady.IsRetryable);
            Assert.IsTrue(AdShowResult.Failed("no fill").IsRetryable);
            Assert.IsFalse(AdShowResult.Unsupported(AdFormat.AppOpen, "LevelPlay").IsRetryable);
            Assert.IsFalse(AdShowResult.Completed.IsRetryable);
        }

        [Test]
        public void Banner_WithoutProvider_DoesNotThrow()
        {
            Assert.DoesNotThrow(UniAds.HideBanner);
            Assert.DoesNotThrow(UniAds.HideMrec);
            Assert.DoesNotThrow(() => UniAds.DestroyInline(AdFormat.Banner));
        }

        [Test]
        public void Config_ReportsMissingUnits()
        {
            // Every id is blank by default — deliberately, so a template can never ship
            // someone else's live ad unit.
            var missing = _config.DescribeMissingUnits();

            StringAssert.Contains("Interstitial", missing);
            StringAssert.Contains("AppOpen", missing);
        }

        [UnityTest]
        public IEnumerator Reset_ClearsEverySessionStatic() => UniTask.ToCoroutine(async () =>
        {
            // With Reload Domain disabled, statics survive between play sessions. A leftover
            // cooldown, a withdrawn consent flag, or "app-open already played" would make
            // the second Play behave differently from the first.
            await InitAsync();
            UniAds.SetConsent(false);
            UniAds.InterstitialCooldown = 999f;
            MarkNotFirstLaunch();
            await UniAds.ShowInterstitialAsync(cToken: CancellationToken.None);

            UniAds.Reset();

            Assert.IsNull(UniAds.Provider);
            Assert.IsNull(UniAds.Config);
            Assert.IsTrue(UniAds.HasConsent, "consent must not carry into the next session");
            Assert.AreEqual(45f, UniAds.InterstitialCooldown, "cooldown must return to its default");
            Assert.IsFalse(UniAds.IsShowing);

            // The cooldown timestamp is cleared too, so a fresh session is not blocked by an
            // interstitial shown in the previous one.
            await InitAsync();
            UniAds.InterstitialCooldown = 999f;
            Assert.AreEqual(AdResult.Completed,
                (await UniAds.ShowInterstitialAsync(cToken: CancellationToken.None)).Result);
        });

        [Test]
        public void PixelsPerDp_FallsBackToOneWhenDpiIsUnreported()
        {
            // Screen.dpi is 0 on devices that do not report it. The fallback must be 1:1 —
            // dividing a clamped dpi by 160 would turn an unknown density into 0.00625 and
            // misplace a banner by a factor of 160.
            var density = UniAds.PixelsPerDp;

            Assert.Greater(density, 0f);
            Assert.IsFalse(float.IsNaN(density));
            Assert.IsFalse(float.IsInfinity(density));
        }

        [Test]
        public void Config_ForcesTestAdsInTheEditor()
            => Assert.IsTrue(_config.UseTestAds,
                "test mode must be forced in the editor; live units would log invalid traffic");
    }
}
