using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniTx.Audio
{
    /// <summary>
    /// Static facade over the kit's audio service.
    /// </summary>
    public static class UniAudio
    {
        private static IAudioService _audioService;

        /// <summary>
        /// Indicates whether the audio service has been initialized.
        /// </summary>
        public static bool IsInitialized => _audioService != null;

        #region SFX

        /// <summary>
        /// Raised when the SFX mute state changes.
        /// </summary>
        public static event Action<bool> OnSfxMutedChanged
        {
            add => Service.OnSfxMutedChanged += value;
            remove
            {
                if (_audioService != null) _audioService.OnSfxMutedChanged -= value;
            }
        }

        /// <summary>
        /// Raised when the SFX bus volume changes.
        /// </summary>
        public static event Action<float> OnSfxVolumeChanged
        {
            add => Service.OnSfxVolumeChanged += value;
            remove
            {
                if (_audioService != null) _audioService.OnSfxVolumeChanged -= value;
            }
        }

        /// <summary>
        /// Gets whether the SFX bus is muted.
        /// </summary>
        public static bool IsSfxMuted => _audioService?.IsSfxMuted ?? false;

        /// <summary>
        /// Gets the SFX bus volume, 0..1.
        /// </summary>
        public static float SfxVolume => _audioService?.SfxVolume ?? 1f;

        /// <summary>
        /// Mutes or unmutes the SFX bus.
        /// </summary>
        /// <param name="mute">Whether SFX should be silent.</param>
        public static void SetMuteSfx(bool mute) => Service.SetMuteSfx(mute);

        /// <summary>
        /// Sets the SFX bus volume.
        /// </summary>
        /// <param name="volume">Bus volume, clamped to 0..1.</param>
        public static void SetSfxVolume(float volume) => Service.SetSfxVolume(volume);

        #endregion

        #region Music

        /// <summary>
        /// Raised when the music mute state changes.
        /// </summary>
        public static event Action<bool> OnMusicMutedChanged
        {
            add => Service.OnMusicMutedChanged += value;
            remove
            {
                if (_audioService != null) _audioService.OnMusicMutedChanged -= value;
            }
        }

        /// <summary>
        /// Raised when the music bus volume changes.
        /// </summary>
        public static event Action<float> OnMusicVolumeChanged
        {
            add => Service.OnMusicVolumeChanged += value;
            remove
            {
                if (_audioService != null) _audioService.OnMusicVolumeChanged -= value;
            }
        }

        /// <summary>
        /// Gets whether the music bus is muted.
        /// </summary>
        public static bool IsMusicMuted => _audioService?.IsMusicMuted ?? false;

        /// <summary>
        /// Gets the music bus volume, 0..1.
        /// </summary>
        public static float MusicVolume => _audioService?.MusicVolume ?? 1f;

        /// <summary>
        /// Mutes or unmutes the music bus.
        /// </summary>
        /// <param name="mute">Whether music should be silent.</param>
        public static void SetMuteMusic(bool mute) => Service.SetMuteMusic(mute);

        /// <summary>
        /// Sets the music bus volume.
        /// </summary>
        /// <param name="volume">Bus volume, clamped to 0..1.</param>
        public static void SetMusicVolume(float volume) => Service.SetMusicVolume(volume);

        #endregion

        /// <summary>
        /// Initializes the default audio service.
        /// </summary>
        /// <param name="cToken">Token to cancel initialization.</param>
        public static UniTask InitializeAsync(CancellationToken cToken = default)
            => InitializeAsync(new UniAudioService(), cToken);

        /// <summary>
        /// Initializes with a custom audio service.
        /// </summary>
        /// <param name="audioService">The service to install.</param>
        /// <param name="cToken">Token to cancel initialization.</param>
        public static UniTask InitializeAsync(IAudioService audioService, CancellationToken cToken = default)
        {
            if (_audioService != null)
            {
                throw new InvalidOperationException(
                    "UniAudio is already initialized. Call Reset() before initializing again.");
            }

            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));

            return _audioService.InitializeAsync(cToken);
        }

        /// <summary>
        /// Stops everything and releases the audio service.
        /// </summary>
        public static void Reset()
        {
            if (_audioService == null) return;

            _audioService.Reset();
            _audioService = null;
        }

        /// <summary>
        /// Plays a sound in 2D.
        /// </summary>
        /// <param name="config">The sound to play.</param>
        public static void Play2D(IAudioConfig config) => Service.Play2D(config);

        /// <summary>
        /// Plays a sound at a world position.
        /// </summary>
        /// <param name="config">The sound to play.</param>
        /// <param name="position">Where to play it.</param>
        public static void Play3D(IAudioConfig config, Vector3 position) => Service.Play3D(config, position);

        /// <summary>
        /// Plays a sound that follows a transform while it plays.
        /// </summary>
        /// <param name="config">The sound to play.</param>
        /// <param name="parent">The transform to follow.</param>
        public static void PlayAttached(IAudioConfig config, Transform parent)
            => Service.PlayAttached(config, parent);

        /// <summary>
        /// Starts background music immediately, replacing whatever is playing.
        /// </summary>
        /// <param name="config">The music to play.</param>
        public static void PlayMusic(IAudioConfig config) => Service.PlayMusic(config);

        /// <summary>
        /// Crossfades from the current music to a new track.
        /// </summary>
        /// <param name="config">The music to play.</param>
        /// <param name="fadeDuration">Total crossfade duration in seconds.</param>
        /// <param name="cToken">Token to cancel the crossfade.</param>
        public static UniTask PlayMusicAsync(IAudioConfig config, float fadeDuration,
            CancellationToken cToken = default)
            => Service.PlayMusicAsync(config, fadeDuration, cToken);

        /// <summary>
        /// Stops background music.
        /// </summary>
        public static void StopMusic() => Service.StopMusic();

        /// <summary>
        /// Stops every playing sound effect.
        /// </summary>
        public static void StopAllSfx() => Service.StopAllSfx();

        /// <summary>
        /// Pauses music and every playing effect.
        /// </summary>
        public static void PauseAll() => _audioService?.PauseAll();

        /// <summary>
        /// Resumes everything paused by <see cref="PauseAll"/>.
        /// </summary>
        public static void ResumeAll() => _audioService?.ResumeAll();

        private static IAudioService Service => _audioService
            ?? throw new InvalidOperationException(
                "UniAudio is not initialized. Call UniAudio.InitializeAsync() first — " +
                "UniTxStep does this during bootstrap.");
    }
}
