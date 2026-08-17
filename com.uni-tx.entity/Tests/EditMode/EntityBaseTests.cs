using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniTx.Content;
using UniTx.IoC;
using UniTx.Serialization;

namespace UniTx.Entity.Tests.EditMode
{
    public sealed class TestEntityData : IEntityData
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public IEntity CreateEntity() => new TestEntity(Id);
    }

    public sealed class TestSavedData : ISavedData
    {
        public string Id { get; set; }
        public long ModifiedTimestamp { get; set; }
    }

    public sealed class TestEntity : EntityBase<TestEntityData, TestSavedData>
    {
        public bool Injected { get; private set; }
        public bool Initialized { get; private set; }
        public bool ResetCalled { get; private set; }

        public TestEntity(string id) : base(id) { }

        public TestEntity(string id, string dataId) : base(id, dataId) { }

        protected override void OnInject(IResolver resolver) => Injected = true;

        protected override UniTask OnInitAsync(CancellationToken cToken)
        {
            Initialized = true;
            return UniTask.CompletedTask;
        }

        protected override void OnReset() => ResetCalled = true;
    }

    /// <summary>
    /// An entity whose initialization fails, for the readiness contract.
    /// </summary>
    public sealed class FailingEntity : EntityBase<TestEntityData, TestSavedData>
    {
        public FailingEntity(string id) : base(id) { }

        protected override void OnInject(IResolver resolver) { }

        protected override UniTask OnInitAsync(CancellationToken cToken) =>
            UniTask.FromException(new System.InvalidOperationException("init failed"));

        protected override void OnReset() { }
    }

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

    internal sealed class FakeSerialisationService : ISerialisationService
    {
        private readonly Dictionary<string, ISavedData> _store = new();

        public void Save(ISavedData data)
        {
            if (data?.Id != null) _store[data.Id] = data;
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

    public class EntityBaseTests
    {
        private UniContainer _container;
        private EntityService _service;

        [SetUp]
        public void SetUp()
        {
            _container = new UniContainer();
            _container.Bind<FakeContentService>().AsSingleton().Conclude();
            _container.Bind<FakeSerialisationService>().AsSingleton().Conclude();

            _service = new EntityService(_container);
        }

        [Test]
        public void LoadEntities_InitializesAllEntityData()
        {
            _container.Resolve<FakeContentService>().Add(
                new TestEntityData { Id = "entity-1", Name = "Entity One" });

            Run(_service.LoadEntitiesAsync(CancellationToken.None));

            Assert.AreEqual(1, _service.GetAll<TestEntity>().Count());
            var entity = _service.Get<TestEntity>("entity-1");
            Assert.IsTrue(entity.Injected);
            Assert.IsTrue(entity.Initialized);
            Assert.IsTrue(entity.IsReady);
            Assert.AreEqual("Entity One", entity.Data.Name);
        }

        [Test]
        public void UnloadEntities_ResetsAllEntities()
        {
            _container.Resolve<FakeContentService>().Add(
                new TestEntityData { Id = "entity-1", Name = "Entity One" });

            Run(_service.LoadEntitiesAsync(CancellationToken.None));
            var entity = _service.Get<TestEntity>("entity-1");

            _service.UnloadEntities();

            Assert.IsTrue(entity.ResetCalled);
            Assert.IsNull(entity.Data);
            Assert.IsFalse(entity.IsReady);
        }

        [Test]
        public void Get_UnknownId_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => _service.Get<TestEntity>("missing"));
        }

        [Test]
        public void Get_IdRegisteredUnderAnotherType_ReportsTheTypeMismatch()
        {
            _service.Register(new TestEntity("shared-id"));

            var error = Assert.Throws<KeyNotFoundException>(
                () => _service.Get<FailingEntity>("shared-id"));

            // "Not found" would send the caller hunting for a registration that is there.
            Assert.That(error.Message,
                Does.Contain(nameof(TestEntity)).And.Contain(nameof(FailingEntity)));
        }

        [Test]
        public void Initialize_WhenOnInitAsyncFails_LeavesTheEntityNotReady()
        {
            _container.Resolve<FakeContentService>().Add(
                new TestEntityData { Id = "broken", Name = "Broken" });

            var entity = new FailingEntity("broken");
            entity.Inject(_container);

            Assert.Throws<System.InvalidOperationException>(
                () => Run(entity.InitializeAsync(CancellationToken.None)));
            Assert.IsFalse(entity.IsReady);
        }

        [Test]
        public void Register_ExplicitEntity_BecomesResolvable()
        {
            var entity = new TestEntity("explicit-1");

            _service.Register(entity);

            Assert.AreSame(entity, _service.Get<TestEntity>("explicit-1"));
        }

        [Test]
        public void Unregister_RemovesTheEntityWithoutResettingIt()
        {
            var entity = new TestEntity("explicit-1");

            _service.Register(entity);
            _service.Unregister(entity);

            Assert.Throws<KeyNotFoundException>(() => _service.Get<TestEntity>("explicit-1"));
            Assert.IsFalse(entity.ResetCalled);
        }

        [Test]
        public void Entity_WithSeparateDataId_LoadsContentAndSaveUnderDifferentKeys()
        {
            _container.Resolve<FakeContentService>().Add(
                new TestEntityData { Id = "content-key", Name = "Separated" });

            var entity = new TestEntity("save-key", "content-key");
            entity.Inject(_container);
            Run(entity.InitializeAsync(CancellationToken.None));

            Assert.AreEqual("save-key", entity.Id);
            Assert.AreEqual("content-key", entity.DataId);
            Assert.AreEqual("Separated", entity.Data.Name);
            Assert.AreEqual("save-key", entity.SavedData.Id);
        }

        [Test]
        public void Entity_SetDataIdAndReloadData_SwitchesContentWithoutTouchingTheSave()
        {
            _container.Resolve<FakeContentService>().Add(
                new TestEntityData { Id = "first", Name = "First" });
            _container.Resolve<FakeContentService>().Add(
                new TestEntityData { Id = "second", Name = "Second" });

            var entity = new TestEntity("stable-save", "first");
            entity.Inject(_container);
            Run(entity.InitializeAsync(CancellationToken.None));

            entity.SetDataId("second");
            entity.ReloadData();

            Assert.AreEqual("Second", entity.Data.Name);
            Assert.AreEqual("stable-save", entity.SavedData.Id);
        }

        private static void Run(UniTask task) => task.GetAwaiter().GetResult();
    }
}
