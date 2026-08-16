using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Core;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.SeasonPass.Tests
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
    internal sealed class RecordingGranter : ISeasonPassRewardGranter
    {
        public List<SeasonRewardRef> Granted { get; } = new();

        public bool ShouldFail { get; set; }

        public bool ShouldThrow { get; set; }

        public UniTask<bool> GrantAsync(SeasonRewardData reward, SeasonRewardRef reference,
            CancellationToken cToken = default)
        {
            if (ShouldThrow) throw new InvalidOperationException("granter blew up");

            if (ShouldFail) return UniTask.FromResult(false);

            Granted.Add(reference);

            return UniTask.FromResult(true);
        }

        public int CountFor(int tier, SeasonTrack track) =>
            Granted.Count(reference => reference.Tier == tier && reference.Track == track);
    }

    /// <summary>
    /// A wallet with balances the test sets.
    /// </summary>
    internal sealed class FakeWallet : ISeasonPassWallet
    {
        private readonly Dictionary<string, int> _balances = new();

        public void SetBalance(string currencyId, int amount) => _balances[currencyId] = amount;

        public int GetBalance(string currencyId) =>
            _balances.TryGetValue(currencyId, out var balance) ? balance : 0;

        public bool TrySpend(string currencyId, int amount)
        {
            if (GetBalance(currencyId) < amount) return false;

            _balances[currencyId] -= amount;

            return true;
        }
    }

    /// <summary>
    /// A backend that can go offline and hand back a divergent record, so reconciliation and
    /// the offline queue can be exercised without a server.
    /// </summary>
    internal sealed class FakeBackend : ISeasonPassBackend
    {
        private readonly FakeSerialisationService _serialisation;

        public FakeBackend(FakeSerialisationService serialisation) => _serialisation = serialisation;

        public bool IsAuthoritative { get; set; } = true;

        public bool IsOnline { get; set; } = true;

        public SeasonPassSavedData RemoteRecord { get; set; }

        public SeasonPassSavedData LastSynced { get; private set; }

        public int SyncCount { get; private set; }

        public UniTask<SeasonPassSavedData> LoadAsync(string saveId, CancellationToken cToken = default)
        {
            var data = _serialisation.Load<SeasonPassSavedData>(saveId);
            data.Migrate();

            return UniTask.FromResult(data);
        }

        public UniTask SaveAsync(SeasonPassSavedData data, bool immediate,
            CancellationToken cToken = default)
        {
            // Mirrors LocalSeasonPassBackend: a fake that ignores the token would let
            // cancellation bugs pass here and surface only against the real one.
            cToken.ThrowIfCancellationRequested();

            _serialisation.Save(data);

            if (immediate) _serialisation.Flush();

            return UniTask.CompletedTask;
        }

        public UniTask<SeasonPassSavedData> SyncAsync(SeasonPassSavedData local,
            CancellationToken cToken = default)
        {
            SyncCount++;
            LastSynced = local;

            return UniTask.FromResult(RemoteRecord);
        }
    }

    /// <summary>
    /// Builds season definitions the way content does — through JSON.
    /// </summary>
    /// <remarks>
    /// Deliberately not a set of object initializers. The runtime only ever sees a season that
    /// came out of <c>JsonUtility</c>, so building one any other way in a test would skip the
    /// field-mapping rules that make content load at all.
    /// </remarks>
    internal static class SeasonJson
    {
        public const string SeasonId = "season_test";

        /// <summary>
        /// A three-tier season: 100/200/300 XP, one free and one premium reward per tier.
        /// </summary>
        public static SeasonPassData Standard(string id = SeasonId, string startUtc = "2026-06-01T00:00:00Z",
            string endUtc = "2026-07-01T00:00:00Z", int bonusTierXp = 0, int dailyCap = 0,
            int maxTierSkips = 0) =>
            Parse($@"{{
  ""_id"": ""{id}"",
  ""_displayName"": ""Test Season"",
  ""_startUtc"": ""{startUtc}"",
  ""_endUtc"": ""{endUtc}"",
  ""_claimGraceHours"": 48,
  ""_endingSoonHours"": 72,
  ""_bonusTierXp"": {bonusTierXp},
  ""_bonusTierRewards"": [
    {{ ""_rewardId"": ""bonus_coins"", ""_track"": 0, ""_kind"": 0, ""_itemId"": ""coins"", ""_amount"": 50 }}
  ],
  ""_tiers"": [
    {{ ""_tier"": 2, ""_requiredXp"": 200, ""_rewards"": [
      {{ ""_rewardId"": ""f2"", ""_track"": 0, ""_kind"": 0, ""_itemId"": ""coins"", ""_amount"": 20 }},
      {{ ""_rewardId"": ""p2"", ""_track"": 1, ""_kind"": 1, ""_itemId"": ""gem"", ""_amount"": 2 }}
    ] }},
    {{ ""_tier"": 1, ""_requiredXp"": 100, ""_rewards"": [
      {{ ""_rewardId"": ""f1"", ""_track"": 0, ""_kind"": 0, ""_itemId"": ""coins"", ""_amount"": 10 }},
      {{ ""_rewardId"": ""p1"", ""_track"": 1, ""_kind"": 1, ""_itemId"": ""gem"", ""_amount"": 1 }}
    ] }},
    {{ ""_tier"": 3, ""_requiredXp"": 300, ""_rewards"": [
      {{ ""_rewardId"": ""f3"", ""_track"": 0, ""_kind"": 0, ""_itemId"": ""coins"", ""_amount"": 30 }},
      {{ ""_rewardId"": ""p3"", ""_track"": 1, ""_kind"": 2, ""_itemId"": ""skin"", ""_amount"": 1, ""_isHighlight"": true }}
    ] }}
  ],
  ""_trackOffers"": [
    {{ ""_track"": 1, ""_productId"": ""com.game.pass"", ""_currencyId"": ""gems"", ""_currencyCost"": 500, ""_includedTierSkips"": 0 }}
  ],
  ""_xpSources"": [
    {{ ""_sourceId"": ""match_complete"", ""_xpPerEvent"": 50, ""_dailyCap"": {dailyCap} }},
    {{ ""_sourceId"": ""premium_bonus"", ""_xpPerEvent"": 25, ""_dailyCap"": 0, ""_requiresPaidTrack"": true }}
  ],
  ""_quests"": [
    {{ ""_questId"": ""daily_win"", ""_scope"": 0, ""_goal"": 2, ""_xpReward"": 60 }},
    {{ ""_questId"": ""weekly_grind"", ""_scope"": 1, ""_goal"": 5, ""_xpReward"": 150 }}
  ],
  ""_tierSkipProductId"": ""com.game.skip"",
  ""_tierSkipCurrencyId"": ""gems"",
  ""_tierSkipCurrencyCost"": 100,
  ""_maxTierSkipPurchases"": {maxTierSkips}
}}");

        /// <summary>
        /// Parses a season definition exactly as the content loader would.
        /// </summary>
        public static SeasonPassData Parse(string json) => JsonUtility.FromJson<SeasonPassData>(json);
    }
}
