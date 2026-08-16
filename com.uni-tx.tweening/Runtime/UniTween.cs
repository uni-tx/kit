using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniTx.Tweening
{
    /// <summary>
    /// Awaitable tweens for transforms, colors, alpha and arbitrary values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every tween is a plain awaitable, so sequencing and parallelism come from the
    /// language rather than a bespoke sequence API:
    /// </para>
    /// <code>
    /// await UniTween.MoveAsync(t, target, 0.3f, Ease.OutBack, cToken: token);   // then
    /// await UniTask.WhenAll(                                                    // together
    ///     UniTween.ScaleAsync(t, Vector3.one, 0.2f, cToken: token),
    ///     UniTween.FadeAsync(group, 1f, 0.2f, cToken: token));
    /// </code>
    /// <para>
    /// Tweens run on UniTask's pooled state machines, so a running tween allocates only its
    /// captured state — no per-frame garbage. Always pass a token
    /// (<c>this.GetCancellationTokenOnDestroy()</c>); a tween whose target is destroyed
    /// mid-flight otherwise throws when it next writes to the transform.
    /// </para>
    /// </remarks>
    public static class UniTween
    {
        /// <summary>
        /// Moves a transform to a world position.
        /// </summary>
        /// <param name="target">The transform to move.</param>
        /// <param name="to">Destination position.</param>
        /// <param name="duration">Duration in seconds.</param>
        /// <param name="ease">Easing curve.</param>
        /// <param name="unscaledTime">Ignore <see cref="Time.timeScale"/>, e.g. for pause-menu UI.</param>
        /// <param name="cToken">Token to cancel the tween.</param>
        public static UniTask MoveAsync(Transform target, Vector3 to, float duration,
            Ease ease = Ease.OutQuad, bool unscaledTime = false, CancellationToken cToken = default)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var from = target.position;

            return RunAsync(duration, ease, unscaledTime, cToken,
                t =>
                {
                    if (target != null) target.position = Vector3.LerpUnclamped(from, to, t);
                });
        }

        /// <summary>
        /// Moves a transform to a position relative to its parent.
        /// </summary>
        /// <param name="target">The transform to move.</param>
        /// <param name="to">Destination local position.</param>
        /// <param name="duration">Duration in seconds.</param>
        /// <param name="ease">Easing curve.</param>
        /// <param name="unscaledTime">Ignore <see cref="Time.timeScale"/>.</param>
        /// <param name="cToken">Token to cancel the tween.</param>
        public static UniTask MoveLocalAsync(Transform target, Vector3 to, float duration,
            Ease ease = Ease.OutQuad, bool unscaledTime = false, CancellationToken cToken = default)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var from = target.localPosition;

            return RunAsync(duration, ease, unscaledTime, cToken,
                t =>
                {
                    if (target != null) target.localPosition = Vector3.LerpUnclamped(from, to, t);
                });
        }

        /// <summary>
        /// Scales a transform.
        /// </summary>
        /// <param name="target">The transform to scale.</param>
        /// <param name="to">Destination local scale.</param>
        /// <param name="duration">Duration in seconds.</param>
        /// <param name="ease">Easing curve.</param>
        /// <param name="unscaledTime">Ignore <see cref="Time.timeScale"/>.</param>
        /// <param name="cToken">Token to cancel the tween.</param>
        public static UniTask ScaleAsync(Transform target, Vector3 to, float duration,
            Ease ease = Ease.OutBack, bool unscaledTime = false, CancellationToken cToken = default)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var from = target.localScale;

            return RunAsync(duration, ease, unscaledTime, cToken,
                t =>
                {
                    if (target != null) target.localScale = Vector3.LerpUnclamped(from, to, t);
                });
        }

        /// <summary>
        /// Rotates a transform to a local rotation.
        /// </summary>
        /// <param name="target">The transform to rotate.</param>
        /// <param name="to">Destination local rotation.</param>
        /// <param name="duration">Duration in seconds.</param>
        /// <param name="ease">Easing curve.</param>
        /// <param name="unscaledTime">Ignore <see cref="Time.timeScale"/>.</param>
        /// <param name="cToken">Token to cancel the tween.</param>
        public static UniTask RotateAsync(Transform target, Quaternion to, float duration,
            Ease ease = Ease.OutQuad, bool unscaledTime = false, CancellationToken cToken = default)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var from = target.localRotation;

            return RunAsync(duration, ease, unscaledTime, cToken,
                t =>
                {
                    // Slerp, not Lerp: linear interpolation of a quaternion changes angular
                    // speed through the arc and reads as a stutter on long rotations.
                    if (target != null) target.localRotation = Quaternion.SlerpUnclamped(from, to, t);
                });
        }

        /// <summary>
        /// Fades a <see cref="CanvasGroup"/>'s alpha.
        /// </summary>
        /// <param name="target">The canvas group to fade.</param>
        /// <param name="to">Destination alpha, 0..1.</param>
        /// <param name="duration">Duration in seconds.</param>
        /// <param name="ease">Easing curve.</param>
        /// <param name="unscaledTime">Ignore <see cref="Time.timeScale"/>.</param>
        /// <param name="cToken">Token to cancel the tween.</param>
        /// <remarks>
        /// A CanvasGroup fades a whole subtree in one write. Fading each Graphic separately
        /// dirties every one of them and rebuilds the canvas each frame.
        /// </remarks>
        public static UniTask FadeAsync(CanvasGroup target, float to, float duration,
            Ease ease = Ease.OutQuad, bool unscaledTime = false, CancellationToken cToken = default)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var from = target.alpha;
            to = Mathf.Clamp01(to);

            return RunAsync(duration, ease, unscaledTime, cToken,
                t =>
                {
                    if (target != null) target.alpha = Mathf.LerpUnclamped(from, to, t);
                });
        }

        /// <summary>
        /// Tweens a <see cref="SpriteRenderer"/>'s color.
        /// </summary>
        /// <param name="target">The renderer to tint.</param>
        /// <param name="to">Destination color.</param>
        /// <param name="duration">Duration in seconds.</param>
        /// <param name="ease">Easing curve.</param>
        /// <param name="unscaledTime">Ignore <see cref="Time.timeScale"/>.</param>
        /// <param name="cToken">Token to cancel the tween.</param>
        public static UniTask ColorAsync(SpriteRenderer target, Color to, float duration,
            Ease ease = Ease.Linear, bool unscaledTime = false, CancellationToken cToken = default)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var from = target.color;

            return RunAsync(duration, ease, unscaledTime, cToken,
                t =>
                {
                    if (target != null) target.color = Color.LerpUnclamped(from, to, t);
                });
        }

        /// <summary>
        /// Tweens a float and reports it, for anything without a dedicated overload.
        /// </summary>
        /// <param name="from">Start value.</param>
        /// <param name="to">End value.</param>
        /// <param name="duration">Duration in seconds.</param>
        /// <param name="onUpdate">Receives the interpolated value each frame.</param>
        /// <param name="ease">Easing curve.</param>
        /// <param name="unscaledTime">Ignore <see cref="Time.timeScale"/>.</param>
        /// <param name="cToken">Token to cancel the tween.</param>
        /// <remarks>Use this for score counters, audio levels, shader properties and fills.</remarks>
        public static UniTask ValueAsync(float from, float to, float duration, Action<float> onUpdate,
            Ease ease = Ease.Linear, bool unscaledTime = false, CancellationToken cToken = default)
        {
            if (onUpdate == null) throw new ArgumentNullException(nameof(onUpdate));

            return RunAsync(duration, ease, unscaledTime, cToken,
                t => onUpdate(Mathf.LerpUnclamped(from, to, t)));
        }

        /// <summary>
        /// Scales up and back down, for a button press or a pickup pop.
        /// </summary>
        /// <param name="target">The transform to punch.</param>
        /// <param name="strength">Peak scale offset, e.g. 0.2 for +20%.</param>
        /// <param name="duration">Total duration in seconds.</param>
        /// <param name="unscaledTime">Ignore <see cref="Time.timeScale"/>.</param>
        /// <param name="cToken">Token to cancel the tween.</param>
        public static async UniTask PunchScaleAsync(Transform target, float strength = 0.2f,
            float duration = 0.25f, bool unscaledTime = false, CancellationToken cToken = default)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var original = target.localScale;
            var half = duration * 0.5f;

            try
            {
                await ScaleAsync(target, original * (1f + strength), half, Ease.OutQuad, unscaledTime, cToken);
                await ScaleAsync(target, original, half, Ease.OutBack, unscaledTime, cToken);
            }
            finally
            {
                // Restore on cancel too, or a punch interrupted at its peak leaves the
                // object permanently oversized.
                if (target != null) target.localScale = original;
            }
        }

        /// <summary>
        /// Shakes a transform around its current local position.
        /// </summary>
        /// <param name="target">The transform to shake.</param>
        /// <param name="strength">Maximum offset in units.</param>
        /// <param name="duration">Duration in seconds.</param>
        /// <param name="unscaledTime">Ignore <see cref="Time.timeScale"/>.</param>
        /// <param name="cToken">Token to cancel the tween.</param>
        public static async UniTask ShakeAsync(Transform target, float strength = 0.2f,
            float duration = 0.3f, bool unscaledTime = false, CancellationToken cToken = default)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            var origin = target.localPosition;

            try
            {
                await RunAsync(duration, Ease.Linear, unscaledTime, cToken,
                    t =>
                    {
                        if (target == null) return;

                        // Damped so the shake settles instead of stopping abruptly.
                        var damping = 1f - t;
                        target.localPosition = origin + (Vector3)UnityEngine.Random.insideUnitCircle
                            * (strength * damping);
                    });
            }
            finally
            {
                if (target != null) target.localPosition = origin;
            }
        }

        private static async UniTask RunAsync(float duration, Ease ease, bool unscaledTime,
            CancellationToken cToken, Action<float> apply)
        {
            if (duration <= 0f)
            {
                apply(1f);
                return;
            }

            var elapsed = 0f;

            while (elapsed < duration)
            {
                cToken.ThrowIfCancellationRequested();

                elapsed += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                apply(Easing.Evaluate(ease, Mathf.Clamp01(elapsed / duration)));

                await UniTask.Yield(PlayerLoopTiming.Update, cToken);
            }

            // Land exactly on the target: accumulated delta time almost never sums to the
            // duration precisely, which otherwise leaves objects a fraction short.
            apply(Easing.Evaluate(ease, 1f));
        }
    }
}
