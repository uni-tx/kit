using System.Collections.Generic;
using UnityEngine;

namespace UniTx.Resources
{
    /// <summary>
    /// Maps lookup keys to Addressables references, e.g. widget type names to UI prefabs.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAssetData", menuName = "UniTx/Asset Data")]
    public sealed class AssetData : ScriptableObject
    {
        [Tooltip("Identifier for this set. Useful when a project ships several.")]
        [SerializeField] private string _id = string.Empty;

        [SerializeField] private List<Asset> _assets = new();

        private Dictionary<string, Asset> _lookup;

        /// <summary>
        /// Gets the identifier of this asset set.
        /// </summary>
        public string Id => _id;

        /// <summary>
        /// Gets the assets in this set.
        /// </summary>
        public IReadOnlyList<Asset> Assets => _assets;

        /// <summary>
        /// Gets the asset registered under the given key.
        /// </summary>
        /// <param name="key">The lookup key.</param>
        /// <exception cref="KeyNotFoundException">No asset is registered under the key.</exception>
        public Asset GetAsset(string key)
            => TryGetAsset(key, out var asset)
                ? asset
                : throw new KeyNotFoundException(
                    $"AssetData '{name}' has no asset with key '{key}'. Known keys: {string.Join(", ", BuildLookup().Keys)}.");

        /// <summary>
        /// Gets the asset registered under the given key, without throwing when absent.
        /// </summary>
        /// <param name="key">The lookup key.</param>
        /// <param name="asset">The matching asset, or null.</param>
        /// <returns><c>true</c> when a match was found.</returns>
        public bool TryGetAsset(string key, out Asset asset)
        {
            asset = null;

            return !string.IsNullOrEmpty(key) && BuildLookup().TryGetValue(key, out asset);
        }

        private Dictionary<string, Asset> BuildLookup()
        {
            // Built once on first access rather than memoizing per-miss, which previously
            // meant every lookup of an absent key rescanned the whole list.
            if (_lookup != null) return _lookup;

            _lookup = new Dictionary<string, Asset>(_assets.Count);

            foreach (var asset in _assets)
            {
                if (asset == null || string.IsNullOrEmpty(asset.Key)) continue;

                _lookup[asset.Key] = asset;
            }

            return _lookup;
        }

        private void OnValidate() => _lookup = null;
    }
}
