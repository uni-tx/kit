using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Core;
using UniTx.Currency;
using UniTx.IoC;
using UniTx.Rewards;
using UniTx.Serialization;

namespace UniTx.Economy.Tests
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
            where T : IData =>
            _data.Values.OfType<T>();
    }

    /// <summary>
    /// An in-memory currency wallet: balances, spends and grants without any entities.
    /// </summary>
    internal sealed class FakeCurrencyService : ICurrencyService
    {
        private readonly Dictionary<string, int> _balances = new();

        public bool IsReady => true;

        public void Inject(IResolver resolver)
        {
        }

        public UniTask InitializeAsync(CancellationToken cToken = default)
        {
            cToken.ThrowIfCancellationRequested();

            return UniTask.CompletedTask;
        }

        public void Reset() => _balances.Clear();

        public void SetBalance(string currencyId, int balance) => _balances[currencyId] = balance;

        public int GetBalance(string currencyId)
        {
            if (_balances.TryGetValue(currencyId, out var balance)) return balance;

            throw new KeyNotFoundException($"Currency '{currencyId}' is not registered.");
        }

        public bool TryGetBalance(string currencyId, out int balance)
            => _balances.TryGetValue(currencyId, out balance);

        public bool TrySpend(string currencyId, int amount)
        {
            if (amount <= 0) return false;

            if (!_balances.TryGetValue(currencyId, out var balance) || balance < amount)
            {
                return false;
            }

            _balances[currencyId] = balance - amount;

            return true;
        }

        public UniTask<CurrencyGrantResult> GrantAsync(string currencyId, int amount,
            string grantId = null, CancellationToken cToken = default)
        {
            if (amount <= 0) return UniTask.FromResult(CurrencyGrantResult.Rejected);

            if (!_balances.ContainsKey(currencyId))
            {
                return UniTask.FromResult(CurrencyGrantResult.UnknownCurrency);
            }

            _balances[currencyId] = _balances[currencyId] + amount;

            return UniTask.FromResult(CurrencyGrantResult.Granted);
        }
    }

    /// <summary>
    /// A reward service that records grants and can be told to fail.
    /// </summary>
    internal sealed class RecordingRewardService : IRewardService
    {
        public List<string> GrantedIds { get; } = new();

        public bool ShouldFail { get; set; }

        public UniTask<RewardGrantResult> GrantAsync(RewardData reward, string grantId = null,
            CancellationToken cToken = default)
        {
            if (ShouldFail) return UniTask.FromResult(RewardGrantResult.Failed);

            GrantedIds.Add(grantId);

            return UniTask.FromResult(RewardGrantResult.Granted);
        }

        public void SetHandler(RewardKind kind, IRewardHandler handler)
        {
        }

        public void Inject(IResolver resolver)
        {
        }

        public UniTask InitializeAsync(CancellationToken cToken = default)
        {
            cToken.ThrowIfCancellationRequested();

            return UniTask.CompletedTask;
        }

        public void Reset()
        {
            GrantedIds.Clear();
            ShouldFail = false;
        }
    }

    /// <summary>
    /// A backend over the in-memory serialisation service.
    /// </summary>
    internal sealed class FakeBackend : IEconomyBackend
    {
        private readonly ISerialisationService _serialisation;

        public FakeBackend(ISerialisationService serialisation) =>
            _serialisation = serialisation ?? throw new ArgumentNullException(nameof(serialisation));

        public bool IsAuthoritative => false;

        public bool IsOnline => true;

        public UniTask<EconomySavedData> LoadAsync(string saveId,
            CancellationToken cToken = default)
        {
            cToken.ThrowIfCancellationRequested();

            var data = _serialisation.Load<EconomySavedData>(saveId);
            data.Migrate();

            return UniTask.FromResult(data);
        }

        public UniTask SaveAsync(EconomySavedData data, bool immediate,
            CancellationToken cToken = default)
        {
            cToken.ThrowIfCancellationRequested();

            _serialisation.Save(data);

            if (immediate) _serialisation.Flush();

            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// Test JSON: two economies — a core one with an exchange and a purchase, and a meta
    /// one with a purchase of its own.
    /// </summary>
    internal static class EconomyJson
    {
        public const string CoreId = "core";
        public const string MetaId = "meta";

        public static EconomyData Core() =>
            Parse($@"{{
  ""_id"": ""{CoreId}"",
  ""_displayName"": ""Core"",
  ""_currencyIds"": [""coins"", ""gems""],
  ""_exchangeRules"": [
    {{
      ""_id"": ""coins_to_gems"",
      ""_fromCurrencyId"": ""coins"",
      ""_toCurrencyId"": ""gems"",
      ""_rate"": 10,
      ""_minAmount"": 5
    }}
  ],
  ""_purchases"": [
    {{
      ""_id"": ""power_up"",
      ""_displayName"": ""Power Up"",
      ""_costs"": [{{ ""_currencyId"": ""gems"", ""_amount"": 3 }}],
      ""_rewards"": [{{ ""_id"": ""p1r1"", ""_kind"": 0, ""_itemId"": ""coins"", ""_amount"": 100 }}]
    }}
  ]
}}");

        public static EconomyData Meta() =>
            Parse($@"{{
  ""_id"": ""{MetaId}"",
  ""_displayName"": ""Meta"",
  ""_currencyIds"": [""tokens""],
  ""_purchases"": [
    {{
      ""_id"": ""skin"",
      ""_displayName"": ""Skin"",
      ""_costs"": [{{ ""_currencyId"": ""tokens"", ""_amount"": 5 }}],
      ""_rewards"": [{{ ""_id"": ""s1r1"", ""_kind"": 0, ""_itemId"": ""coins"", ""_amount"": 50 }}]
    }}
  ]
}}");

        public static EconomyData Parse(string json) =>
            UnityEngine.JsonUtility.FromJson<EconomyData>(json);
    }
}
