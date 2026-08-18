using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Ads;
using UniTx.Content;
using UniTx.Core;
using UniTx.Events;
using UniTx.Iap;
using UniTx.IoC;
using UniTx.Rewards;
using UnityEngine;

namespace UniTx.Store
{
    /// <summary>
    /// The shop itself: content-defined offers of three kinds — IAP, free-on-cooldown and
    /// rewarded — with idempotent claims that are recorded only after delivery succeeds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every rule that could cost a player something is concentrated here rather than
    /// spread across call sites: a claim is recorded only after the granter reports
    /// success, a cooldown and a claim limit gate free and rewarded offers, a failed
    /// delivery is retried on the next refresh, and a replayed grant id never pays twice.
    /// A game calls one or two methods; the invariants are not its problem.
    /// </para>
    /// <para>
    /// Static and saved data live in a <see cref="StoreEntity"/> — the same entity
    /// foundation every kit system builds on. The entity's save key is stable while its
    /// content key (the store id) can be re-pointed, and persistence routes through
    /// <see cref="IStoreBackend"/> so a server can take authority later.
    /// </para>
    /// <para>
    /// IAP offers forward to <see cref="UniIap"/> and report the billing verdict; the
    /// entitlement itself is granted from <see cref="UniIap.OnPurchased"/> (via the
    /// optional <c>StoreIapBridge</c>), which is the only place restores and deferred
    /// purchases ever appear. Rewarded offers forward to <see cref="UniAds"/> and grant
    /// only when the ad completes.
    /// </para>
    /// </remarks>
    public sealed class StoreService : IStoreService
    {
        private IClock _clock;
        private IContentService _content;
        private IStoreBackend _backend;
        private IStoreRewardGranter _granter;
        private UniStoreConfig _config;

        private StoreEntity _entity;
        private bool _hasWarnedMultipleStores;

        /// <summary>
        /// Creates the service; dependencies arrive through <see cref="Inject"/>.
        /// </summary>
        public StoreService()
        {
        }

        /// <summary>
        /// Creates the service with explicit dependencies, for tests and manual wiring.
        /// </summary>
        /// <param name="clock">The time source driving cooldowns.</param>
        /// <param name="content">The content service holding store definitions.</param>
        /// <param name="backend">Where progress is stored.</param>
        /// <param name="config">Policy. Falls back to Resources/UniStoreConfig.</param>
        public StoreService(IClock clock, IContentService content,
            IStoreBackend backend, UniStoreConfig config = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _config = config;
        }

        /// <inheritdoc />
        public bool IsReady { get; private set; }

        /// <inheritdoc />
        public StoreData Store => _entity?.Data;

        /// <inheritdoc />
        public StoreSnapshot Snapshot => BuildSnapshot();

        /// <inheritdoc />
        public event Action<StoreSnapshot> OnChanged;

        /// <summary>
        /// Gets the player's persisted progress, or null before initialization.
        /// </summary>
        public StoreSavedData SavedData => _entity?.SavedData;

        /// <inheritdoc />
        public void Inject(IResolver resolver)
        {
            _clock ??= resolver.Resolve<IClock>();
            _content ??= resolver.Resolve<IContentService>();

            if (_backend == null && !resolver.TryResolve(out _backend))
            {
                var local = new LocalStoreBackend();
                local.Inject(resolver);
                _backend = local;
            }

            // Optional by design: a game without an economy yet still gets a working shop.
            if (_granter == null && resolver.TryResolve<IStoreRewardGranter>(out var granter))
            {
                _granter = granter;
            }

            // Default on top of the entity foundation: rewards route through the kit's
            // reward service when it is registered.
            if (_granter == null && resolver.TryResolve<IRewardService>(out var rewards))
            {
                _granter = new StoreRewardGranter(rewards);
            }
        }

        /// <inheritdoc />
        public async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            _config ??= Resources.Load<UniStoreConfig>(UniStoreConfig.DefaultResourcePath);

            if (_config == null)
            {
                UniStatics.LogWarning(
                    "No UniStoreConfig supplied and none found at " +
                    $"Resources/{UniStoreConfig.DefaultResourcePath}; using defaults.", this);

                _config = ScriptableObject.CreateInstance<UniStoreConfig>();
            }
            else
            {
                var problems = _config.DescribeProblems();

                if (!string.IsNullOrEmpty(problems))
                {
                    UniStatics.LogWarning($"UniStoreConfig has problems: {problems}.", this);
                }
            }

