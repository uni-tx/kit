using UnityEngine;

namespace UniTx.Core
{
    /// <summary>
    /// Central configuration asset for the UniTx kit, created via <c>Assets ▸ Create ▸ UniTx ▸ Config</c>.
    /// </summary>
    /// <remarks>
    /// Assign it to a <c>UniTxStep</c> in the scene, or place one at
    /// <c>Resources/UniTxConfig</c> to have it picked up automatically.
    /// </remarks>
    [CreateAssetMenu(fileName = "UniTxConfig", menuName = "UniTx/Config")]
    public sealed class UniTxConfig : ScriptableObject
    {
        /// <summary>
        /// Resources path the kit falls back to when no config is assigned explicitly.
        /// </summary>
        public const string DefaultResourcePath = "UniTxConfig";

        [Header("Widgets")]
        [SerializeField] private string _widgetsAssetDataKey = "WidgetAssetData";
        [SerializeField] private string _widgetsParentTag = "WidgetsParent";

        [Header("Clock")]
        [Tooltip("HTTPS endpoint whose response Date header supplies UTC. Any reliable " +
                 "host works — no API key, no rate limit, no third-party time service.")]
        [SerializeField] private string _timeServerUrl = "https://www.cloudflare.com/cdn-cgi/trace";
        [Tooltip("How many times ServerClock retries before falling back to device time.")]
        [SerializeField, Min(0)] private int _timeServerMaxRetries = 3;

        [Header("Serialization")]
        [Tooltip("Seconds between automatic save batches.")]
        [SerializeField, Min(0.1f)] private float _saveInterval = 5f;

        /// <summary>
        /// Gets the Addressables key of the <c>AssetData</c> asset that maps widget types to prefabs.
        /// </summary>
        public string WidgetsAssetDataKey => _widgetsAssetDataKey;

        /// <summary>
        /// Gets the scene tag of the transform under which widgets are spawned.
        /// </summary>
        public string WidgetsParentTag => _widgetsParentTag;

        /// <summary>
        /// Gets the URL whose HTTP <c>Date</c> response header supplies UTC time.
        /// </summary>
        public string TimeServerUrl => _timeServerUrl;

        /// <summary>
        /// Gets how many times <see cref="ServerClock"/> retries before falling back to device time.
        /// </summary>
        public int TimeServerMaxRetries => _timeServerMaxRetries;

        /// <summary>
        /// Gets the interval in seconds between automatic save batches.
        /// </summary>
        public float SaveInterval => _saveInterval;
    }
}
