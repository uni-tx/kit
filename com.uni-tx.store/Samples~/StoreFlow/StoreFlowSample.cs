using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Ads;
using UniTx.Content;
using UniTx.Core;
using UniTx.Events;
using UniTx.Iap;
using UniTx.Resources;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.Store.Samples
{
    /// <summary>
    /// Grants rewards into a pretend inventory and logs what arrived.
    /// </summary>
    /// <remarks>
    /// This is the piece every game writes itself. Note that it returns a bool rather than
    /// throwing on refusal: returning false leaves the offer claimable and queued for
    /// retry, which is what keeps a full inventory or a dropped connection from eating a
    /// reward.
    /// </remarks>
    public sealed class SampleStoreRewardGranter : IStoreRewardGranter
    {
        private readonly Dictionary<string, int> _inventory = new();

        /// <summary>
        /// Gets how many of an item the player holds.
        /// </summary>
        /// <param name="itemId">The item to read.</param>
        public int CountOf(string itemId) => _inventory.GetValueOrDefault(itemId, 0);

        /// <inheritdoc />
        public UniTask<bool> GrantAsync(StoreOfferData offer, StoreRewardData reward,
            StoreOfferRef reference, string grantId, CancellationToken cToken = default)
        {
            _inventory[reward.ItemId] = CountOf(reward.ItemId) + reward.Amount;

            Debug.Log($"[Store] +{reward.Amount} {reward.ItemId} from offer " +
                      $"'{reference.OfferId}'. Held: {CountOf(reward.ItemId)}.");

            return UniTask.FromResult(true);
        }
    }

    /// <summary>
    /// The whole shop lifecycle in one script, with no UI.
    /// </summary>
    /// <remarks>
    /// <b>Setup:</b> put <c>store_default.json</c> (in this folder) somewhere Addressable
    /// with the label below, and make sure its asset name matches the registered file name.
    /// Then press play and read the console.
    /// <para>
    /// With no ad or billing provider installed, the rewarded offer reports
    /// <see cref="StoreClaimResult.AdNotReady"/> and the IAP offers report
    /// <see cref="StoreClaimResult.Unavailable"/> — the shop degrades instead of throwing,
    /// which is exactly what a desktop or editor build should do. Install real providers
    /// (the kit's NoOp providers are the default) and the same code pays out for real.
    /// </para>
    /// </remarks>
    public sealed class StoreFlowSample : MonoBehaviour
    {
        private const string StoreFile = "store_default";

        [Tooltip("Addressables label the store definitions are tagged with.")]
        [SerializeField] private string _contentLabel = "content";

        [Tooltip("Policy asset. Leave empty to load Resources/UniStoreConfig.")]
        [SerializeField] private UniStoreConfig _config;

        private readonly SampleStoreRewardGranter _granter = new();

        private StoreService _service;
        private CancellationTokenSource _cts;

        private void Start() => RunAsync().Forget();

        private void OnDestroy()
        {
            UniEvents.Unsubscribe<StoreOfferClaimed>(OnOfferClaimed);

            UniStore.Reset();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async UniTaskVoid RunAsync()
        {
            try
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

                if (!UniEvents.IsInitialized) UniEvents.Initialize();

                // Listening on the bus rather than polling: the same events drive a shop
                // screen, a toast and an analytics adapter without any of them knowing
                // each other.
                UniEvents.Subscribe<StoreOfferClaimed>(OnOfferClaimed);

                var content = await LoadContentAsync(_contentLabel, _cts.Token);

                _service = new StoreService(new LocalClock(), content,
                    new LocalStoreBackend(new SerialisationService()), _config);

                _service.SetRewardGranter(_granter);

                await UniStore.InitializeAsync(_service, _cts.Token);

                if (UniStore.Store == null)
                {
                    Debug.LogWarning("[Store] No store is registered — check the JSON and its label.");
                    return;
                }

                LogState("ready");

                // 1. The free offer is claimable now.
                var free = await UniStore.ClaimAsync("free_pack", _cts.Token);

                Debug.Log($"[Store] Claiming 'free_pack': {free}.");

                // 2. The rewarded offer — with no ad provider installed this reports
                //    AdNotReady; with a real provider it plays the ad and grants on completion.
                var rewarded = await UniStore.ClaimAsync("bonus_gems", _cts.Token);

                Debug.Log($"[Store] Claiming 'bonus_gems': {rewarded}.");

                // 3. An IAP offer — with no billing provider this reports Unavailable; with
                //    a real provider it opens the store sheet and the entitlement arrives
                //    through UniIap.OnPurchased, not through this return value.
                var iap = await UniStore.ClaimAsync("starter_pack", _cts.Token);

                Debug.Log($"[Store] Buying 'starter_pack': {iap}.");

                // 4. The free offer is now on cooldown.
                var again = await UniStore.ClaimAsync("free_pack", _cts.Token);

                Debug.Log($"[Store] Claiming 'free_pack' again: {again}.");

                LogState("done");

                Debug.Log($"[Store] Held: {_granter.CountOf("coins")} coins, " +
                          $"{_granter.CountOf("gems")} gems.");
            }
            catch (OperationCanceledException)
            {
                // The scene is going away; nothing to do.
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Store] Sample failed: {exception}");
            }
        }

        private static async UniTask<IContentService> LoadContentAsync(string label,
            CancellationToken cToken)
        {
            if (!UniResources.IsInitialized) await UniResources.InitializeAsync(cToken);

            // Bind the file name to the type before loading; the Addressable asset name must
            // match this string exactly or the loader skips it with a warning.
            ContentRegistry.Register<StoreData>(StoreFile);

            var content = new ContentService();
            await content.LoadContentAsync(new[] { label }, cToken);

            return content;
        }

        private void LogState(string label)
        {
            var snapshot = UniStore.Snapshot;

            Debug.Log($"[Store] {label}: {snapshot.Offers.Count} offers in " +
                      $"'{snapshot.StoreId}'.");

            foreach (var offer in snapshot.Offers)
            {
                Debug.Log($"[Store]   '{offer.OfferId}': {offer.State} " +
                          $"(claimed {offer.ClaimCount}x).");
            }
        }

        private void OnOfferClaimed(StoreOfferClaimed @event) =>
            Debug.Log($"[Store] '{@event.OfferId}' claimed ({@event.Kind}) " +
                      $"in '{@event.StoreId}'.");
    }
}