            _granter ??= LoggingStoreRewardGranter.Instance;

            EnsureEntity();

            // Loads the save through the backend and prepares the entity's data half.
            await _entity.InitializeAsync(cToken);

            IsReady = true;

            // IAP entitlements arrive through the store's event — restores, deferred
            // purchases and subscription renewals never return through the purchase call.
            // Subscribe here so a re-initialization detaches the previous subscription first.
            UniIap.OnPurchased -= HandlePurchased;
            UniIap.OnPurchased += HandlePurchased;

            await RefreshAsync(cToken);
        }

        /// <inheritdoc />
        public void Reset()
        {
            IsReady = false;
            _hasWarnedMultipleStores = false;

            UniIap.OnPurchased -= HandlePurchased;

            _entity?.Reset();
            _entity = null;
        }

        private void HandlePurchased(IapPurchase purchase)
        {
            if (!purchase.IsSuccess || string.IsNullOrEmpty(purchase.ProductId)) return;

            DeliverIapAsync(purchase.ProductId, purchase.TransactionId).Forget();
        }

        private async UniTaskVoid DeliverIapAsync(string productId, string transactionId)
        {
            try
            {
                await DeliverIapCoreAsync(productId, transactionId, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // The service was reset mid-delivery; nothing to report.
            }
            catch (Exception exception)
            {
                UniStatics.LogException(exception, this);
            }
        }

        /// <summary>
        /// Delivers an IAP entitlement for a product id — from a purchase, a restore, a
        /// deferred order that finally cleared, or a server-validated receipt.
        /// </summary>
        /// <param name="productId">The billing product id.</param>
        /// <param name="transactionId">The store transaction id, for idempotency.</param>
        /// <param name="cToken">Token to cancel the delivery.</param>
        /// <returns>The claim outcome.</returns>
        /// <remarks>
        /// The one place IAP offers are delivered, and the reason the service subscribes to
        /// <see cref="UniIap.OnPurchased"/>: restores, deferred purchases and subscription
        /// renewals never return through the purchase call. The transaction id is the
        /// replay discriminator, so a restore replaying the same transaction cannot pay
        /// twice.
        /// </remarks>
        public async UniTask<StoreClaimResult> DeliverIapAsync(string productId,
            string transactionId, CancellationToken cToken = default)
            => await DeliverIapCoreAsync(productId, transactionId, cToken);

        private async UniTask<StoreClaimResult> DeliverIapCoreAsync(string productId,
            string transactionId, CancellationToken cToken)
        {
            if (!IsReady || Store == null) return StoreClaimResult.NoStore;

            StoreOfferData offer = null;

            foreach (var candidate in Store.Offers)
            {
                if (candidate != null &&
                    string.Equals(candidate.ProductId, productId, StringComparison.Ordinal))
                {
                    offer = candidate;
                    break;
                }
            }

            if (offer == null || offer.Kind != StoreOfferKind.Iap) return StoreClaimResult.NoOffer;

            var record = SavedData.GetOrCreateRecord(offer.Id);

            // The transaction id is the replay discriminator: a restore replaying the same
            // transaction cannot double-pay. Fall back to the clock when the store gives none.
            var claimKey = string.IsNullOrEmpty(transactionId)
                ? $"txn:{_clock.UnixTimestampNow}"
                : $"txn:{transactionId}";

            return await DeliverAsync(offer, record, cToken, claimKey);
        }

        /// <inheritdoc />
        public void SetRewardGranter(IStoreRewardGranter granter) =>
            _granter = granter ?? throw new ArgumentNullException(nameof(granter));

        /// <inheritdoc />
        public async UniTask<StoreClaimResult> ClaimAsync(string offerId,
            CancellationToken cToken = default)
        {
            if (!IsReady || Store == null) return StoreClaimResult.NoStore;

            var offer = Store.GetOffer(offerId);

            if (offer == null) return StoreClaimResult.NoOffer;

            cToken.ThrowIfCancellationRequested();

            var record = SavedData.GetOrCreateRecord(offerId);

            // Free and rewarded offers are gated by cooldown and limit; IAP offers are
            // gated by the store and never by our own clock.
            if (offer.Kind != StoreOfferKind.Iap)
            {
                if (StoreCalculator.IsLimitReached(offer, record))
                {
                    return StoreClaimResult.LimitReached;
                }

                if (StoreCalculator.IsOnCooldown(offer, record, _clock.UnixTimestampNow))
                {
                    return StoreClaimResult.OnCooldown;
                }
            }

            return offer.Kind switch
            {
                StoreOfferKind.Iap => await ClaimIapAsync(offer, cToken),
                StoreOfferKind.Free => await DeliverAsync(offer, record, cToken),
                _ => await ClaimRewardedAsync(offer, record, cToken),
            };
        }

        /// <inheritdoc />
        public async UniTask RefreshAsync(CancellationToken cToken = default)
        {
            if (!IsReady) return;

            cToken.ThrowIfCancellationRequested();

            SetStore(SelectStore());

            // A delivery that failed earlier is retried here, so a player who saw the
            // failure and simply closed the app still gets the reward on the next launch.
            await RetryFailedDeliveriesAsync(cToken);

            await PersistAsync(false, cToken);

            RaiseChanged();
        }

        private async UniTask RetryFailedDeliveriesAsync(CancellationToken cToken)
        {
            if (Store == null) return;

            foreach (var offer in Store.Offers)
            {
                if (offer == null || !offer.IsValid) continue;

                // IAP offers are delivered through the store's entitlement event, not by
                // us — a failed IAP delivery resolves when the purchase restores.
                if (offer.Kind == StoreOfferKind.Iap) continue;

                var record = SavedData.GetOrCreateRecord(offer.Id);

                if (!record.IsFailed) continue;

                await DeliverAsync(offer, record, cToken);
            }
        }

        private async UniTask<StoreClaimResult> ClaimIapAsync(StoreOfferData offer,
            CancellationToken cToken)
        {
            if (string.IsNullOrEmpty(offer.ProductId)) return StoreClaimResult.Rejected;

            var purchase = await UniIap.PurchaseAsync(offer.ProductId, cToken);

            return purchase.Result switch
            {
                IapResult.Success => StoreClaimResult.Purchased,
                IapResult.Cancelled => StoreClaimResult.Cancelled,
                IapResult.AlreadyOwned => StoreClaimResult.AlreadyOwned,
                IapResult.Deferred => StoreClaimResult.Deferred,
                IapResult.ProductUnavailable or IapResult.Unsupported =>
                    StoreClaimResult.Unavailable,
                _ => StoreClaimResult.PurchaseFailed,
            };
        }

        private async UniTask<StoreClaimResult> ClaimRewardedAsync(StoreOfferData offer,
            StoreOfferRecord record, CancellationToken cToken)
        {
            var result = await UniAds.ShowRewardedAsync($"store_{offer.Id}", cToken);

            // Grant only when the ad actually completed — never on close, never on skip.
            if (!result.ShouldReward)
            {
                return result.Result == AdResult.NotReady
                    ? StoreClaimResult.AdNotReady
                    : StoreClaimResult.AdNotCompleted;
            }

            return await DeliverAsync(offer, record, cToken);
        }

        private async UniTask<StoreClaimResult> DeliverAsync(StoreOfferData offer,
            StoreOfferRecord record, CancellationToken cToken,
            string claimKey = null)
        {
            // The claim key is the replay discriminator: free and rewarded offers count up
            // (a failed delivery retries with the same number, so a partial success cannot
            // double-pay), IAP offers use the store transaction id so a restore replaying it
            // is deduped by the grant ledger.
            claimKey ??= (record.ClaimCount + 1).ToString();

            var failedRewardId = string.Empty;

            foreach (var reward in offer.Rewards)
            {
                if (reward == null || !reward.IsValid) continue;

                var grantId = StoreOfferRef.GrantId(Store.Id, offer.Id, reward.RewardId,
                    claimKey);

                // Belt and braces on top of the claim flag: the same offer cannot be
                // recorded twice, but a replayed delivery with the same id must not pay twice.
                if (SavedData.HasAppliedGrant(grantId)) continue;

                var reference = new StoreOfferRef(Store.Id, offer.Id);

                bool granted;

                try
                {
                    granted = await _granter.GrantAsync(offer, reward, reference, grantId,
                        cToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // A granter that throws is a bug in the game's economy code, not a
                    // reason to mark the offer collected. The offer stays claimable and
                    // the next refresh or claim retries it.
                    UniStatics.LogException(exception, this);
                    granted = false;
                }

                if (!granted)
                {
                    failedRewardId = reward.RewardId;
                    break;
                }

                SavedData.RecordAppliedGrant(grantId);
            }

            if (!string.IsNullOrEmpty(failedRewardId))
            {
                record.MarkClaimFailed();
                Raise(new StoreDeliveryFailed(Store.Id, offer.Id, failedRewardId));

                if (_config.VerboseLogging)
                {
                    UniStatics.LogWarning(
                        $"Store delivery failed for '{offer.Id}': reward '{failedRewardId}' " +
                        "was refused; the offer stays claimable and will be retried.", this);
                }

                await PersistAsync(true, cToken);
                RaiseChanged();

                return StoreClaimResult.GrantFailed;
            }

            record.RecordClaim(_clock.UnixTimestampNow);

            Raise(new StoreOfferClaimed(Store.Id, offer.Id, offer.Kind, claimKey));

            if (_config.VerboseLogging)
            {
                UniStatics.LogInfo($"Store offer claimed: '{offer.Id}' from '{Store.Id}'.", this);
            }

            await PersistAsync(true, cToken);

            RaiseChanged();

            return offer.Kind == StoreOfferKind.Rewarded
                ? StoreClaimResult.Rewarded
                : StoreClaimResult.Claimed;
        }

        private void EnsureEntity()
        {
            if (_entity != null) return;

            _entity = new StoreEntity(_config.SaveId, _backend, _content);
        }

        private StoreData SelectStore()
        {
            var forcedId = _config.ForcedStoreId;

            if (!string.IsNullOrWhiteSpace(forcedId))
            {
                return _content.TryGetData<StoreData>(forcedId, out var forced)
                    ? forced
                    : null;
            }

            StoreData first = null;
            var count = 0;

            foreach (var candidate in _content.GetAllData<StoreData>())
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.Id)) continue;

                count++;

                if (first == null) first = candidate;
            }

