using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Ads
{
    /// <summary>
    /// Simulates ads without any SDK: logs the request, waits, and returns a result.
    /// </summary>
    /// <remarks>
    /// Lets the whole flow — button gating, cooldowns, safe-area placement, grant-and-persist
    /// — be built and tested before an ad network is integrated, and keeps editor and CI runs
    /// from firing real requests. Register a real adapter for release builds.
    /// </remarks>
    public sealed class NoOpAdsProvider : IAdsProvider
    {
        private readonly HashSet<AdFormat> _visibleInline = new();
        private readonly HashSet<AdFormat> _unsupported;
        private readonly float _simulatedDuration;
        private readonly AdResult _rewardedResult;

        private bool _verbose = true;

        /// <summary>
        /// Creates a simulated provider.
        /// </summary>
        /// <param name="simulatedDuration">Seconds a full-screen ad appears to run for.</param>
        /// <param name="rewardedResult">Result rewarded ads resolve to. Set to
        /// <see cref="AdResult.Skipped"/> to exercise the no-reward path.</param>
        /// <param name="unsupportedFormats">
        /// Formats to report as unsupported. Pass <see cref="AdFormat.AppOpen"/> to mimic
        /// LevelPlay, so the app-open path is exercised against a provider that lacks it.
        /// </param>
        public NoOpAdsProvider(float simulatedDuration = 0.5f,
            AdResult rewardedResult = AdResult.Completed,
            params AdFormat[] unsupportedFormats)
        {
            _simulatedDuration = Mathf.Max(0f, simulatedDuration);
            _rewardedResult = rewardedResult;
            _unsupported = new HashSet<AdFormat>(unsupportedFormats ?? Array.Empty<AdFormat>());
        }

        /// <inheritdoc />
        public string Name => "NoOp";

        /// <summary>
        /// Gets the inline formats currently shown.
        /// </summary>
        public IReadOnlyCollection<AdFormat> VisibleInline => _visibleInline;

        /// <summary>
        /// Gets the last safe-area inset the facade supplied, in dp.
        /// </summary>
        public Vector2 LastSafeAreaInsetDp { get; private set; }

        /// <summary>
        /// Gets the last placement requested for an inline ad.
        /// </summary>
        public AdPlacement LastPlacement { get; private set; }

        /// <inheritdoc />
        public bool Supports(AdFormat format) => !_unsupported.Contains(format);

        /// <inheritdoc />
        public UniTask InitializeAsync(UniAdsConfig config, CancellationToken cToken = default)
        {
            _verbose = config == null || config.VerboseLogging;
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public bool IsReady(AdFormat format) => Supports(format);

        /// <inheritdoc />
        public UniTask LoadAsync(AdFormat format, CancellationToken cToken = default)
            => UniTask.CompletedTask;

        /// <inheritdoc />
        public async UniTask<AdShowResult> ShowAsync(AdFormat format, string placementName = null,
            CancellationToken cToken = default)
        {
            Log($"show {format} '{placementName ?? "default"}'");

            // Unscaled: a real ad pauses the game, so anything driven by Time.timeScale
            // would stall here and the simulation would never return.
            await UniTask.Delay(TimeSpan.FromSeconds(_simulatedDuration), DelayType.UnscaledDeltaTime,
                cancellationToken: cToken);

            return format == AdFormat.Rewarded ? new AdShowResult(_rewardedResult) : AdShowResult.Completed;
        }

        /// <inheritdoc />
        public UniTask<AdShowResult> ShowInlineAsync(AdFormat format, AdPlacement placement,
            Vector2 safeAreaInsetDp, CancellationToken cToken = default)
        {
            _visibleInline.Add(format);
            LastPlacement = placement;
            LastSafeAreaInsetDp = safeAreaInsetDp;

            Log($"show {format} at {placement} (safe-area inset {safeAreaInsetDp}dp)");

            return UniTask.FromResult(AdShowResult.Completed);
        }

        /// <inheritdoc />
        public void HideInline(AdFormat format)
        {
            _visibleInline.Remove(format);
            Log($"hide {format}");
        }

        /// <inheritdoc />
        public void DestroyInline(AdFormat format)
        {
            _visibleInline.Remove(format);
            Log($"destroy {format}");
        }

        /// <inheritdoc />
        public void SetConsent(bool hasConsent) => Log($"consent = {hasConsent}");

        private void Log(string message)
        {
            if (_verbose) UniStatics.LogInfo(message, this, Color.magenta);
        }
    }
}
