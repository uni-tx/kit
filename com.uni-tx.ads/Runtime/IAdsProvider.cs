using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniTx.Ads
{
    /// <summary>
    /// A single ad network. Implement one adapter per mediation SDK.
    /// </summary>
    /// <remarks>
    /// The kit depends on no ad SDK. Every network brings its own manifest entries, native
    /// binaries and policy obligations, so the choice — and the build-size cost — stays with
    /// the game. A project with no adapter still runs; requests resolve to
    /// <see cref="AdResult.NotReady"/>.
    /// </remarks>
    public interface IAdsProvider
    {
        /// <summary>
        /// Gets a short name used in logs, e.g. "LevelPlay".
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Indicates whether this provider can serve a format at all.
        /// </summary>
        /// <param name="format">The format to check.</param>
        /// <remarks>
        /// A permanent capability answer, not a fill answer. LevelPlay returns false for
        /// <see cref="AdFormat.AppOpen"/> because its ad units are only rewarded,
        /// interstitial, banner and native — so the UI should hide the feature rather than
        /// poll for an ad that can never arrive.
        /// </remarks>
        bool Supports(AdFormat format);

        /// <summary>
        /// Starts the underlying SDK.
        /// </summary>
        /// <param name="config">Ad unit ids, test-mode flag and pacing.</param>
        /// <param name="cToken">Token to cancel initialization.</param>
        UniTask InitializeAsync(UniAdsConfig config, CancellationToken cToken = default);

        /// <summary>
        /// Indicates whether a format has an ad loaded and ready to show.
        /// </summary>
        /// <param name="format">The format to check.</param>
        bool IsReady(AdFormat format);

        /// <summary>
        /// Preloads an ad so a later show request is instant.
        /// </summary>
        /// <param name="format">The format to load.</param>
        /// <param name="cToken">Token to cancel the load.</param>
        UniTask LoadAsync(AdFormat format, CancellationToken cToken = default);

        /// <summary>
        /// Shows a full-screen ad and waits for it to close.
        /// </summary>
        /// <param name="format">Interstitial, rewarded or app-open.</param>
        /// <param name="placementName">Optional placement name, for reporting.</param>
        /// <param name="cToken">Token to cancel the request.</param>
        UniTask<AdShowResult> ShowAsync(AdFormat format, string placementName = null,
            CancellationToken cToken = default);

        /// <summary>
        /// Shows an inline ad — a banner or an MREC.
        /// </summary>
        /// <param name="format">Either <see cref="AdFormat.Banner"/> or <see cref="AdFormat.Mrec"/>.</param>
        /// <param name="placement">Where to anchor it.</param>
        /// <param name="safeAreaInsetDp">
        /// Extra inset in density-independent pixels to keep the ad clear of cutouts and the
        /// home indicator, or zero to place flush against the edge.
        /// </param>
        /// <param name="cToken">Token to cancel the request.</param>
        /// <remarks>
        /// The inset is supplied by the facade rather than left to the SDK: LevelPlay's own
        /// <c>respectSafeArea</c> is Android-only, so an iOS bottom banner would otherwise
        /// sit under the home indicator.
        /// </remarks>
        UniTask<AdShowResult> ShowInlineAsync(AdFormat format, AdPlacement placement,
            UnityEngine.Vector2 safeAreaInsetDp, CancellationToken cToken = default);

        /// <summary>
        /// Hides an inline ad without destroying it, so it can be shown again cheaply.
        /// </summary>
        /// <param name="format">The inline format to hide.</param>
        void HideInline(AdFormat format);

        /// <summary>
        /// Destroys an inline ad and releases its native view.
        /// </summary>
        /// <param name="format">The inline format to destroy.</param>
        void DestroyInline(AdFormat format);

        /// <summary>
        /// Records or withdraws the player's personalized-ads consent.
        /// </summary>
        /// <param name="hasConsent">Whether the player consented.</param>
        void SetConsent(bool hasConsent);
    }
}
