using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.Resources;
using UnityEngine;

namespace UniTx.Content.Samples
{
    /// <summary>
    /// One weapon definition, deserialized from the JSON catalog.
    /// </summary>
    /// <remarks>
    /// JsonUtility maps <b>fields</b>, so every value that must load needs a serialized
    /// field. Public read-only properties are the kit's convention for exposing them.
    /// </remarks>
    [Serializable]
    public sealed class WeaponData : IData
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private int _damage;
        [SerializeField] private float _fireRate;
        [SerializeField] private string[] _tags;

        /// <inheritdoc />
        public string Id => _id;

        /// <summary>
        /// Gets the player-facing name.
        /// </summary>
        public string DisplayName => _displayName;

        /// <summary>
        /// Gets the base damage.
        /// </summary>
        public int Damage => _damage;

        /// <summary>
        /// Gets shots per second.
        /// </summary>
        public float FireRate => _fireRate;

        /// <summary>
        /// Gets the classification tags.
        /// </summary>
        public string[] Tags => _tags ?? Array.Empty<string>();
    }

    /// <summary>
    /// Registering data types, loading a catalog by label, and querying it.
    /// </summary>
    /// <remarks>
    /// <b>Setup:</b> put <c>weapons.json</c> (see this folder) somewhere Addressable, give
    /// it the label below, and make sure its asset name matches the registered file name.
    /// </remarks>
    public sealed class ContentCatalogSample : MonoBehaviour
    {
        private const string WeaponsFile = "weapons";

        [SerializeField] private string _label = "content";

        private readonly ContentService _content = new();
        private CancellationTokenSource _cts;

        private async void Start()
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            if (!UniResources.IsInitialized) await UniResources.InitializeAsync(_cts.Token);

            // Bind file name to type before loading. The file's asset name must match this
            // string exactly, or the loader skips it with a warning.
            ContentRegistry.Register<WeaponData>(WeaponsFile);

            try
            {
                await _content.LoadContentAsync(new[] { _label }, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Debug.Log($"Loaded {_content.Count} content object(s).");

            QueryExamples();
        }

        private void OnDestroy() => _cts.SafeCancelAndDispose();

        private void QueryExamples()
        {
            // By id. Throws with the list of known keys when there is no match, which makes
            // a typo obvious instead of surfacing as a null later.
            var pistol = _content.GetData<WeaponData>("weapon_pistol");
            Debug.Log($"{pistol.DisplayName}: {pistol.Damage} damage");

            // Optional lookup, for content that may not be in the loaded set.
            if (_content.TryGetData<WeaponData>("weapon_secret", out var secret))
            {
                Debug.Log($"Secret weapon unlocked: {secret.DisplayName}");
            }

            // Everything of a type — the usual "populate a shop list" query.
            var all = _content.GetAllData<WeaponData>().OrderByDescending(w => w.Damage).ToArray();

            foreach (var weapon in all)
            {
                Debug.Log($"  {weapon.Id} dmg={weapon.Damage} rate={weapon.FireRate} " +
                          $"tags=[{string.Join(", ", weapon.Tags)}]");
            }

            // Several ids at once.
            var starters = _content.GetData<WeaponData>(new[] { "weapon_pistol", "weapon_smg" });
            Debug.Log($"Starter loadout: {string.Join(", ", starters.Select(w => w.DisplayName))}");
        }

        /// <summary>
        /// Unloads this label's content, e.g. when leaving a chapter.
        /// </summary>
        [ContextMenu("Unload Content")]
        public void UnloadContent() => _content.UnloadContentAsync(new[] { _label }, _cts.Token).Forget();
    }
}
