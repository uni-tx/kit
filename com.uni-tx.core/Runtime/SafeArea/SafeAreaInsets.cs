using System;
using UnityEngine;

namespace UniTx.Core
{
    /// <summary>
    /// How much of each screen edge is obscured by a cutout, notch or system gesture area.
    /// </summary>
    /// <remarks>
    /// Normalized to 0..1 of the screen dimension, so the values survive a resolution change
    /// and can be applied directly to <c>RectTransform</c> anchors. Pixel values are
    /// available through <see cref="ToPixels"/> for consumers that position native views —
    /// an ad banner, for instance — which work in device pixels rather than canvas space.
    /// </remarks>
    public readonly struct SafeAreaInsets : IEquatable<SafeAreaInsets>
    {
        /// <summary>
        /// Insets with every edge at zero.
        /// </summary>
        public static readonly SafeAreaInsets Zero = new(0f, 0f, 0f, 0f);

        /// <summary>
        /// Gets the obscured fraction of the left edge.
        /// </summary>
        public float Left { get; }

        /// <summary>
        /// Gets the obscured fraction of the right edge.
        /// </summary>
        public float Right { get; }

        /// <summary>
        /// Gets the obscured fraction of the bottom edge.
        /// </summary>
        public float Bottom { get; }

        /// <summary>
        /// Gets the obscured fraction of the top edge.
        /// </summary>
        public float Top { get; }

        /// <summary>
        /// Indicates whether every edge is unobstructed.
        /// </summary>
        public bool IsZero => Left <= 0f && Right <= 0f && Bottom <= 0f && Top <= 0f;

        /// <summary>
        /// Creates insets from normalized edge fractions.
        /// </summary>
        /// <param name="left">Obscured fraction of the left edge.</param>
        /// <param name="right">Obscured fraction of the right edge.</param>
        /// <param name="bottom">Obscured fraction of the bottom edge.</param>
        /// <param name="top">Obscured fraction of the top edge.</param>
        public SafeAreaInsets(float left, float right, float bottom, float top)
        {
            Left = Mathf.Max(0f, left);
            Right = Mathf.Max(0f, right);
            Bottom = Mathf.Max(0f, bottom);
            Top = Mathf.Max(0f, top);
        }

        /// <summary>
        /// Converts to pixel insets for the given screen size.
        /// </summary>
        /// <param name="screenWidth">Screen width in pixels.</param>
        /// <param name="screenHeight">Screen height in pixels.</param>
        /// <returns>Left, right, bottom and top insets in pixels.</returns>
        public (float Left, float Right, float Bottom, float Top) ToPixels(float screenWidth, float screenHeight)
            => (Left * screenWidth, Right * screenWidth, Bottom * screenHeight, Top * screenHeight);

        /// <summary>
        /// Returns these insets with the larger value of each opposing pair applied to both.
        /// </summary>
        /// <param name="horizontal">Balance the left and right edges.</param>
        /// <param name="vertical">Balance the bottom and top edges.</param>
        /// <remarks>
        /// A landscape notch obscures one side only, which shifts a centred layout off-centre.
        /// Mirroring the larger inset onto the opposite edge trades a little screen space for
        /// keeping the composition centred — the same trade uGUI 2.6's SafeArea calls
        /// "balance".
        /// </remarks>
        public SafeAreaInsets Balanced(bool horizontal, bool vertical)
        {
            var left = Left;
            var right = Right;
            var bottom = Bottom;
            var top = Top;

            if (horizontal)
            {
                left = right = Mathf.Max(Left, Right);
            }

            if (vertical)
            {
                bottom = top = Mathf.Max(Bottom, Top);
            }

            return new SafeAreaInsets(left, right, bottom, top);
        }

        /// <summary>
        /// Returns these insets with the unselected edges zeroed.
        /// </summary>
        /// <param name="edges">Edges to keep.</param>
        public SafeAreaInsets Masked(SafeAreaEdges edges) => new(
            edges.HasFlag(SafeAreaEdges.Left) ? Left : 0f,
            edges.HasFlag(SafeAreaEdges.Right) ? Right : 0f,
            edges.HasFlag(SafeAreaEdges.Bottom) ? Bottom : 0f,
            edges.HasFlag(SafeAreaEdges.Top) ? Top : 0f);

        /// <inheritdoc />
        public bool Equals(SafeAreaInsets other)
            => Mathf.Approximately(Left, other.Left)
               && Mathf.Approximately(Right, other.Right)
               && Mathf.Approximately(Bottom, other.Bottom)
               && Mathf.Approximately(Top, other.Top);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is SafeAreaInsets other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Left, Right, Bottom, Top);

        /// <inheritdoc />
        public override string ToString()
            => $"SafeAreaInsets(L:{Left:F3} R:{Right:F3} B:{Bottom:F3} T:{Top:F3})";

        /// <summary>
        /// Compares two insets for equality.
        /// </summary>
        public static bool operator ==(SafeAreaInsets left, SafeAreaInsets right) => left.Equals(right);

        /// <summary>
        /// Compares two insets for inequality.
        /// </summary>
        public static bool operator !=(SafeAreaInsets left, SafeAreaInsets right) => !left.Equals(right);
    }

    /// <summary>
    /// Screen edges a safe-area inset can be applied to.
    /// </summary>
    [Flags]
    public enum SafeAreaEdges
    {
        /// <summary>
        /// No edge is inset.
        /// </summary>
        None = 0,

        /// <summary>
        /// Inset the left edge.
        /// </summary>
        Left = 1 << 0,

        /// <summary>
        /// Inset the right edge.
        /// </summary>
        Right = 1 << 1,

        /// <summary>
        /// Inset the bottom edge.
        /// </summary>
        Bottom = 1 << 2,

        /// <summary>
        /// Inset the top edge.
        /// </summary>
        Top = 1 << 3,

        /// <summary>
        /// Inset the left and right edges.
        /// </summary>
        Horizontal = Left | Right,

        /// <summary>
        /// Inset the bottom and top edges.
        /// </summary>
        Vertical = Bottom | Top,

        /// <summary>
        /// Inset every edge.
        /// </summary>
        All = Left | Right | Bottom | Top,
    }
}
