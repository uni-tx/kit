using UnityEngine;

namespace UniTx.Audio
{
    /// <summary>
    /// Interface for audio configuration.
    /// </summary>
    public interface IAudioConfig
    {
        /// <summary>
        /// Gets the audio configuration data.
        /// </summary>
        IAudioConfigData Data { get; }

        /// <summary>
        /// Plays the audio in 2D space.
        /// </summary>
        void Play2D();

        /// <summary>
        /// Plays the audio at a specific 3D position.
        /// </summary>
        void Play3D(Vector3 position);

        /// <summary>
        /// Plays the audio attached to a specific transform.
        /// </summary>
        void PlayAttached(Transform parent);
    }
}