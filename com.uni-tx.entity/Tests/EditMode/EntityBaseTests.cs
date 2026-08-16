using System.Collections.Generic;
using System.Linq;
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

        protected override void OnInject(IResolver resolver) => Injected = true;
        protected override void OnInit() => Initialized = true;
        protected override void OnReset() => ResetCalled = true;
    }

    internal sealed class FakeContentService : IContentService
    {
        public T GetData<T>(string key) where T : IData
            => (T)(IData)new TestEntityData { Id = key, Name = "Entity One" };

        public bool TryGetData<T>(string key, out T data) where T : IData
        {
            data = (T)(IData)new TestEntityData { Id = key, Name = "Entity One" };
            return true;
        }

        public IEnumerable<T> GetData<T>(IEnumerable<string> keys) where T : IData
            => keys.Select(GetData<T>);

        public IEnumerable<T> GetAllData<T>() where T : IData
        {
            yield return (T)(IData)new TestEntityData { Id = "entity-1", Name = "Entity One" };
        }
    }

    internal sealed class FakeSerialisationService : ISerialisationService
    {
        public void Save(ISavedData data) { }

        public T Load<T>(string id) where T : ISavedData, new()
            => (T)(ISavedData)new TestSavedData { Id = id };

        public int Flush() => 0;

        public void Delete(string id) { }
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
            _service.LoadEntities();

            Assert.AreEqual(1, _service.GetAll<TestEntity>().Count());
            var entity = _service.Get<TestEntity>("entity-1");
            Assert.IsTrue(entity.Injected);
            Assert.IsTrue(entity.Initialized);
            Assert.AreEqual("Entity One", entity.Data.Name);
        }

        [Test]
        public void UnloadEntities_ResetsAllEntities()
        {
            _service.LoadEntities();
            var entity = _service.Get<TestEntity>("entity-1");

            _service.UnloadEntities();

            Assert.IsTrue(entity.ResetCalled);
            Assert.IsNull(entity.Data);
        }

        [Test]
        public void Get_UnknownId_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => _service.Get<TestEntity>("missing"));
        }
    }
}
