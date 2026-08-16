using NUnit.Framework;
using UnityEngine;

namespace UniTx.Tweening.Tests.EditMode
{
    public class EasingTests
    {
        private static readonly Ease[] AllEases = (Ease[])System.Enum.GetValues(typeof(Ease));

        [Test]
        public void Evaluate_AtZero_IsZero([ValueSource(nameof(AllEases))] Ease ease)
            => Assert.That(Easing.Evaluate(ease, 0f), Is.EqualTo(0f).Within(0.0001f));

        [Test]
        public void Evaluate_AtOne_IsOne([ValueSource(nameof(AllEases))] Ease ease)
            => Assert.That(Easing.Evaluate(ease, 1f), Is.EqualTo(1f).Within(0.0001f));

        [Test]
        public void Evaluate_ClampsInputOutsideZeroToOne([ValueSource(nameof(AllEases))] Ease ease)
        {
            // A tween's accumulated delta time routinely overshoots the duration slightly;
            // clamping keeps the final frame landing exactly on the target.
            Assert.That(Easing.Evaluate(ease, -5f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(Easing.Evaluate(ease, 5f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Evaluate_NeverReturnsNaN([ValueSource(nameof(AllEases))] Ease ease)
        {
            for (var i = 0; i <= 100; i++)
            {
                var value = Easing.Evaluate(ease, i / 100f);

                Assert.IsFalse(float.IsNaN(value), $"{ease} produced NaN at t={i / 100f}");
                Assert.IsFalse(float.IsInfinity(value), $"{ease} produced infinity at t={i / 100f}");
            }
        }

        [Test]
        public void Linear_IsIdentity()
        {
            Assert.That(Easing.Evaluate(Ease.Linear, 0.25f), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(Easing.Evaluate(Ease.Linear, 0.75f), Is.EqualTo(0.75f).Within(0.0001f));
        }

        [Test]
        public void InQuad_StartsSlowerThanLinear()
            => Assert.Less(Easing.Evaluate(Ease.InQuad, 0.25f), 0.25f);

        [Test]
        public void OutQuad_StartsFasterThanLinear()
            => Assert.Greater(Easing.Evaluate(Ease.OutQuad, 0.25f), 0.25f);

        [Test]
        public void OutBack_OvershootsPastOne()
        {
            // The overshoot is the whole point of Back easing; a version clamped to 0..1
            // would look identical to OutQuad.
            var peak = 0f;

            for (var i = 0; i <= 100; i++)
            {
                peak = Mathf.Max(peak, Easing.Evaluate(Ease.OutBack, i / 100f));
            }

            Assert.Greater(peak, 1f);
        }

        [Test]
        public void Evaluate_IsMonotonicForNonOvershootingCurves(
            [Values(Ease.Linear, Ease.InQuad, Ease.OutQuad, Ease.InOutQuad,
                Ease.InCubic, Ease.OutCubic, Ease.InOutCubic)] Ease ease)
        {
            var previous = float.NegativeInfinity;

            for (var i = 0; i <= 100; i++)
            {
                var value = Easing.Evaluate(ease, i / 100f);

                Assert.GreaterOrEqual(value, previous - 0.0001f, $"{ease} went backwards at t={i / 100f}");
                previous = value;
            }
        }

        [Test]
        public void UnknownEase_FallsBackToLinear()
            => Assert.That(Easing.Evaluate((Ease)999, 0.4f), Is.EqualTo(0.4f).Within(0.0001f));
    }
}