            if (count > 1 && !_hasWarnedMultipleStores)
            {
                _hasWarnedMultipleStores = true;

                UniStatics.LogWarning(
                    $"{count} stores are registered but none is forced; using " +
                    $"'{first?.Id}'. Pin UniStoreConfig.ForcedStoreId to select " +
                    "deterministically.", this);
            }

            return first;
        }

        /// <summary>
        /// Points the entity's content key at a store and reloads its static data.
        /// </summary>
        /// <param name="store">The store to show, or null for none.</param>
        private void SetStore(StoreData store)
        {
            _entity.SetDataId(store?.Id);
            _entity.ReloadData();

            // A store id that changed means the offers the progress points into are gone;
            // keep the per-offer records (recurring offers survive) while the store id in
            // the save is re-pointed.
            if (store != null &&
                !string.Equals(SavedData.StoreId, store.Id, StringComparison.Ordinal))
            {
                SavedData.SetStoreId(store.Id);
            }
        }

        private StoreSnapshot BuildSnapshot()
        {
            if (!IsReady || Store == null)
            {
                return new StoreSnapshot(null, Array.Empty<StoreOfferSnapshot>());
            }

            var nowUnix = _clock.UnixTimestampNow;
            var offers = new List<StoreOfferSnapshot>(Store.Offers.Count);

            foreach (var offer in Store.Offers)
            {
                if (offer == null) continue;

                var record = SavedData.GetOrCreateRecord(offer.Id);

                var state = StoreCalculator.EvaluateState(offer, record, nowUnix);

                offers.Add(new StoreOfferSnapshot(offer.Id, offer.Kind, state,
                    StoreCalculator.RemainingCooldownSeconds(offer, record, nowUnix),
                    record.ClaimCount));
            }

            return new StoreSnapshot(Store.Id, offers);
        }

        private void RaiseChanged() => OnChanged.SafeInvoke(BuildSnapshot());

        private UniTask PersistAsync(bool isCheckpoint, CancellationToken cToken) =>
            _entity.SaveAsync(isCheckpoint && _config.FlushOnCheckpoint, cToken);

        private static void Raise<TEvent>(TEvent @event)
            where TEvent : struct, IEvent
        {
            // The bus is optional: a game that never bootstrapped UniEvents still gets a
            // working shop through OnChanged and the awaited results.
            if (UniEvents.IsInitialized) UniEvents.Raise(@event);
        }
    }
}
