using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Audio
{
    /// <summary>
    /// Plays sound effects and music, with independent SFX and music buses.
    /// </summary>
    public interface IAudioService : IInitializableAsync, IResettable
    {
        #region SFX

        /// <summary>
        /// Raised when the SFX mute state changes.
        /// </summary>
        event Action<bool> OnSfxMutedChanged;

        /// <summary>
        /// Raised when the SFX bus volume changes.
        /// </summary>
        event Action<float> OnSfxVolumeChanged;

        /// <summary>
        /// Gets whether the SFX bus is muted.
        /// </summary>
        bool IsSfxMuted { get; }

        /// <summary>
        /// Gets the SFX bus volume, 0..1.
        /// </summary>
        float SfxVolume { get; }

        /// <summary>
        /// Mutes or unmutes the SFX bus.
        /// </summary>
        /// <param name="mute">Whether SFX should be silent.</param>
        void SetMuteSfx(bool mute);

        /// <summary>
        /// Sets the SFX bus volume.
        /// </summary>
        /// <param name="volume">Bus volume, clamped to 0..1.</param>
        void SetSfxVolume(float volume);

        #endregion

        #region Music

        /// <summary>
        /// Raised when the music mute state changes.
        /// </summary>
        event Action<bool> OnMusicMutedChanged;

        /// <summary>
        /// Raised when the music bus volume changes.
        /// </summary>
        event Action<float> OnMusicVolumeChanged;

        /// <summary>
        /// Gets whether the music bus is muted.
        /// </summary>
        bool IsMusicMuted { get; }

        /// <summary>
        /// Gets the music bus volume, 0..1.
        /// </summary>
        float MusicVolume { get; }

        /// <summary>
        /// Mutes or unmutes the music bus.
        /// </summary>
        /// <param name="mute">Whether music should be silent.</param>
        void SetMuteMusic(bool mute);

        /// <summary>
        /// Sets the music bus volume.
        /// </summary>
        /// <param name="volume">Bus volume, clamped to 0..1.</param>
        void SetMusicVolume(float volume);

        #endregion

        /// <summary>
        /// Plays a sound in 2D.
        /// </summary>
        /// <param name="config">The sound to play.</param>
        void Play2D(IAudioConfig config);

        /// <summary>
        /// Plays a sound at a world position.
        /// </summary>
        /// <param name="config">The sound to play.</param>
        /// <param name="position">Where to play it.</param>
        void Play3D(IAudioConfig config, Vector3 position);

        /// <summary>
        /// Plays a sound that follows a transform while it plays.
        /// </summary>
        /// <param name="config">The sound to play.</param>
        /// <param name="parent">The transform to follow.</param>
        void PlayAttached(IAudioConfig config, Transform parent);

        /// <summary>
        /// Starts background music immediately, replacing whatever is playing.
        /// </summary>
        /// <param name="config">The music to play.</param>
        void PlayMusic(IAudioConfig config);

        /// <summary>
        /// Crossfades from the current music to a new track.
        /// </summary>
        /// <param name="config">The music to play.</param>
        /// <param name="fadeDuration">Total crossfade duration in seconds.</param>
        /// <param name="cToken">Token to cancel the crossfade.</param>
        UniTask PlayMusicAsync(IAudioConfig config, float fadeDuration, CancellationToken cToken = default);

        /// <summary>
        /// Stops background music.
        /// </summary>
        void StopMusic();

        /// <summary>
        /// Stops every playing sound effect and returns the sources to the pool.
        /// </summary>
        void StopAllSfx();

        /// <summary>
        /// Pauses music and every playing effect, keeping their playheads.
        /// </summary>
        /// <remarks>
        /// Call from <c>IUnityEventListener.OnPause</c> so audio does not keep playing when
        /// the app is backgrounded on mobile.
        /// </remarks>
        void PauseAll();

        /// <summary>
        /// Resumes everything paused by <see cref="PauseAll"/>.
        /// </summary>
        void ResumeAll();
    }
}
