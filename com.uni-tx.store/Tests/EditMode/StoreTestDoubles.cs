using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Ads;
using UniTx.Content;
using UniTx.Core;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.Store.Tests
{
    /// <summary>
    /// A clock the test drives by hand.
    /// </summary>
    internal sealed class FakeClock : IClock
    {
        public FakeClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; set; }

        public long UnixTimestampNow => UtcNow.ToUnixTimestamp();

        public void Advance(TimeSpan amount) => UtcNow += amount;
    }

    /// <summary>
    /// An in-memory serialisation service, so tests never touch the disk.
    /// </summary>
    internal sealed class FakeSerialisationService : ISerialisationService
    {
        private readonly Dictionary<string, ISavedData> _store = new();

        public int SaveCount { get; private set; }

        public int FlushCount { get; private set; }

        public void Save(ISavedData data)
        {
            SaveCount++;

            if (data?.Id != null) _store[data.Id] = data;
        }

        public T Load<T>(string id)
            where T : ISavedData, new()
        {
            if (_store.TryGetValue(id, out var existing) && existing is T typed) return typed;

            var created = new T { Id = id };
            _store[id] = created;

            return created;
        }

        public int Flush()
        {
            FlushCount++;
            return _store.Count;
        }

        public void Delete(string id) => _store.Remove(id);
    }

    /// <summary>
    /// A content service backed by a dictionary the test fills.
    /// </summary>
    internal sealed class FakeContentService : IContentService
    {
        private readonly Dictionary<string, IData> _data = new();

        public void Add(IData data) => _data[data.Id] = data;

        public void Remove(string key) => _data.Remove(key);

        public T GetData<T>(string key)
            where T : IData =>
            _data.TryGetValue(key, out var data) && data is T typed
                ? typed
                : throw new KeyNotFoundException(key);

        public bool TryGetData<T>(string key, out T data)
            where T : IData
        {
            if (key != null && _data.TryGetValue(key, out var found) && found is T typed)
            {
                data = typed;
                return true;
            }

            data = default;
            return false;
        }

        public IEnumerable<T> GetData<T>(IEnumerable<string> keys)
            where T : IData =>
            keys.Select(key => TryGetData<T>(key, out var data) ? data : default)
                .Where(data => data != null);

        public IEnumerable<T> GetAllData<T>()
            where T : IData => _data.Values.OfType<T>();
    }

    /// <summary>
    /// A granter that records what it was asked to deliver, and can be told to refuse.
    /// </summary>
    internal sealed class RecordingGranter : IStoreRewardGranter
    {
        public List<StoreOfferRef> Granted { get; } = new();

        public List<string> GrantIds { get; } = new();

        public bool ShouldFail { get; set; }

        public bool ShouldThrow { get; set; }

        public UniTask<bool> GrantAsync(StoreOfferData offer, StoreRewardData reward,
            StoreOfferRef reference, string grantId, CancellationToken cToken = default)
        {
            if (ShouldThrow) throw new InvalidOperationException("granter blew up");

            if (ShouldFail) return UniTask.FromResult(false);

            Granted.Add(reference);
            GrantIds.Add(grantId);

            return UniTask.FromResult(true);
        }

        public int CountFor(string offerId) => Granted.Count(reference => reference.OfferId == offerId);
    }

    /// <summary>
    /// An ads provider that completes instantly — EditMode has no player loop, so
    /// <see cref="NoOpAdsProvider"/>'s simulated delay would never elapse and the
    /// rewarded claim would hang. Returns the configured result synchronously.
    /// </summary>
    internal sealed class SyncAdsProvider : IAdsProvider
    {
        private readonly AdResult _rewardedResult;

        public SyncAdsProvider(AdResult rewardedResult = AdResult.Completed)
        {
            _rewardedResult = rewardedResult;
        }

        /// <inheritdoc />
        public string Name => "Sync";

        /// <inheritdoc />
        public bool Supports(AdFormat format) => true;

        /// <inheritdoc />
        public UniTask InitializeAsync(UniAdsConfig config, CancellationToken cToken = default)
            => UniTask.CompletedTask;

        /// <inheritdoc />
        public bool IsReady(AdFormat format) => true;

        /// <inheritdoc />
        public UniTask LoadAsync(AdFormat format, CancellationToken cToken = default)
            => UniTask.CompletedTask;

        /// <inheritdoc />
        public UniTask<AdShowResult> ShowAsync(AdFormat format, string placementName = null,
            CancellationToken cToken = default)
            => UniTask.FromResult(format == AdFormat.Rewarded
                ? new AdShowResult(_rewardedResult)
                : AdShowResult.Completed);

        /// <inheritdoc />
        public UniTask<AdShowResult> ShowInlineAsync(AdFormat format, AdPlacement placement,
            Vector2 safeAreaInsetDp, CancellationToken cToken = default)
            => UniTask.FromResult(AdShowResult.Completed);

        /// <inheritdoc />
        public void HideInline(AdFormat format)
        {
        }

        /// <inheritdoc />
        public void DestroyInline(AdFormat format)
        {
        }

        /// <inheritdoc />
        public void SetConsent(bool hasConsent)
        {
        }
    }

    /// <summary>
    /// A backend over the in-memory serialisation service.
    /// </summary>
    internal sealed class FakeBackend : IStoreBackend
    {
        private readonly FakeSerialisationService _serialisation;

        public FakeBackend(FakeSerialisationService serialisation) => _serialisation = serialisation;

        public bool IsAuthoritative { get; set; } = true;

        public bool IsOnline { get; set; } = true;

        public UniTask<StoreSavedData> LoadAsync(string saveId,
            CancellationToken cToken = default)
        {
            var data = _serialisation.Load<StoreSavedData>(saveId);
            data.Migrate();

            return UniTask.FromResult(data);
        }

        public UniTask SaveAsync(StoreSavedData data, bool immediate,
            CancellationToken cToken = default)
        {
            // Mirrors LocalStoreBackend: a fake that ignores the token would let
            // cancellation bugs pass here and surface only against the real one.
            cToken.ThrowIfCancellationRequested();

            _serialisation.Save(data);

            if (immediate) _serialisation.Flush();

            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// Builds a config the way a designer's asset would deserialize — through JSON — because
    /// the private serialized fields have no public setters.
    /// </summary>
    internal static class ConfigFactory
    {
        public static UniStoreConfig Create(bool flushOnCheckpoint = true,
            string forcedStoreId = null)
        {
            var config = ScriptableObject.CreateInstance<UniStoreConfig>();

            var json =
                $@"{{ ""_saveId"": ""{StoreSavedData.DefaultSaveId}"", " +
                $@"""_flushOnCheckpoint"": {(flushOnCheckpoint ? "true" : "false")}, " +
                $@"""_forcedStoreId"": ""{forcedStoreId ?? string.Empty}"", " +
                $@"""_verboseLogging"": false }}";

            JsonUtility.FromJsonOverwrite(json, config);

            return config;
        }
    }

    /// <summary>
    /// Builds stores the way content does — through JSON.
    /// </summary>
    /// <remarks>
    /// Deliberately not a set of object initializers. The runtime only ever sees a store
    /// that came out of <c>JsonUtility</c>, so building one any other way in a test would
    /// skip the field-mapping rules that make content load at all.
    /// </remarks>
    internal static class StoreJson
    {
        public const string StoreId = "store_test";

        /// <summary>
        /// A store with a free offer (cooldown 60s, limit 3), a rewarded offer and an IAP
        /// offer — the three kinds, so every claim path is exercised.
        /// </summary>
        public static StoreData ThreeKinds(string storeId = StoreId) =>
            Parse($@"{{
  ""_id"": ""{storeId}"",
  ""_displayName"": ""Test Store"",
  ""_offers"": [
    {{
      ""_id"": ""free1"",
      ""_kind"": 1,
      ""_displayName"": ""Free Coins"",
      ""_section"": ""Free"",
      ""_cooldownSeconds"": 60,
      ""_maxClaims"": 3,
      ""_rewards"": [{{ ""_rewardId"": ""f1r1"", ""_kind"": 0, ""_itemId"": ""coins"", ""_amount"": 10 }}]
    }},
    {{
      ""_id"": ""rewarded1"",
      ""_kind"": 2,
      ""_displayName"": ""Bonus Gems"",
      ""_section"": ""Deals"",
      ""_rewards"": [{{ ""_rewardId"": ""r1r1"", ""_kind"": 0, ""_itemId"": ""gems"", ""_amount"": 5 }}]
    }},
    {{
      ""_id"": ""iap1"",
      ""_kind"": 0,
      ""_displayName"": ""Starter Pack"",
      ""_section"": ""Deals"",
      ""_productId"": ""com.test.starter"",
      ""_rewards"": [{{ ""_rewardId"": ""i1r1"", ""_kind"": 0, ""_itemId"": ""gems"", ""_amount"": 100 }}]
    }}
  ]
}}");

        /// <summary>
        /// Parses a store definition exactly as the content loader would.
        /// </summary>
        public static StoreData Parse(string json) => JsonUtility.FromJson<StoreData>(json);
    }
}
