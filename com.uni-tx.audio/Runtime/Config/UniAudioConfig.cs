using UnityEngine;

namespace UniTx.Audio
{
    /// <summary>
    /// ScriptableObject implementation of IAudioConfig.
    /// </summary>
    /// <summary>
    /// ScriptableObject audio configuration, created via <c>Assets > Create > UniTx > Audio > Config</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAudioConfig", menuName = "UniTx/Audio/Config")]
    public sealed class UniAudioConfig : ScriptableObject, IAudioConfig
    {
        [SerializeField] private UniAudioConfigData _data;

        /// <summary>
        /// Gets the audio configuration data.
        /// </summary>
        public IAudioConfigData Data => _data;

        /// <summary>
        /// Plays this audio configuration in 2D space.
        /// </summary>
        public void Play2D() => UniAudio.Play2D(this);

        /// <summary>
        /// Plays this audio configuration at a specific 3D position.
        /// </summary>
        public void Play3D(Vector3 position) => UniAudio.Play3D(this, position);

        /// <summary>
        /// Plays this audio configuration attached to a specific transform.
        /// </summary>
        public void PlayAttached(Transform parent) => UniAudio.PlayAttached(this, parent);
    }
}