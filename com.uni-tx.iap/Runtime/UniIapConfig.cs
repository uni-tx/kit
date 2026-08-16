using System.Collections.Generic;
using UnityEngine;

namespace UniTx.Iap
{
    /// <summary>
    /// The product catalog, created via <c>Assets ▸ Create ▸ UniTx ▸ IAP ▸ Config</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fill in your own product ids; no code changes are needed to ship. The list is empty by
    /// default rather than pre-filled with example ids, because a stray id in a template is
    /// how a build ends up querying products that belong to someone else.
    /// </para>
    /// <para>
    /// The ids here must match the ones configured in App Store Connect and the Google Play
    /// Console exactly. A product the store has never heard of resolves to
    /// <see cref="IapResult.ProductUnavailable"/> rather than throwing, since a partially
    /// approved catalog is normal during review.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "UniIapConfig", menuName = "UniTx/IAP/Config")]
    public sealed class UniIapConfig : ScriptableObject
    {
        /// <summary>
        /// Resources path the facade falls back to when no config is supplied.
        /// </summary>
        public const string DefaultResourcePath = "UniIapConfig";

        [Header("Catalog")]
        [Tooltip("Every product the game sells. Ids must match the store consoles exactly.")]
        [SerializeField] private List<IapProductStub> _products = new();

        [Header("Behaviour")]
        [Tooltip("Fetch owned non-consumables and subscriptions during initialization, so " +
                 "entitlements survive a reinstall without the player pressing Restore.")]
        [SerializeField] private bool _restoreOnInitialize = true;

        [Tooltip("Log every fetch, purchase and failure. Noisy, but the only practical way " +
                 "to diagnose a store rejection on device.")]
        [SerializeField] private bool _verboseLogging = true;

        [Header("Testing")]
        [Tooltip("Use the fake store in the editor so purchases can be exercised without a " +
                 "real store account. Has no effect in a player build.")]
        [SerializeField] private bool _useFakeStoreInEditor = true;

        /// <summary>
        /// Gets the declared products.
        /// </summary>
        public IReadOnlyList<IapProductStub> Products => _products;

        /// <summary>
        /// Gets whether owned products are fetched during initialization.
        /// </summary>
        public bool RestoreOnInitialize => _restoreOnInitialize;

        /// <summary>
        /// Gets whether the provider should log every request and result.
        /// </summary>
        public bool VerboseLogging => _verboseLogging;

        /// <summary>
        /// Gets whether the editor should route purchases through the fake store.
        /// </summary>
        public bool UseFakeStoreInEditor => _useFakeStoreInEditor;

        /// <summary>
        /// Finds a declared product by its catalog id.
        /// </summary>
        /// <param name="productId">The catalog id to look up.</param>
        /// <returns>The product, or null when it is not declared.</returns>
        public IapProductStub Find(string productId)
        {
            if (string.IsNullOrEmpty(productId)) return null;

            for (var i = 0; i < _products.Count; i++)
            {
                if (_products[i] != null && _products[i].Id == productId) return _products[i];
            }

            return null;
        }

        /// <summary>
        /// Reports catalog problems that would break purchasing at runtime.
        /// </summary>
        /// <returns>A human-readable summary, or an empty string when the catalog is sound.</returns>
        /// <remarks>
        /// Duplicates and blank ids are caught here rather than at the store, because both
        /// fail in ways that are hard to read: a blank id surfaces as a generic fetch failure,
        /// and a duplicate silently shadows whichever entry was declared second.
        /// </remarks>
        public string DescribeProblems()
        {
            if (_products.Count == 0) return "no products are declared";

            var problems = string.Empty;
            var seen = new HashSet<string>();

            for (var i = 0; i < _products.Count; i++)
            {
                var product = _products[i];

                if (product == null || string.IsNullOrWhiteSpace(product.Id))
                {
                    Append(ref problems, $"entry {i} has no id");
                    continue;
                }

                if (!seen.Add(product.Id)) Append(ref problems, $"duplicate id '{product.Id}'");
            }

            return problems;
        }

        private static void Append(ref string target, string entry) =>
            target += target.Length == 0 ? entry : $", {entry}";
    }
}
