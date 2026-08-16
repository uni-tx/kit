using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Iap;
using UnityEngine;

namespace UniTx.Iap.Samples
{
    /// <summary>
    /// A shop screen wired to the IAP facade end to end.
    /// </summary>
    /// <remarks>
    /// The important detail is where content is granted: in <see cref="Grant"/>, subscribed to
    /// <see cref="UniIap.OnPurchased"/> — not in the code that awaits the purchase. Restores,
    /// subscription renewals and deferred orders never come back through
    /// <see cref="UniIap.PurchaseAsync"/>, so granting from its return value silently loses
    /// every one of them.
    /// </remarks>
    public sealed class ShopSample : MonoBehaviour
    {
        [Tooltip("Catalog, created via Assets ▸ Create ▸ UniTx ▸ IAP ▸ Config.")]
        [SerializeField] private UniIapConfig _config;

        [Tooltip("Consumable product id, matching the store consoles.")]
        [SerializeField] private string _gemsProductId = "com.game.gems";

        [Tooltip("Non-consumable product id.")]
        [SerializeField] private string _removeAdsProductId = "com.game.removeads";

        private CancellationTokenSource _cts;

        /// <summary>
        /// Gets the currently displayed price for the gems product.
        /// </summary>
        public string GemsPrice => UniIap.GetPrice(_gemsProductId);

        private void Awake() => _cts = new CancellationTokenSource();

        private async void Start()
        {
            UniIap.OnPurchased += Grant;

            // Swap in UnityIapProvider once com.unity.purchasing is installed; the no-op
            // provider keeps the shop rendering on desktop and in tests.
            await UniIap.InitializeAsync(new NoOpIapProvider(), _config, _cts.Token);

            Debug.Log($"Shop ready. Gems cost {GemsPrice}.");
        }

        private void OnDestroy()
        {
            UniIap.OnPurchased -= Grant;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        /// <summary>
        /// Buys the consumable gem pack.
        /// </summary>
        public async UniTaskVoid BuyGems()
        {
            var result = await UniIap.PurchaseAsync(_gemsProductId, _cts.Token);

            // React to the *outcome* here — a toast, a retry prompt. The content itself is
            // granted in Grant, which also runs for restores.
            switch (result.Result)
            {
                case IapResult.Success:
                    break;
                case IapResult.Cancelled:
                    Debug.Log("Player closed the store sheet.");
                    break;
                case IapResult.Deferred:
                    Debug.Log("Waiting on parental approval; the gems arrive later.");
                    break;
                default:
                    Debug.LogWarning($"Purchase failed: {result}");
                    break;
            }
        }

        /// <summary>
        /// Restores previous purchases, as iOS review requires.
        /// </summary>
        /// <remarks>
        /// Must be reachable from the UI on iOS — an app that sells non-consumables with no
        /// restore button is rejected. Entitlements arrive through
        /// <see cref="UniIap.OnPurchased"/>, so nothing is granted from the return value.
        /// </remarks>
        public async UniTaskVoid Restore()
        {
            var completed = await UniIap.RestoreAsync(_cts.Token);

            Debug.Log(completed ? "Restore finished." : "Restore did not complete.");
        }

        private void Grant(IapPurchase purchase)
        {
            if (purchase.ProductId == _gemsProductId)
            {
                Debug.Log("Granted 100 gems.");
                return;
            }

            if (purchase.ProductId == _removeAdsProductId)
            {
                Debug.Log("Ads disabled.");
            }
        }
    }
}
