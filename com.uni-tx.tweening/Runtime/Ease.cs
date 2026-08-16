namespace UniTx.Tweening
{
    /// <summary>
    /// Easing curve applied to a tween's normalized progress.
    /// </summary>
    public enum Ease
    {
        /// <summary>
        /// Constant rate. Correct for continuous motion, lifeless for UI.
        /// </summary>
        Linear = 0,

        /// <summary>
        /// Accelerates from rest. Good for something leaving the screen.
        /// </summary>
        InQuad,

        /// <summary>
        /// Decelerates to rest. The safe default for something arriving.
        /// </summary>
        OutQuad,

        /// <summary>
        /// Accelerates, then decelerates.
        /// </summary>
        InOutQuad,

        /// <summary>
        /// Sharper acceleration than <see cref="InQuad"/>.
        /// </summary>
        InCubic,

        /// <summary>
        /// Sharper deceleration than <see cref="OutQuad"/>.
        /// </summary>
        OutCubic,

        /// <summary>
        /// Sharper acceleration and deceleration than <see cref="InOutQuad"/>.
        /// </summary>
        InOutCubic,

        /// <summary>
        /// Pulls back before moving forward.
        /// </summary>
        InBack,

        /// <summary>
        /// Overshoots the target then settles. Reads as "snappy" on UI.
        /// </summary>
        OutBack,

        /// <summary>
        /// Pulls back, overshoots, then settles.
        /// </summary>
        InOutBack,

        /// <summary>
        /// Overshoots and oscillates to rest.
        /// </summary>
        OutElastic,

        /// <summary>
        /// Bounces on arrival.
        /// </summary>
        OutBounce,
    }
}
