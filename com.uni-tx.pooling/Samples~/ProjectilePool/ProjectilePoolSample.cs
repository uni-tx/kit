using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;
using UniTx.Pooling;
using UnityEngine;

namespace UniTx.Pooling.Samples
{
    /// <summary>
    /// Data handed to a projectile as it spawns.
    /// </summary>
    public sealed class ProjectileData : IPoolItemData
    {
        public Vector3 Direction;
        public float Speed;
        public float Lifetime;
        public int Damage;
    }

    /// <summary>
    /// A pooled projectile: moves, expires, and returns itself to the pool.
    /// </summary>
    /// <remarks>
    /// <b>Setup:</b> make a prefab with this component and a Rigidbody/Collider if you want
    /// hits, then assign it to <see cref="ProjectilePoolSample"/>.
    /// </remarks>
    public sealed class Projectile : MonoBehaviour, IPoolItem<ProjectileData>, IInjectable
    {
        private IPoolItemReturner _returner;
        private CancellationTokenSource _lifeCts;

        /// <inheritdoc />
        public ProjectileData Data { get; private set; }

        /// <inheritdoc />
        public GameObject GameObject => gameObject;

        /// <inheritdoc />
        public Transform Transform => transform;

        /// <inheritdoc />
        public void SetPoolItemReturner(IPoolItemReturner returner) => _returner = returner;

        /// <inheritdoc />
        public void SetData(IPoolItemData data) => Data = (ProjectileData)data;

        /// <summary>
        /// Optional. Called once when the pool creates the instance, not on every spawn.
        /// </summary>
        public void Inject(IResolver resolver)
        {
            // Resolve anything the projectile needs here — a damage service, an audio config
            // — so the per-spawn path stays allocation-free.
        }

        /// <summary>
        /// Called every time the projectile is spawned, after data and placement are applied.
        /// </summary>
        public void Initialize()
        {
            _lifeCts?.Cancel();
            _lifeCts?.Dispose();
            _lifeCts = new CancellationTokenSource();

            ExpireAsync(_lifeCts.Token).Forget();
        }

        /// <summary>
        /// Called every time the projectile is returned. Leave no state behind for the next
        /// spawn — a pooled object that keeps state is the classic pooling bug.
        /// </summary>
        public void Reset()
        {
            _lifeCts?.Cancel();
            _lifeCts?.Dispose();
            _lifeCts = null;
            Data = null;
        }

        /// <inheritdoc />
        public void Return() => _returner.Return(this);

        private void Update()
        {
            if (Data == null) return;

            transform.position += Data.Direction * (Data.Speed * Time.deltaTime);
        }

        private async UniTaskVoid ExpireAsync(CancellationToken cToken)
        {
            // SuppressCancellationThrow keeps a returned-early projectile from surfacing an
            // OperationCanceledException in the console every time.
            var canceled = await UniTask
                .Delay(System.TimeSpan.FromSeconds(Data.Lifetime), cancellationToken: cToken)
                .SuppressCancellationThrow();

            if (!canceled) Return();
        }

        private void OnDestroy() => Reset();
    }

    /// <summary>
    /// Fires pooled projectiles. Drop on a GameObject, assign the prefab, press Play.
    /// </summary>
    public sealed class ProjectilePoolSample : MonoBehaviour
    {
        [Tooltip("A prefab whose root carries the Projectile component.")]
        [SerializeField] private Projectile _projectilePrefab;

        [SerializeField, Min(0)] private int _prewarmCount = 20;
        [SerializeField, Min(0.01f)] private float _fireInterval = 0.1f;

        private UniSpawner _spawner;
        private CancellationTokenSource _cts;

        private void Start()
        {
            if (_projectilePrefab == null)
            {
                Debug.LogError("Assign a Projectile prefab first.", this);
                return;
            }

            // The spawner wraps UnityEngine.Pool.ObjectPool rather than reimplementing it,
            // adding lifecycle hooks, typed data and dependency injection.
            _spawner = new UniSpawner(
                prefab: _projectilePrefab,
                parent: transform,
                initialCapacity: _prewarmCount,
                maxSize: 200);

            // Inject before prewarming, so instances created up front get their dependencies.
            if (IoCStatics.IsInitialized) _spawner.Inject(IoCStatics.Resolver);

            // Build the instances now. Without this the first burst instantiates mid-combat,
            // which is exactly where a frame spike is most visible.
            _spawner.Prewarm(_prewarmCount);

            _cts = new CancellationTokenSource();
            FireLoopAsync(_cts.Token).Forget();
        }

        private void OnDestroy()
        {
            _cts.SafeCancelAndDispose();
            _spawner?.Dispose();
        }

        private async UniTaskVoid FireLoopAsync(CancellationToken cToken)
        {
            var data = new ProjectileData
            {
                Speed = 12f,
                Lifetime = 2f,
                Damage = 10,
            };

            while (!cToken.IsCancellationRequested)
            {
                data.Direction = transform.forward;

                // Spawn returns the item, so you can keep a handle on what you just fired.
                // Rotation defaults to Quaternion.identity — passing default(Quaternion)
                // would be (0,0,0,0), an invalid quaternion that yields NaN transforms.
                var projectile = _spawner.Spawn<Projectile>(data, transform.position);

                Debug.Log($"Fired. active={_spawner.ActiveCount} pooled={_spawner.InactiveCount} " +
                          $"damage={projectile.Data.Damage}");

                await UniTask.Delay(System.TimeSpan.FromSeconds(_fireInterval), cancellationToken: cToken);
            }
        }

        /// <summary>
        /// Returns everything in flight without destroying the pooled instances.
        /// </summary>
        [ContextMenu("Return All")]
        public void ReturnAll() => _spawner?.ReturnAll();
    }
}
