using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UniTx.Resources
{
    /// <summary>
    /// Pairs a lookup key with the Addressables reference it resolves to.
    /// </summary>
    [Serializable]
    public sealed class Asset
    {
        [Tooltip("Key you look this asset up by — for widgets, the widget type name.")]
        [SerializeField] private string _key = string.Empty;

        [SerializeField] private AssetReference _reference = default;

        /// <summary>
        /// Gets the lookup key for this asset.
        /// </summary>
        public string Key => _key;

        /// <summary>
        /// Gets the Addressables reference this asset points at.
        /// </summary>
        public AssetReference Reference => _reference;

        /// <summary>
        /// Indicates whether the reference points at a real asset.
        /// </summary>
        public bool IsValid => _reference != null && _reference.RuntimeKeyIsValid();

        /// <summary>
        /// Gets the Addressables runtime key.
        /// </summary>
        /// <exception cref="InvalidOperationException">The reference is unassigned.</exception>
        public string RuntimeKey => IsValid
            ? _reference.RuntimeKey.ToString()
            : throw new InvalidOperationException(
                $"Asset '{_key}' has no Addressables reference assigned. Set it on the AssetData asset.");
    }
}
