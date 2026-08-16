using System.Runtime.CompilerServices;
using UnityEngine;

namespace UniTx.Tweening
{
    /// <summary>
    /// Evaluates <see cref="Ease"/> curves.
    /// </summary>
    /// <remarks>
    /// Closed-form maths rather than <see cref="AnimationCurve"/>: an AnimationCurve is a
    /// managed object that must be serialized, copied per tween and sampled through a
    /// managed call, which is measurably worse when hundreds of UI tweens run at once.
    /// </remarks>
    public static class Easing
    {
        private const float BackOvershoot = 1.70158f;
        private const float ElasticPeriod = 0.3f;

        /// <summary>
        /// Evaluates the curve at normalized time.
        /// </summary>
        /// <param name="ease">The curve to evaluate.</param>
        /// <param name="t">Normalized progress; clamped to 0..1.</param>
        /// <returns>The eased progress. Some curves overshoot outside 0..1 by design.</returns>
        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);

            return ease switch
            {
                Ease.Linear => t,
                Ease.InQuad => t * t,
                Ease.OutQuad => 1f - (1f - t) * (1f - t),
                Ease.InOutQuad => t < 0.5f ? 2f * t * t : 1f - Pow2(-2f * t + 2f) * 0.5f,
                Ease.InCubic => t * t * t,
                Ease.OutCubic => 1f - Pow3(1f - t),
                Ease.InOutCubic => t < 0.5f ? 4f * t * t * t : 1f - Pow3(-2f * t + 2f) * 0.5f,
                Ease.InBack => (BackOvershoot + 1f) * t * t * t - BackOvershoot * t * t,
                Ease.OutBack => OutBack(t),
                Ease.InOutBack => InOutBack(t),
                Ease.OutElastic => OutElastic(t),
                Ease.OutBounce => OutBounce(t),
                _ => t,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Pow2(float v) => v * v;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Pow3(float v) => v * v * v;

        private static float OutBack(float t)
        {
            var f = t - 1f;
            return 1f + (BackOvershoot + 1f) * Pow3(f) + BackOvershoot * Pow2(f);
        }

        private static float InOutBack(float t)
        {
            const float c = BackOvershoot * 1.525f;

            return t < 0.5f
                ? Pow2(2f * t) * ((c + 1f) * 2f * t - c) * 0.5f
                : (Pow2(2f * t - 2f) * ((c + 1f) * (t * 2f - 2f) + c) + 2f) * 0.5f;
        }

        private static float OutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            const float c = 2f * Mathf.PI / ElasticPeriod;

            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c) + 1f;
        }

        private static float OutBounce(float t)
        {
            const float n = 7.5625f;
            const float d = 2.75f;

            if (t < 1f / d) return n * t * t;

            if (t < 2f / d)
            {
                t -= 1.5f / d;
                return n * t * t + 0.75f;
            }

            if (t < 2.5f / d)
            {
                t -= 2.25f / d;
                return n * t * t + 0.9375f;
            }

            t -= 2.625f / d;
            return n * t * t + 0.984375f;
        }
    }
}
