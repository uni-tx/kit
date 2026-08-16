using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;
using UniTx.Pooling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UniTx.Audio
{
    /// <summary>
    /// Default <see cref="IAudioService"/>: pooled SFX plus a dedicated music source.
    /// </summary>
    internal sealed class UniAudioService : IAudioService
    {
        private const int DefaultSfxCapacity = 8;

        /// <remarks>
        /// Android and iOS mix a limited number of simultaneous voices — Unity's default
        /// Max Real Voices is 32, and anything past it is virtualized or dropped. Letting
        /// the pool grow to <see cref="UniSpawner"/>'s default of 1000 would allocate
        /// hundreds of AudioSource GameObjects that can never all be heard, costing memory
        /// and per-frame work for nothing. Above this, released sources are destroyed
        /// rather than retained.
        /// </remarks>
        private const int MaxSfxVoices = 24;

        private readonly List<UniAudioSource> _sfxBuffer = new();

        private UniSpawner _spawner;
        private UniAudioSource _musicSource;
        private GameObject _root;
        private CancellationTokenSource _musicFadeCts;

        /// <inheritdoc/>
        public event Action<bool> OnSfxMutedChanged;

        /// <inheritdoc/>
        public event Action<float> OnSfxVolumeChanged;

        /// <inheritdoc/>
        public event Action<bool> OnMusicMutedChanged;

        /// <inheritdoc/>
        public event Action<float> OnMusicVolumeChanged;

        /// <inheritdoc/>
        public bool IsSfxMuted { get; private set; }

        /// <inheritdoc/>
        public float SfxVolume { get; private set; } = 1f;

        /// <inheritdoc/>
        public bool IsMusicMuted { get; private set; }

        /// <inheritdoc/>
        public float MusicVolume { get; private set; } = 1f;

        /// <inheritdoc/>
        public void SetMuteSfx(bool mute)
        {
            if (IsSfxMuted == mute) return;

            IsSfxMuted = mute;

            foreach (var source in ActiveSfx())
            {
                source.SetMute(mute);
            }

            OnSfxMutedChanged.SafeInvoke(mute);
        }

        /// <inheritdoc/>
        public void SetSfxVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);

            if (Mathf.Approximately(SfxVolume, volume)) return;

            SfxVolume = volume;

            // Each source recomputes from its clip's own volume. The previous version
            // multiplied each source's *current* volume by the new global value, so every
            // call compounded and repeated adjustments faded everything to silence.
            foreach (var source in ActiveSfx())
            {
                source.SetBusVolume(SfxVolume);
            }

            OnSfxVolumeChanged.SafeInvoke(SfxVolume);
        }

        /// <inheritdoc/>
        public void SetMuteMusic(bool mute)
        {
            if (IsMusicMuted == mute) return;

            IsMusicMuted = mute;
            _musicSource?.SetMute(mute);
            OnMusicMutedChanged.SafeInvoke(mute);
        }

        /// <inheritdoc/>
        public void SetMusicVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);

            if (Mathf.Approximately(MusicVolume, volume)) return;

            MusicVolume = volume;
            _musicSource?.SetBusVolume(MusicVolume);
            OnMusicVolumeChanged.SafeInvoke(MusicVolume);
        }

        /// <inheritdoc/>
        public UniTask InitializeAsync(CancellationToken cToken = default)
        {
            cToken.ThrowIfCancellationRequested();

            _root = new GameObject("[UniTx] Audio") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(_root);

            var sfxPrefab = new GameObject("UniAudioSource_SFX").AddComponent<UniAudioSource>();
            sfxPrefab.transform.SetParent(_root.transform);
            sfxPrefab.gameObject.SetActive(false);

            _spawner = new UniSpawner(sfxPrefab, _root.transform, DefaultSfxCapacity, MaxSfxVoices);

            if (IoCStatics.IsInitialized) _spawner.Inject(IoCStatics.Resolver);

            _spawner.Prewarm(DefaultSfxCapacity);

            var musicObject = new GameObject("UniAudioSource_Music");
            musicObject.transform.SetParent(_root.transform);
            _musicSource = musicObject.AddComponent<UniAudioSource>();

            if (IoCStatics.IsInitialized) _musicSource.Inject(IoCStatics.Resolver);

            return UniTask.CompletedTask;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _musicFadeCts.SafeCancelAndDispose();
            _musicFadeCts = null;

            _spawner?.Dispose();
            _spawner = null;

            _musicSource?.Reset();
            _musicSource = null;

            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
        }

        /// <inheritdoc/>
        public void Play2D(IAudioConfig config) => PlaySfx(config, 0f, Vector3.zero, null);

        /// <inheritdoc/>
        public void Play3D(IAudioConfig config, Vector3 position) => PlaySfx(config, 1f, position, null);

        /// <inheritdoc/>
        public void PlayAttached(IAudioConfig config, Transform parent)
        {
            if (parent == null)
            {
                UniStatics.LogWarning("PlayAttached called with a null transform; playing in 2D instead.", this);
                Play2D(config);
                return;
            }

            PlaySfx(config, 1f, parent.position, parent);
        }

        /// <inheritdoc/>
        public void PlayMusic(IAudioConfig config)
        {
            if (!TryPrepare(config, out var data)) return;

            _musicFadeCts.SafeCancelAndDispose();
            _musicFadeCts = null;

            data.IsMusic = true;
            data.SpatialBlend = 0f;
            data.BusVolume = MusicVolume;
            data.IsMuted = IsMusicMuted;

            _musicSource.Reset();
            _musicSource.SetData(data);
            _musicSource.Initialize();
        }

        /// <inheritdoc/>
        public async UniTask PlayMusicAsync(IAudioConfig config, float fadeDuration,
            CancellationToken cToken = default)
        {
            if (fadeDuration <= 0f)
            {
                PlayMusic(config);
                return;
            }

            _musicFadeCts.SafeCancelAndDispose();
            _musicFadeCts = CancellationTokenSource.CreateLinkedTokenSource(cToken);
            var token = _musicFadeCts.Token;

            if (_musicSource != null && _musicSource.IsPlaying)
            {
                await _musicSource.FadeAsync(0f, fadeDuration * 0.5f, token);
            }

            PlayMusic(config);
            _musicSource.SetBusVolume(0f);

            await _musicSource.FadeAsync(MusicVolume, fadeDuration * 0.5f, token);
        }

        /// <inheritdoc/>
        public void StopMusic()
        {
            _musicFadeCts.SafeCancelAndDispose();
            _musicFadeCts = null;
            _musicSource?.Reset();
        }

        /// <inheritdoc/>
        public void StopAllSfx() => _spawner?.ReturnAll();

        /// <inheritdoc/>
        public void PauseAll()
        {
            _musicSource?.Pause();

            foreach (var source in ActiveSfx())
            {
                source.Pause();
            }
        }

        /// <inheritdoc/>
        public void ResumeAll()
        {
            _musicSource?.Resume();

            foreach (var source in ActiveSfx())
            {
                source.Resume();
            }
        }

        private void PlaySfx(IAudioConfig config, float spatialBlend, Vector3 position, Transform follow)
        {
            if (_spawner == null)
            {
                UniStatics.LogWarning("Audio service is not initialized; ignoring playback request.", this);
                return;
            }

            if (!TryPrepare(config, out var data)) return;

            data.SpatialBlend = spatialBlend;
            data.ToFollow = follow;
            data.BusVolume = SfxVolume;
            data.IsMuted = IsSfxMuted;

            _spawner.Spawn<UniAudioSource>(data, position,
                follow != null ? follow.rotation : Quaternion.identity);
        }

        private bool TryPrepare(IAudioConfig config, out UniAudioConfigData data)
        {
            data = null;

            if (config?.Data == null)
            {
                UniStatics.LogWarning("Audio config or its data is null; ignoring playback request.", this);
                return false;
            }

            // Clone so per-playback values (spatial blend, follow target) never write back
            // into the shared ScriptableObject.
            data = (UniAudioConfigData)config.Data.Clone();
            return true;
        }

        private IReadOnlyList<UniAudioSource> ActiveSfx()
        {
            _sfxBuffer.Clear();

            if (_spawner == null) return _sfxBuffer;

            foreach (var item in _spawner.ActiveItems)
            {
                if (item is UniAudioSource source) _sfxBuffer.Add(source);
            }

            return _sfxBuffer;
        }
    }
}
