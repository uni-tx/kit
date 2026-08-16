using UnityEngine;
using UnityEngine.Audio;

namespace UniTx.Audio
{
    /// <summary>
    /// Playback settings for one sound.
    /// </summary>
    /// <remarks>
    /// <see cref="Volume"/> is the clip's own level and is never scaled in place. The
    /// service multiplies it by the current bus volume when applying it to a source, so
    /// changing the global volume recomputes from this value rather than compounding on top
    /// of whatever the source happened to be playing at.
    /// </remarks>
    public interface IAudioConfigData
    {
        /// <summary>
        /// Gets the clip to play.
        /// </summary>
        AudioClip Clip { get; }

        /// <summary>
        /// Gets the clip's own volume, independent of the SFX or music bus level.
        /// </summary>
        float Volume { get; }

        /// <summary>
        /// Gets the playback pitch.
        /// </summary>
        float Pitch { get; }

        /// <summary>
        /// Gets whether playback loops.
        /// </summary>
        bool Loop { get; }

        /// <summary>
        /// Gets the minimum distance for 3D attenuation.
        /// </summary>
        float MinDistance { get; }

        /// <summary>
        /// Gets the maximum distance for 3D attenuation.
        /// </summary>
        float MaxDistance { get; }

        /// <summary>
        /// Gets the mixer group this sound routes through, if any.
        /// </summary>
        AudioMixerGroup MixerGroup { get; }

        /// <summary>
        /// Gets or sets whether this sound is treated as music rather than an effect.
        /// </summary>
        bool IsMusic { get; set; }

        /// <summary>
        /// Gets or sets the bus level (SFX or music) multiplied onto <see cref="Volume"/>.
        /// </summary>
        /// <remarks>
        /// Carried on the data rather than applied after spawning, so a source starts at the
        /// correct level on its very first frame instead of playing one frame at full volume.
        /// </remarks>
        float BusVolume { get; set; }

        /// <summary>
        /// Gets or sets whether the sound starts muted.
        /// </summary>
        bool IsMuted { get; set; }

        /// <summary>
        /// Gets or sets the spatial blend, 0 for 2D and 1 for 3D.
        /// </summary>
        float SpatialBlend { get; set; }

        /// <summary>
        /// Gets or sets the transform this sound follows while playing.
        /// </summary>
        Transform ToFollow { get; set; }

        /// <summary>
        /// Creates an independent copy, so per-playback tweaks never mutate the shared asset.
        /// </summary>
        IAudioConfigData Clone();
    }
}
