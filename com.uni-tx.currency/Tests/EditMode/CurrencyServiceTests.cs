using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniTx.Content;
using UniTx.Currency;
using UniTx.Entity;
using UniTx.IoC;
using UniTx.Serialization;

namespace UniTx.Currency.Tests.EditMode
{
    /// <summary>
    /// A content service backed by a dictionary the test fills.
    /// </summary>
    internal sealed class FakeContentService : IContentService
    {
        private readonly Dictionary<string, IData> _data = new();

        public void Add(IData data) => _data[data.Id] = data;

        public T GetData<T>(string key) where T : IData
        {
            if (_data.TryGetValue(key, out var data) && data is T typed) return typed;

            throw new KeyNotFoundException(key);
        }

        public bool TryGetData<T>(string key, out T data) where T : IData
        {
            if (key != null && _data.TryGetValue(key, out var found) && found is T typed)
            {
                data = typed;
                return true;
            }

            data = default;
            return false;
        }

        public IEnumerable<T> GetData<T>(IEnumerable<string> keys) where T : IData
            => keys.Select(key => TryGetData<T>(key, out var data) ? data : default)
                .Where(data => data != null);

        public IEnumerable<T> GetAllData<T>() where T : IData
            => _data.Values.OfType<T>();
    }

    /// <summary>
    /// An in-memory serialisation service, so tests never touch the disk.
    /// </summary>
    internal sealed class FakeSerialisationService : ISerialisationService
    {
        private readonly Dictionary<string, ISavedData> _store = new();

        public void Save(ISavedData data)
        {
            if (data?.Id == null) return;

            data.ModifiedTimestamp = System.DateTime.UtcNow.Ticks;
            _store[data.Id] = data;
        }

        public T Load<T>(string id) where T : ISavedData, new()
        {
            if (_store.TryGetValue(id, out var existing) && existing is T typed) return typed;

            var created = new T { Id = id };
            _store[id] = created;

            return created;
        }

        public int Flush() => _store.Count;

        public void Delete(string id) => _store.Remove(id);
    }

    [TestFixture]
    public sealed class CurrencyServiceTests
    {
        private UniContainer _container;
        private FakeContentService _content;
        private FakeSerialisationService _serialisation;
        private EntityService _entities;
        private CurrencyService _service;

        [SetUp]
        public void SetUp()
        {
            _container = new UniContainer();
            _content = new FakeContentService();
            _serialisation = new FakeSerialisationService();

            // Bind the instances the tests mutate, so the container resolves the same
            // ones the entity service and entities see.
            _container.BindInstance(_content).AsSingleton().Conclude();
            _container.BindInstance(_serialisation).AsSingleton().Conclude();

            _entities = new EntityService(_container);
            _service = new CurrencyService(_entities);
        }

        [Test]
        public void Grant_AddsToTheBalance()
        {
            Load(MakeCurrency("gems", maxBalance: 0));

            Assert.That(RunFor(_service.GrantAsync("gems", 100)), Is.EqualTo(CurrencyGrantResult.Granted));
            Assert.That(_service.GetBalance("gems"), Is.EqualTo(100));
        }

        [Test]
        public void Grant_WithARepeatedGrantId_AppliesOnce()
        {
            Load(MakeCurrency("gems"));

            Assert.That(RunFor(_service.GrantAsync("gems", 50, "reward-1")),
                Is.EqualTo(CurrencyGrantResult.Granted));
            Assert.That(RunFor(_service.GrantAsync("gems", 50, "reward-1")),
                Is.EqualTo(CurrencyGrantResult.Duplicate));
            Assert.That(_service.GetBalance("gems"), Is.EqualTo(50));
        }

        [Test]
        public void Grant_AtTheMaximum_IsTrimmedThenRefused()
        {
            Load(MakeCurrency("gems", maxBalance: 100));

            Assert.That(RunFor(_service.GrantAsync("gems", 60)), Is.EqualTo(CurrencyGrantResult.Granted));
            Assert.That(RunFor(_service.GrantAsync("gems", 60)), Is.EqualTo(CurrencyGrantResult.Capped));
            Assert.That(_service.GetBalance("gems"), Is.EqualTo(100));
        }

        [Test]
        public void Grant_AnUnregisteredCurrency_IsRefused()
        {
            Assert.That(RunFor(_service.GrantAsync("nope", 10)),
                Is.EqualTo(CurrencyGrantResult.UnknownCurrency));
        }

        [Test]
        public void Grant_NonPositiveAmount_IsRejected()
        {
            Load(MakeCurrency("gems"));

            Assert.That(RunFor(_service.GrantAsync("gems", 0)), Is.EqualTo(CurrencyGrantResult.Rejected));
            Assert.That(RunFor(_service.GrantAsync("gems", -5)), Is.EqualTo(CurrencyGrantResult.Rejected));
        }

