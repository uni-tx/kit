using System;
using NUnit.Framework;
using UnityEngine;

namespace UniTx.Pooling.Tests.PlayMode
{
    public sealed class TestPoolItemData : IPoolItemData
    {
        public int Value;
    }

    public sealed class TestPoolItem : MonoBehaviour, IPoolItem<TestPoolItemData>
    {
        public int InitializeCount { get; private set; }
        public int ResetCount { get; private set; }
        public TestPoolItemData Data { get; private set; }

        public GameObject GameObject => gameObject;
        public Transform Transform => transform;

        public void SetPoolItemReturner(IPoolItemReturner returner) => Returner = returner;
        public IPoolItemReturner Returner { get; private set; }
        public void SetData(IPoolItemData data) => Data = (TestPoolItemData)data;
        public void Return() => Returner.Return(this);
        public void Initialize() => InitializeCount++;
        public void Reset() => ResetCount++;
    }

    public class UniSpawnerTests
    {
        private GameObject _root;
        private GameObject _prefab;
        private UniSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Root");
            _prefab = new GameObject("Prefab", typeof(TestPoolItem));
            _prefab.SetActive(false);

            _spawner = new UniSpawner(_prefab.GetComponent<IPoolItem>(), _root.transform, initialCapacity: 1);
        }

        [TearDown]
        public void TearDown()
        {
            _spawner.Dispose();
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void Spawn_ReturnsTheSpawnedItem()
        {
            // Spawn used to return void, so callers had no handle on what they just spawned
            // and had to dig through ActiveItems to find it.
            var item = _spawner.Spawn();

            Assert.IsNotNull(item);
            Assert.AreEqual(1, _spawner.ActiveCount);
            CollectionAssert.Contains(_spawner.ActiveItems, item);
        }

        [Test]
        public void Spawn_ActivatesAndInitializes()
        {
            var item = (TestPoolItem)_spawner.Spawn();

            Assert.IsTrue(item.GameObject.activeSelf);
            Assert.AreEqual(1, item.InitializeCount);
        }

        [Test]
        public void Spawn_DefaultRotation_IsIdentityNotZeroQuaternion()
        {
            // default(Quaternion) is (0,0,0,0) — not identity but an invalid quaternion that
            // yields NaN transforms. The parameter is nullable so the default can be identity.
            var item = _spawner.Spawn();

            Assert.AreEqual(Quaternion.identity, item.Transform.rotation);
            Assert.IsFalse(float.IsNaN(item.Transform.eulerAngles.x));
        }

        [Test]
        public void Spawn_AppliesPositionAndRotation()
        {
            var position = new Vector3(1f, 2f, 3f);
            var rotation = Quaternion.Euler(0f, 90f, 0f);

            var item = _spawner.Spawn(position: position, rotation: rotation);

            Assert.AreEqual(position, item.Transform.position);
            Assert.That(Quaternion.Angle(rotation, item.Transform.rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void Spawn_PassesDataBeforeInitialize()
        {
            var data = new TestPoolItemData { Value = 7 };

            var item = _spawner.Spawn<TestPoolItem>(data);

            Assert.AreSame(data, item.Data);
        }

        [Test]
        public void SpawnGeneric_WrongType_Throws()
            => Assert.Throws<InvalidCastException>(() => _spawner.Spawn<WrongItem>());

        [Test]
        public void Return_RemovesFromActiveAndResets()
        {
            var item = (TestPoolItem)_spawner.Spawn();

            _spawner.Return(item);

            Assert.AreEqual(0, _spawner.ActiveCount);
            Assert.AreEqual(1, item.ResetCount);
            Assert.IsFalse(item.GameObject.activeSelf);
        }

        [Test]
        public void Return_Twice_IsIgnored()
        {
            var item = _spawner.Spawn();

            _spawner.Return(item);

            // ObjectPool's collectionCheck throws on a double release; the spawner filters
            // the second call out by tracking what it handed out.
            Assert.DoesNotThrow(() => _spawner.Return(item));
        }

        [Test]
        public void Return_Null_IsIgnored() => Assert.DoesNotThrow(() => _spawner.Return(null));

        [Test]
        public void ReturnedItem_IsReusedRatherThanReinstantiated()
        {
            var first = _spawner.Spawn();
            _spawner.Return(first);

            var second = _spawner.Spawn();

            Assert.AreSame(first, second);
        }

        [Test]
        public void ReturnAll_ClearsActiveButKeepsPooledInstances()
        {
            _spawner.Spawn();
            _spawner.Spawn();

            _spawner.ReturnAll();

            Assert.AreEqual(0, _spawner.ActiveCount);
            Assert.AreEqual(2, _spawner.InactiveCount);
        }

        [Test]
        public void ClearSpawns_EmptiesActiveAndPool()
        {
            _spawner.Spawn();
            _spawner.Spawn();

            _spawner.ClearSpawns();

            Assert.AreEqual(0, _spawner.ActiveCount);
            Assert.AreEqual(0, _spawner.InactiveCount);
        }

        [Test]
        public void Prewarm_PopulatesPoolWithoutActivating()
        {
            _spawner.Prewarm(5);

            Assert.AreEqual(0, _spawner.ActiveCount);
            Assert.AreEqual(5, _spawner.InactiveCount);
        }

        [Test]
        public void Item_ReturnsItselfThroughItsReturner()
        {
            var item = _spawner.Spawn<TestPoolItem>();

            item.Return();

            Assert.AreEqual(0, _spawner.ActiveCount);
        }

        [Test]
        public void Spawn_AfterDispose_Throws()
        {
            _spawner.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _spawner.Spawn());
        }

        [Test]
        public void Constructor_NullPrefab_Throws()
            => Assert.Throws<ArgumentNullException>(() => new UniSpawner(null, _root.transform, 1));

        [Test]
        public void Constructor_PrefabWithoutPoolItem_ThrowsOnFirstSpawn()
        {
            var bare = new GameObject("Bare");

            try
            {
                var spawner = new UniSpawner(new FakePoolItem(bare), _root.transform, 1);
                Assert.Throws<MissingComponentException>(() => spawner.Spawn());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bare);
            }
        }

        private sealed class WrongItem : MonoBehaviour, IPoolItem
        {
            public GameObject GameObject => gameObject;
            public Transform Transform => transform;
            public void SetPoolItemReturner(IPoolItemReturner returner) { }
            public void Return() { }
            public void Initialize() { }
            public void Reset() { }
        }

        /// <summary>
        /// Stands in for a prefab whose root carries no IPoolItem component.
        /// </summary>
        private sealed class FakePoolItem : IPoolItem
        {
            public FakePoolItem(GameObject go) => GameObject = go;
            public GameObject GameObject { get; }
            public Transform Transform => GameObject.transform;
            public void SetPoolItemReturner(IPoolItemReturner returner) { }
            public void Return() { }
            public void Initialize() { }
            public void Reset() { }
        }
    }
}
