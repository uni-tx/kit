using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Audio.Samples
{
    /// <summary>
    /// 2D/3D/attached effects, music crossfades, and bus volume and mute.
    /// </summary>
    /// <remarks>
    /// <b>Setup:</b> create configs via <b>Assets ▸ Create ▸ UniTx ▸ Audio ▸ Config</b>,
    /// assign a clip to each, then drag them onto the fields below.
    /// </remarks>
    public sealed class AudioPlaybackSample : MonoBehaviour
    {
        [Header("Effects")]
        [SerializeField] private UniAudioConfig _uiClick;
        [SerializeField] private UniAudioConfig _explosion;
        [SerializeField] private UniAudioConfig _engineLoop;

        [Header("Music")]
        [SerializeField] private UniAudioConfig _menuTheme;
        [SerializeField] private UniAudioConfig _battleTheme;

        [Header("Follow target for attached playback")]
        [SerializeField] private Transform _vehicle;

        private CancellationTokenSource _cts;

        private async void Start()
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            // UniTxStep does this during bootstrap.
            if (!UniAudio.IsInitialized) await UniAudio.InitializeAsync(_cts.Token);

            // Bus changes are events, so a settings screen can reflect them without polling.
            UniAudio.OnSfxVolumeChanged += v => Debug.Log($"SFX bus: {v:P0}");
            UniAudio.OnMusicMutedChanged += m => Debug.Log($"Music muted: {m}");

            UniAudio.PlayMusic(_menuTheme);
        }

        private void OnDestroy() => _cts.SafeCancelAndDispose();

        /// <summary>
        /// Plays a UI effect with no spatial position.
        /// </summary>
        [ContextMenu("Play 2D")]
        public void PlayUiClick() => UniAudio.Play2D(_uiClick);

        /// <summary>
        /// Plays an effect at a world position, attenuated by distance.
        /// </summary>
        [ContextMenu("Play 3D")]
        public void PlayExplosion() => UniAudio.Play3D(_explosion, transform.position + Vector3.forward * 5f);

        /// <summary>
        /// Plays an effect that tracks a moving transform.
        /// </summary>
        /// <remarks>
        /// Only attached sounds subscribe to LateUpdate, so a hundred one-shot effects do
        /// not each add a per-frame callback.
        /// </remarks>
        [ContextMenu("Play Attached")]
        public void PlayEngine()
        {
            if (_vehicle != null) UniAudio.PlayAttached(_engineLoop, _vehicle);
        }

        /// <summary>
        /// Crossfades from the current track to the battle theme.
        /// </summary>
        [ContextMenu("Crossfade To Battle")]
        public void CrossfadeToBattle() => CrossfadeAsync(_battleTheme).Forget();

        private async UniTaskVoid CrossfadeAsync(UniAudioConfig track)
        {
            try
            {
                // Fades the outgoing track out and the new one in. A hard cut between tracks
                // is one of the most noticeable audio artifacts there is.
                await UniAudio.PlayMusicAsync(track, fadeDuration: 1.5f, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Superseded by another crossfade, or the object was destroyed.
            }
        }

        /// <summary>
        /// Applies settings-screen values to the buses.
        /// </summary>
        /// <param name="sfx">SFX bus volume, 0..1.</param>
        /// <param name="music">Music bus volume, 0..1.</param>
        public void ApplyVolumeSettings(float sfx, float music)
        {
            // Each playing source recomputes from its clip's own volume, so repeated changes
            // cannot compound and fade everything toward silence.
            UniAudio.SetSfxVolume(sfx);
            UniAudio.SetMusicVolume(music);
        }

        /// <summary>
        /// Mutes both buses, e.g. from a settings toggle.
        /// </summary>
        [ContextMenu("Toggle Mute")]
        public void ToggleMute()
        {
            UniAudio.SetMuteSfx(!UniAudio.IsSfxMuted);
            UniAudio.SetMuteMusic(!UniAudio.IsMusicMuted);
        }

        /// <summary>
        /// Stops every playing effect but leaves music running.
        /// </summary>
        [ContextMenu("Stop All SFX")]
        public void StopEffects() => UniAudio.StopAllSfx();
    }
}
