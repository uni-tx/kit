using System;
using UniTx.Pooling;
using UnityEngine;
using UnityEngine.Audio;

namespace UniTx.Audio
{
    /// <summary>
    /// Serializable playback settings for one sound.
    /// </summary>
    [Serializable]
    public sealed class UniAudioConfigData : IAudioConfigData, IPoolItemData
    {
        [Header("Clip")]
        [SerializeField] private AudioClip _clip;

        [Header("Settings")]
        [Tooltip("This clip's own level. The SFX or music bus volume is applied on top.")]
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [SerializeField, Range(-3f, 3f)] private float _pitch = 1f;
        [SerializeField] private bool _loop;

        [Header("3D")]
        [SerializeField, Min(0f)] private float _minDistance = 1f;
        [SerializeField, Min(0f)] private float _maxDistance = 20f;

        [Header("Mixer")]
        [Tooltip("Optional. Routing through a mixer group is the cheapest way to duck, " +
                 "compress or bus-mute a whole category of sounds.")]
        [SerializeField] private AudioMixerGroup _mixerGroup;

        /// <inheritdoc />
        public AudioClip Clip => _clip;

        /// <inheritdoc />
        public float Volume => _volume;

        /// <inheritdoc />
        public float Pitch => _pitch;

        /// <inheritdoc />
        public bool Loop => _loop;

        /// <inheritdoc />
        public float MinDistance => _minDistance;

        /// <inheritdoc />
        public float MaxDistance => _maxDistance;

        /// <inheritdoc />
        public AudioMixerGroup MixerGroup => _mixerGroup;

        /// <inheritdoc />
        public bool IsMusic { get; set; }

        /// <inheritdoc />
        public float BusVolume { get; set; } = 1f;

        /// <inheritdoc />
        public bool IsMuted { get; set; }

        /// <inheritdoc />
        public float SpatialBlend { get; set; }

        /// <inheritdoc />
        public Transform ToFollow { get; set; }

        /// <summary>
        /// Gets how long one pass of this clip takes, accounting for pitch.
        /// </summary>
        public float PlaybackDuration
        {
            get
            {
                if (_clip == null) return 0f;

                var pitch = Mathf.Abs(_pitch);

                return pitch < 0.01f ? float.PositiveInfinity : _clip.length / pitch;
            }
        }

        /// <inheritdoc />
        public IAudioConfigData Clone() => new UniAudioConfigData
        {
            _clip = _clip,
            _volume = _volume,
            _pitch = _pitch,
            _loop = _loop,
            _minDistance = _minDistance,
            _maxDistance = _maxDistance,
            _mixerGroup = _mixerGroup,
            IsMusic = IsMusic,
            BusVolume = BusVolume,
            IsMuted = IsMuted,
            SpatialBlend = SpatialBlend,
            ToFollow = ToFollow,
        };
    }
}
