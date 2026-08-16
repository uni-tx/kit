using System;
using UnityEngine;

namespace UniTx.Ads
{
    /// <summary>
    /// The kind of ad a placement shows.
    /// </summary>
    public enum AdFormat
    {
        /// <summary>
        /// Full-screen, skippable, shown at a natural break.
        /// </summary>
        Interstitial,

        /// <summary>
        /// Full-screen, opt-in, grants a reward on completion.
        /// </summary>
        Rewarded,

        /// <summary>
        /// Persistent strip anchored to an edge of the screen, typically 320x50.
        /// </summary>
        Banner,

        /// <summary>
        /// Medium rectangle, 300x250. An inline unit for menus and results screens.
        /// </summary>
        /// <remarks>
        /// Mediation SDKs generally serve this through the *banner* ad unit at a different
        /// size — LevelPlay uses <c>LevelPlayAdSize.MEDIUM_RECTANGLE</c>. It is a separate
        /// value here because it needs its own ad unit id, position and lifetime.
        /// </remarks>
        Mrec,

        /// <summary>
        /// Full-screen ad shown while the app itself is loading, on cold start or resume.
        /// </summary>
        /// <remarks>
        /// Not offered by every mediation SDK — notably <b>LevelPlay does not support it</b>
        /// (its ad units are rewarded, interstitial, banner and native). Adapters that
        /// cannot serve it report <see cref="AdResult.Unsupported"/>, so calling code can
        /// hide the feature rather than wait on an ad that never arrives.
        /// </remarks>
        AppOpen,
    }

    /// <summary>
    /// Where a banner or MREC is anchored on screen.
    /// </summary>
    /// <remarks>
    /// The nine anchors match what mediation SDKs expose, so a position maps across without
    /// interpretation. Use <see cref="AdPlacement.At"/> for an exact coordinate instead.
    /// </remarks>
    public enum AdPosition
    {
        /// <summary>
        /// Top-left corner.
        /// </summary>
        TopLeft,

        /// <summary>
        /// Centred against the top edge.
        /// </summary>
        TopCenter,

        /// <summary>
        /// Top-right corner.
        /// </summary>
        TopRight,

        /// <summary>
        /// Centred against the left edge.
        /// </summary>
        CenterLeft,

        /// <summary>
        /// Centred on screen.
        /// </summary>
        Center,

        /// <summary>
        /// Centred against the right edge.
        /// </summary>
        CenterRight,

        /// <summary>
        /// Bottom-left corner.
        /// </summary>
        BottomLeft,

        /// <summary>
        /// Centred against the bottom edge. The usual choice for a banner.
        /// </summary>
        BottomCenter,

        /// <summary>
        /// Bottom-right corner.
        /// </summary>
        BottomRight,
    }

    /// <summary>
    /// Where an inline ad goes: one of the nine anchors, or an exact coordinate.
    /// </summary>
    public readonly struct AdPlacement : IEquatable<AdPlacement>
    {
        /// <summary>
        /// Gets the anchor, when this is not a custom coordinate.
        /// </summary>
        public AdPosition Position { get; }

        /// <summary>
        /// Gets the coordinate in density-independent pixels, when custom.
        /// </summary>
        public Vector2 Offset { get; }

        /// <summary>
        /// Indicates whether this is an exact coordinate rather than an anchor.
        /// </summary>
        public bool IsCustom { get; }

        private AdPlacement(AdPosition position, Vector2 offset, bool isCustom)
        {
            Position = position;
            Offset = offset;
            IsCustom = isCustom;
        }

        /// <summary>
        /// Creates a placement at one of the nine anchors.
        /// </summary>
        /// <param name="position">The anchor to use.</param>
        public static AdPlacement At(AdPosition position) => new(position, Vector2.zero, false);

        /// <summary>
        /// Creates a placement at an exact coordinate.
        /// </summary>
        /// <param name="dpOffset">Coordinate in density-independent pixels, origin top-left.</param>
        /// <remarks>
        /// In dp, not pixels: a pixel coordinate lands somewhere different on every device
        /// density, which is how a banner ends up half off-screen on a high-DPI phone.
        /// </remarks>
        public static AdPlacement At(Vector2 dpOffset) => new(AdPosition.TopLeft, dpOffset, true);

        /// <summary>
        /// Gets the default placement, bottom-centre.
        /// </summary>
        public static AdPlacement Default => At(AdPosition.BottomCenter);

        /// <inheritdoc />
        public bool Equals(AdPlacement other)
            => IsCustom == other.IsCustom
               && (IsCustom ? Offset == other.Offset : Position == other.Position);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is AdPlacement other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Position, Offset, IsCustom);

        /// <inheritdoc />
        public override string ToString() => IsCustom ? $"({Offset.x}, {Offset.y})dp" : Position.ToString();
    }

    /// <summary>
    /// How an ad request ended.
    /// </summary>
    public enum AdResult
    {
        /// <summary>
        /// Watched to completion. The reward is owed.
        /// </summary>
        Completed,

        /// <summary>
        /// Dismissed early. No reward.
        /// </summary>
        Skipped,

        /// <summary>
        /// No fill, or the SDK reported an error.
        /// </summary>
        Failed,

        /// <summary>
        /// Nothing was loaded and ready to show.
        /// </summary>
        NotReady,

        /// <summary>
        /// The active provider cannot serve this format at all.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="NotReady"/>, which is temporary. Unsupported is
        /// permanent for this provider, so the feature should be hidden rather than retried
        /// — LevelPlay and app-open ads, for instance.
        /// </remarks>
        Unsupported,
    }

    /// <summary>
    /// Outcome of a show request.
    /// </summary>
    public readonly struct AdShowResult
    {
        /// <summary>
        /// Gets how the ad ended.
        /// </summary>
        public AdResult Result { get; }

        /// <summary>
        /// Gets the failure reason, when there is one.
        /// </summary>
        public string Error { get; }

        /// <summary>
        /// Indicates whether a reward should be granted.
        /// </summary>
        /// <remarks>
        /// The single place calling code should branch. Checking <see cref="Result"/> by
        /// hand is how "reward on close" ships, paying out players who skipped.
        /// </remarks>
        public bool ShouldReward => Result == AdResult.Completed;

        /// <summary>
        /// Indicates whether retrying could ever succeed.
        /// </summary>
        public bool IsRetryable => Result is AdResult.NotReady or AdResult.Failed;

        /// <summary>
        /// Creates a result.
        /// </summary>
        /// <param name="result">How the ad ended.</param>
        /// <param name="error">Optional failure reason.</param>
        public AdShowResult(AdResult result, string error = null)
        {
            Result = result;
            Error = error;
        }

        /// <summary>
        /// Gets a completed result.
        /// </summary>
        public static AdShowResult Completed => new(AdResult.Completed);

        /// <summary>
        /// Gets a skipped result.
        /// </summary>
        public static AdShowResult Skipped => new(AdResult.Skipped);

        /// <summary>
        /// Gets a not-ready result.
        /// </summary>
        public static AdShowResult NotReady => new(AdResult.NotReady);

        /// <summary>
        /// Gets a result for a format the provider cannot serve.
        /// </summary>
        /// <param name="format">The unsupported format.</param>
        /// <param name="providerName">The provider that cannot serve it.</param>
        public static AdShowResult Unsupported(AdFormat format, string providerName)
            => new(AdResult.Unsupported, $"{providerName} does not support {format} ads.");

        /// <summary>
        /// Gets a failed result carrying a reason.
        /// </summary>
        /// <param name="error">Why the ad failed.</param>
        public static AdShowResult Failed(string error) => new(AdResult.Failed, error);
    }
}
