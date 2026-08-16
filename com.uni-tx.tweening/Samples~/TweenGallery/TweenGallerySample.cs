using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Tweening;
using UnityEngine;

namespace UniTx.Tweening.Samples
{
    /// <summary>
    /// Every tween type, plus sequencing and cancellation patterns.
    /// </summary>
    /// <remarks>
    /// Drop on a GameObject, optionally assign a CanvasGroup, and press Play.
    /// </remarks>
    public sealed class TweenGallerySample : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private CanvasGroup _panel;
        [SerializeField] private SpriteRenderer _sprite;

        private CancellationTokenSource _cts;

        // Tie every tween to the object's lifetime. A tween whose target is destroyed
        // mid-flight otherwise keeps writing to a dead transform.
        private CancellationToken Token => _cts.Token;

        private void Awake()
        {
            if (_target == null) _target = transform;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void Start() => RunGalleryAsync().Forget();

        private async UniTaskVoid RunGalleryAsync()
        {
            try
            {
                await SequentialAsync();
                await ParallelAsync();
                await CustomValueAsync();
                await FeedbackAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected when the object is destroyed mid-tween.
            }
        }

        /// <summary>
        /// Sequencing is just await. There is no sequence type to learn.
        /// </summary>
        private async UniTask SequentialAsync()
        {
            var origin = _target.position;

            await UniTween.MoveAsync(_target, origin + Vector3.right * 3f, 0.5f, Ease.OutCubic, cToken: Token);
            await UniTween.MoveAsync(_target, origin + Vector3.up * 2f, 0.5f, Ease.InOutQuad, cToken: Token);
            await UniTween.MoveAsync(_target, origin, 0.5f, Ease.OutBack, cToken: Token);

            await UniTween.RotateAsync(_target, Quaternion.Euler(0f, 180f, 0f), 0.4f, cToken: Token);
            await UniTween.RotateAsync(_target, Quaternion.identity, 0.4f, cToken: Token);
        }

        /// <summary>
        /// Running tweens together is UniTask.WhenAll — the usual "grow and fade in" combo.
        /// </summary>
        private async UniTask ParallelAsync()
        {
            if (_panel != null) _panel.alpha = 0f;
            _target.localScale = Vector3.zero;

            await UniTask.WhenAll(
                UniTween.ScaleAsync(_target, Vector3.one, 0.35f, Ease.OutBack, cToken: Token),
                _panel != null
                    // unscaledTime: menu animations must still run while the game is paused
                    // via Time.timeScale = 0.
                    ? UniTween.FadeAsync(_panel, 1f, 0.35f, Ease.OutQuad, unscaledTime: true, cToken: Token)
                    : UniTask.CompletedTask);
        }

        /// <summary>
        /// ValueAsync covers anything without a dedicated overload.
        /// </summary>
        private async UniTask CustomValueAsync()
        {
            // A score counter that rolls up rather than snapping.
            await UniTween.ValueAsync(0f, 1250f, 0.8f,
                value => Debug.Log($"Score: {Mathf.RoundToInt(value)}"),
                Ease.OutCubic, cToken: Token);

            if (_sprite != null)
            {
                await UniTween.ColorAsync(_sprite, Color.red, 0.2f, cToken: Token);
                await UniTween.ColorAsync(_sprite, Color.white, 0.2f, cToken: Token);
            }
        }

        /// <summary>
        /// Punch and shake restore the original transform even if cancelled at their peak.
        /// </summary>
        private async UniTask FeedbackAsync()
        {
            await UniTween.PunchScaleAsync(_target, strength: 0.25f, duration: 0.3f, cToken: Token);
            await UniTween.ShakeAsync(_target, strength: 0.15f, duration: 0.4f, cToken: Token);
        }

        /// <summary>
        /// Cancelling one tween to start another — the standard "retarget" case.
        /// </summary>
        [ContextMenu("Interrupt With New Tween")]
        public void InterruptWithNewTween()
        {
            // Cancel the in-flight token, then issue a fresh one. Without this the old tween
            // keeps writing to the transform and the two fight each other frame by frame.
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            UniTween.MoveAsync(_target, _target.position + Vector3.forward * 2f, 0.4f,
                Ease.OutElastic, cToken: Token).Forget();
        }
    }
}