        [Test]
        public void TrySpend_WithEnoughBalance_Deducts()
        {
            Load(MakeCurrency("gems", initialBalance: 100));

            Assert.That(_service.TrySpend("gems", 30), Is.True);
            Assert.That(_service.GetBalance("gems"), Is.EqualTo(70));
        }

        [Test]
        public void TrySpend_WithoutEnoughBalance_ChangesNothing()
        {
            Load(MakeCurrency("gems", initialBalance: 20));

            Assert.That(_service.TrySpend("gems", 50), Is.False);
            Assert.That(_service.GetBalance("gems"), Is.EqualTo(20));
        }

        [Test]
        public void TrySpend_AnUnregisteredCurrency_ReturnsFalse()
        {
            Assert.That(_service.TrySpend("nope", 10), Is.False);
        }

        [Test]
        public void InitialBalance_SeedsAFreshPlayerOnce()
        {
            Load(MakeCurrency("gems", initialBalance: 50));

            Assert.That(_service.GetBalance("gems"), Is.EqualTo(50));

            // A reload must not reseed — the seed persists like any other write.
            _entities.UnloadEntities();
            Run(_entities.LoadEntitiesAsync(CancellationToken.None));

            Assert.That(_service.GetBalance("gems"), Is.EqualTo(50));
        }

        [Test]
        public void TryGetBalance_AnUnregisteredCurrency_ReturnsFalse()
        {
            Assert.That(_service.TryGetBalance("nope", out var balance), Is.False);
            Assert.That(balance, Is.Zero);
        }

        [Test]
        public void CurrencyEntity_Grant_RespectsTheContentCap()
        {
            var currency = LoadSingle(MakeCurrency("gems", maxBalance: 100));

            Assert.That(currency.Grant(60), Is.EqualTo(60));
            // The second grant is trimmed so the balance never exceeds the cap.
            Assert.That(currency.Grant(60), Is.EqualTo(40));
            Assert.That(currency.Balance, Is.EqualTo(100));
            Assert.That(currency.Grant(1), Is.Zero);
        }

        [Test]
        public void Load_AnOlderSave_IsMigratedToTheCurrentVersion()
        {
            // A save written by an earlier schema version, already on disk for this player.
            var stale = UnityEngine.JsonUtility.FromJson<CurrencySavedData>(
                @"{""_id"":""gems"",""_version"":0,""_balance"":25}");
            _serialisation.Save(stale);

            Load(MakeCurrency("gems", initialBalance: 50));

            var saved = _entities.Get<Currency>("gems").SavedData;

            // The entity must run Migrate on load, or the upgrade path never executes and
            // fields added in a later version stay unhandled.
            Assert.That(saved.Version, Is.EqualTo(CurrencySavedData.CurrentVersion));
            Assert.That(saved.Balance, Is.EqualTo(25));
        }

        [Test]
        public void CurrencyEntity_BeforeInitialization_RefusesMutationsInsteadOfThrowing()
        {
            var currency = new Currency("gems");

            Assert.That(currency.Grant(5), Is.Zero);
            Assert.That(currency.TrySpend(1), Is.False);
            Assert.DoesNotThrow(() => currency.RaiseTo(10));
            Assert.That(currency.Balance, Is.Zero);
        }

        [Test]
        public void CurrencyEntity_TrySpend_NeverGoesBelowZero()
        {
            var currency = LoadSingle(MakeCurrency("gems", initialBalance: 10));

            Assert.That(currency.TrySpend(10), Is.True);
            Assert.That(currency.TrySpend(1), Is.False);
            Assert.That(currency.Balance, Is.Zero);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────────

        private static CurrencyData MakeCurrency(string id, int initialBalance = 0, int maxBalance = 0) =>
            Parse($@"{{
  ""_id"": ""{id}"",
  ""_displayName"": ""{id}"",
  ""_kind"": 0,
  ""_initialBalance"": {initialBalance},
  ""_maxBalance"": {maxBalance}
}}");

        private static CurrencyData Parse(string json) =>
            UnityEngine.JsonUtility.FromJson<CurrencyData>(json);

        private void Load(params CurrencyData[] currencies)
        {
            foreach (var currency in currencies) _content.Add(currency);

            Run(_entities.LoadEntitiesAsync(CancellationToken.None));
            Run(_service.InitializeAsync(CancellationToken.None));
        }

        private static Currency LoadSingle(CurrencyData data)
        {
            var currency = new Currency(data.Id);
            var container = new UniContainer();
            var content = new FakeContentService();
            content.Add(data);
            var saves = new FakeSerialisationService();
            container.BindInstance(content).AsSingleton().Conclude();
            container.BindInstance(saves).AsSingleton().Conclude();

            currency.Inject(container);
            Run(currency.InitializeAsync(CancellationToken.None));

            return currency;
        }

        private static void Run(UniTask task) => task.GetAwaiter().GetResult();

        private static T RunFor<T>(UniTask<T> task) => task.GetAwaiter().GetResult();
    }
}
