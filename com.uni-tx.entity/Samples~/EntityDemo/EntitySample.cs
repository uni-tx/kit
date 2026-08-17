using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.IoC;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.Entity.Samples
{
    /// <summary>
    /// Static, designer-authored data for a hero. Never changes at runtime.
    /// </summary>
    [Serializable]
    public sealed class HeroData : IEntityData
    {
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField] private int _baseAttack;
        [SerializeField] private int _healthPerLevel;

        /// <inheritdoc />
        public string Id => _id;

        /// <inheritdoc />
        public string Name => _name;

        /// <summary>
        /// Gets the base attack before level scaling.
        /// </summary>
        public int BaseAttack => _baseAttack;

        /// <summary>
        /// Gets how much health each level grants.
        /// </summary>
        public int HealthPerLevel => _healthPerLevel;

        /// <inheritdoc />
        public IEntity CreateEntity() => new Hero(Id);
    }

    /// <summary>
    /// Per-player state for a hero. This is what gets written to disk.
    /// </summary>
    [Serializable]
    public sealed class HeroSavedData : ISavedData
    {
        [SerializeField] private string _id;
        [SerializeField] private long _modifiedTimestamp;
        [SerializeField] private int _level = 1;
        [SerializeField] private int _experience;

        /// <inheritdoc />
        public string Id
        {
            get => _id;
            set => _id = value;
        }

        /// <inheritdoc />
        public long ModifiedTimestamp
        {
            get => _modifiedTimestamp;
            set => _modifiedTimestamp = value;
        }

        /// <summary>
        /// Gets the current level.
        /// </summary>
        public int Level => _level;

        /// <summary>
        /// Gets accumulated experience.
        /// </summary>
        public int Experience => _experience;

        /// <summary>
        /// Adds experience and levels up every 100 points.
        /// </summary>
        public void AddExperience(int amount)
        {
            _experience += amount;

            while (_experience >= 100)
            {
                _experience -= 100;
                _level++;
            }
        }
    }

    /// <summary>
    /// A hero: static content joined with per-player saved state.
    /// </summary>
    /// <remarks>
    /// The split is the point. Balance values live in content and ship with the build;
    /// progress lives in saves and belongs to the player. Mixing them means a balance patch
    /// either cannot ship or silently rewrites player data.
    /// </remarks>
    public sealed class Hero : EntityBase<HeroData, HeroSavedData>
    {
        /// <summary>
        /// Creates a hero bound to a content id.
        /// </summary>
        public Hero(string id) : base(id) { }

        /// <summary>
        /// Gets attack scaled by the player's level.
        /// </summary>
        public int Attack => Data.BaseAttack + (SavedData.Level - 1) * 2;

        /// <summary>
        /// Gets max health derived from content and level.
        /// </summary>
        public int MaxHealth => Data.HealthPerLevel * SavedData.Level;

        /// <inheritdoc />
        protected override void OnInject(IResolver resolver)
        {
            // Resolve extra services here. The base class has already supplied the content
            // and serialization services.
        }

        /// <inheritdoc />
        protected override UniTask OnInitAsync(CancellationToken cToken)
        {
            Debug.Log($"{Data.Name} ready: level {SavedData.Level}, attack {Attack}, hp {MaxHealth}");

            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        protected override void OnReset() => Debug.Log($"{Id} unloaded");

        /// <summary>
        /// Grants experience and persists the result.
        /// </summary>
        public void GainExperience(int amount)
        {
            SavedData.AddExperience(amount);

            // Queues the write; the service batches and flushes it.
            Save();

            Debug.Log($"{Data.Name} -> level {SavedData.Level} ({SavedData.Experience}/100 xp)");
        }
    }

    /// <summary>
    /// Loading every entity described by the content that is currently in memory.
    /// </summary>
    public sealed class EntitySample : MonoBehaviour
    {
        private EntityService _entities;

        private void Start() => LoadAsync().Forget();

        private async UniTaskVoid LoadAsync()
        {
            try
            {
                // Content must already be loaded — EntityService builds entities from the
                // IEntityData objects the content service is holding.
                ContentRegistry.Register<HeroData>("heroes");

                _entities = new EntityService(IoCStatics.Resolver);

                // Creates, injects and initializes one entity per IEntityData in content.
                await _entities.LoadEntitiesAsync(this.GetCancellationTokenOnDestroy());

                foreach (var hero in _entities.GetAll<Hero>().OrderBy(h => h.Id))
                {
                    Debug.Log($"{hero.Id}: attack {hero.Attack}");
                }
            }
            catch (System.OperationCanceledException)
            {
                // Expected when the sample is destroyed mid-load.
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Grants experience to one hero.
        /// </summary>
        [ContextMenu("Grant Experience")]
        public void GrantExperience() => _entities.Get<Hero>("hero_knight").GainExperience(60);

        /// <summary>
        /// Resets and unregisters every entity, e.g. on sign-out.
        /// </summary>
        [ContextMenu("Unload Entities")]
        public void UnloadEntities() => _entities.UnloadEntities();
    }
}
