using System;
using System.Collections.Generic;
using UniTx.IoC;
using UnityEngine;
using UnityEngine.Pool;

namespace UniTx.Pooling
{
    /// <summary>
    /// Spawns and recycles <see cref="IPoolItem"/> instances from a prefab.
    /// </summary>
    /// <remarks>
    /// Wraps <see cref="ObjectPool{T}"/> rather than reimplementing pooling, adding the
    /// kit's lifecycle hooks (<c>Initialize</c>/<c>Reset</c>), optional typed data
    /// injection, and dependency injection for pooled items.
    /// </remarks>
    public sealed class UniSpawner : IInjectable, IPoolItemReturner, IDisposable
    {
        private readonly HashSet<IPoolItem> _activeItems = new();
        private readonly IPoolItem _prefab;
        private readonly Transform _parent;
        private readonly ObjectPool<IPoolItem> _pool;

        private IResolver _resolver;
        private bool _isDisposed;

        /// <summary>
        /// Gets the currently spawned items.
        /// </summary>
        public IReadOnlyCollection<IPoolItem> ActiveItems => _activeItems;

        /// <summary>
        /// Gets how many items are currently spawned.
        /// </summary>
        public int ActiveCount => _activeItems.Count;

        /// <summary>
        /// Gets how many inactive items are waiting in the pool.
        /// </summary>
        public int InactiveCount => _pool.CountInactive;

        /// <summary>
        /// Creates a spawner for the given prefab.
        /// </summary>
        /// <param name="prefab">Prefab carrying an <see cref="IPoolItem"/> component.</param>
        /// <param name="parent">Transform new instances are parented to.</param>
        /// <param name="initialCapacity">Capacity the backing pool is sized for.</param>
        /// <param name="maxSize">Above this, released items are destroyed instead of pooled.</param>
        public UniSpawner(IPoolItem prefab, Transform parent, int initialCapacity, int maxSize = 1000)
        {
            _prefab = prefab ?? throw new ArgumentNullException(nameof(prefab));

            if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            if (maxSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxSize));

            _parent = parent;
            _pool = new ObjectPool<IPoolItem>(
                createFunc: Create,
                actionOnGet: null,
                actionOnRelease: OnRelease,
                actionOnDestroy: OnDestroyItem,
                collectionCheck: true,
                defaultCapacity: initialCapacity,
                maxSize: maxSize);
        }

        /// <inheritdoc/>
        public void Inject(IResolver resolver) => _resolver = resolver;

        /// <summary>
        /// Spawns an item, optionally with data and placement.
        /// </summary>
        /// <param name="data">Optional data handed to the item before it initializes.</param>
        /// <param name="position">World position; defaults to the origin.</param>
        /// <param name="rotation">World rotation; defaults to <see cref="Quaternion.identity"/>.</param>
        /// <returns>The spawned item.</returns>
        /// <remarks>
        /// <paramref name="rotation"/> is nullable because <c>default(Quaternion)</c> is
        /// <c>(0,0,0,0)</c> — not the identity rotation but an invalid quaternion, which
        /// silently produces NaN transforms.
        /// </remarks>
        public IPoolItem Spawn(IPoolItemData data = null, Vector3 position = default, Quaternion? rotation = null)
        {
            ThrowIfDisposed();

            var item = _pool.Get();

            // Position before activating: OnEnable and Awake run on SetActive(true), and a
            // trail or particle system that starts at the origin then teleports is a visible
            // artifact.
            item.Transform.SetPositionAndRotation(position, rotation ?? Quaternion.identity);

            if (data != null && item is IPoolItemDataReceiver receiver)
            {
                receiver.SetData(data);
            }

            item.GameObject.SetActive(true);
            item.Initialize();
            _activeItems.Add(item);

            return item;
        }

        /// <summary>
        /// Spawns an item and returns it as <typeparamref name="TItem"/>.
        /// </summary>
        /// <typeparam name="TItem">The expected pool item type.</typeparam>
        /// <param name="data">Optional data handed to the item before it initializes.</param>
        /// <param name="position">World position; defaults to the origin.</param>
        /// <param name="rotation">World rotation; defaults to <see cref="Quaternion.identity"/>.</param>
        public TItem Spawn<TItem>(IPoolItemData data = null, Vector3 position = default,
            Quaternion? rotation = null)
            where TItem : class, IPoolItem
        {
            var item = Spawn(data, position, rotation);

            if (item is TItem typed) return typed;

            // Put it back before throwing, or a caller who catches the mismatch leaks a live
            // instance that never returns to the pool.
            var actualType = item.GetType().Name;
            Return(item);

            throw new InvalidCastException(
                $"Pooled item is '{actualType}', not '{typeof(TItem).Name}'.");
        }

        /// <summary>
        /// Creates instances up front so the first spawn does not allocate mid-gameplay.
        /// </summary>
        /// <param name="count">How many instances to create.</param>
        public void Prewarm(int count)
        {
            ThrowIfDisposed();

            if (count <= 0) return;

            // Get-then-release is the only way to seed ObjectPool; the items land inactive
            // in the pool ready for the first real spawn.
            var buffer = new IPoolItem[count];

            for (var i = 0; i < count; i++)
            {
                buffer[i] = _pool.Get();
            }

            for (var i = 0; i < count; i++)
            {
                _pool.Release(buffer[i]);
            }
        }

        /// <inheritdoc/>
        public void Return(IPoolItem item)
        {
            if (item == null || _isDisposed) return;

            // Only release items this spawner handed out; ObjectPool's collectionCheck would
            // otherwise throw on a double return.
            if (_activeItems.Remove(item)) _pool.Release(item);
        }

        /// <summary>
        /// Returns every active item to the pool, keeping the pooled instances alive.
        /// </summary>
        public void ReturnAll()
        {
            if (_activeItems.Count == 0) return;

            // Copy first: Return mutates _activeItems.
            var buffer = new IPoolItem[_activeItems.Count];
            _activeItems.CopyTo(buffer);

            foreach (var item in buffer)
            {
                Return(item);
            }
        }

        /// <summary>
        /// Returns every active item and destroys all pooled instances.
        /// </summary>
        public void ClearSpawns()
        {
            ReturnAll();
            _pool.Clear();
        }

        /// <summary>
        /// Clears the pool and blocks further spawning.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            ClearSpawns();
            _isDisposed = true;
        }

        private IPoolItem Create()
        {
            var instance = UnityEngine.Object.Instantiate(_prefab.GameObject, _parent);
            instance.SetActive(false);

            var item = instance.GetComponent<IPoolItem>();

            if (item == null)
            {
                UnityEngine.Object.Destroy(instance);
                throw new MissingComponentException(
                    $"Prefab '{_prefab.GameObject.name}' assigned to UniSpawner has no IPoolItem component.");
            }

            item.SetPoolItemReturner(this);

            if (item is IInjectable injectable) injectable.Inject(_resolver);

            return item;
        }

        private static void OnRelease(IPoolItem item)
        {
            item.Reset();
            item.GameObject.SetActive(false);
        }

        private static void OnDestroyItem(IPoolItem item)
        {
            if (item?.GameObject != null) UnityEngine.Object.Destroy(item.GameObject);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(UniSpawner));
        }
    }
}
