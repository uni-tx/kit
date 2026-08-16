using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;
using UniTx.Pooling;
using UnityEngine;

namespace UniTx.Audio
{
    /// <summary>
    /// Pooled <see cref="AudioSource"/> wrapper driven by <see cref="UniAudioConfigData"/>.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("")]
    internal sealed class UniAudioSource : MonoBehaviour, IInjectable, IPoolItem<UniAudioConfigData>
    {
        private AudioSource _source;
        private IPoolItemReturner _returner;
        private IUnityEventListener _eventListener;
        private Transform _toFollow;
        private CancellationTokenSource _playCts;
        private float _busVolume = 1f;
        private bool _isPaused;

        /// <inheritdoc />
        public UniAudioConfigData Data { get; private set; }

        /// <inheritdoc />
        public GameObject GameObject => gameObject;

        /// <inheritdoc />
        public Transform Transform => transform;

        /// <summary>
        /// Gets the clip's own volume, before the bus level is applied.
        /// </summary>
        public float BaseVolume => Data?.Volume ?? 0f;

        /// <summary>
        /// Indicates whether the source is currently playing.
        /// </summary>
        public bool IsPlaying => _source != null && _source.isPlaying;

        /// <inheritdoc />
        public void SetData(IPoolItemData data) => Data = (UniAudioConfigData)data;

        /// <inheritdoc />
        public void SetPoolItemReturner(IPoolItemReturner returner) => _returner = returner;

        /// <inheritdoc />
        public void Inject(IResolver resolver)
        {
            // The listener is optional: following a moving target is a feature, not a
            // requirement, and a pool created before the listener is bound must still work.
            if (resolver != null) resolver.TryResolve(out _eventListener);
        }

        /// <summary>
        /// Applies the settings in <see cref="Data"/> and starts playback.
        /// </summary>
        public void Initialize()
        {
            if (Data?.Clip == null)
            {
                UniStatics.LogWarning("Audio config has no clip assigned; nothing to play.", this);
                return;
            }

            // Only subscribe when there is actually something to follow — every pooled SFX
            // adding a LateUpdate delegate is measurable when hundreds play per second.
            _toFollow = Data.ToFollow;

            if (_toFollow != null && _eventListener != null) _eventListener.OnLateUpdate += FollowTarget;

            _busVolume = Mathf.Clamp01(Data.BusVolume);

            _source.clip = Data.Clip;
            _source.mute = Data.IsMuted;
            _source.volume = Data.Volume * _busVolume;
            _source.pitch = Data.Pitch;
            _source.loop = Data.Loop;
            _source.spatialBlend = Data.SpatialBlend;
            _source.minDistance = Data.MinDistance;
            _source.maxDistance = Data.MaxDistance;
            _source.outputAudioMixerGroup = Data.MixerGroup;
            _source.Play();

            if (Data.Loop) return;

            _playCts.SafeCancelAndDispose();
            _playCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            ReturnWhenFinishedAsync(Data.PlaybackDuration, _playCts.Token).Forget();
        }

        /// <summary>
        /// Stops playback and clears state so the instance can be reused.
        /// </summary>
        public void Reset()
        {
            if (_toFollow != null && _eventListener != null) _eventListener.OnLateUpdate -= FollowTarget;

            _playCts.SafeCancelAndDispose();
            _playCts = null;
            _toFollow = null;

            // Cleared here as well as on Resume: a source returned to the pool while the app
            // was backgrounded would otherwise come back paused and never count down.
            _isPaused = false;

            if (Data != null) Data.ToFollow = null;

            if (_source != null)
            {
                _source.Stop();
                _source.clip = null;
                _source.outputAudioMixerGroup = null;
            }
        }

        /// <inheritdoc />
        public void Return() => _returner?.Return(this);

        /// <summary>
        /// Sets the bus level applied on top of the clip's own volume.
        /// </summary>
        /// <param name="busVolume">Normalized bus volume, 0..1.</param>
        /// <remarks>
        /// Recomputes from <see cref="BaseVolume"/> rather than scaling the current value,
        /// so repeated volume changes cannot compound the source towards silence.
        /// </remarks>
        public void SetBusVolume(float busVolume)
        {
            _busVolume = Mathf.Clamp01(busVolume);

            if (Data != null) Data.BusVolume = _busVolume;
            if (_source != null && Data != null) _source.volume = Data.Volume * _busVolume;
        }

        /// <summary>
        /// Mutes or unmutes this source.
        /// </summary>
        /// <param name="mute">Whether the source should be silent.</param>
        public void SetMute(bool mute)
        {
            if (Data != null) Data.IsMuted = mute;
            if (_source != null) _source.mute = mute;
        }

        /// <summary>
        /// Pauses playback, keeping the playhead.
        /// </summary>
        public void Pause()
        {
            _isPaused = true;
            _source?.Pause();
        }

        /// <summary>
        /// Resumes playback from where it was paused.
        /// </summary>
        public void Resume()
        {
            _isPaused = false;
            _source?.UnPause();
        }

        /// <summary>
        /// Fades the bus level to a target over a duration.
        /// </summary>
        /// <param name="target">Target bus volume, 0..1.</param>
        /// <param name="duration">Fade duration in seconds.</param>
        /// <param name="cToken">Token to cancel the fade.</param>
        public async UniTask FadeAsync(float target, float duration, CancellationToken cToken = default)
        {
            target = Mathf.Clamp01(target);

            if (duration <= 0f || _source == null)
            {
                SetBusVolume(target);
                return;
            }

            var start = _busVolume;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                cToken.ThrowIfCancellationRequested();
                // Unscaled: audio fades must not stall when the game pauses via timeScale.
                elapsed += Time.unscaledDeltaTime;
                SetBusVolume(Mathf.Lerp(start, target, elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, cToken);
            }

            SetBusVolume(target);
        }

        private async UniTaskVoid ReturnWhenFinishedAsync(float duration, CancellationToken cToken)
        {
            try
            {
                // Counted down per frame rather than as a single Delay, so the countdown can
                // freeze while paused. A plain Delay kept running when the app was
                // backgrounded, so a paused effect was returned to the pool and simply gone
                // on resume.
                //
                // Not polling AudioSource.isPlaying instead: it reads false on the frame
                // Play() is called *and* while paused, so it cannot distinguish "finished"
                // from "not started yet" or "suspended".
                var remaining = duration;

                while (remaining > 0f)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cToken);

                    if (_isPaused) continue;

                    // Unscaled: a sound effect should not stretch because the game slowed
                    // time down or paused via timeScale.
                    remaining -= Time.unscaledDeltaTime;
                }

                Return();
            }
            catch (OperationCanceledException)
            {
                // Stopped early or the object was destroyed — nothing to do.
            }
        }

        private void FollowTarget()
        {
            if (_toFollow == null) return;

            transform.SetPositionAndRotation(_toFollow.position, _toFollow.rotation);
        }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        private void OnDestroy() => Reset();
    }
}
