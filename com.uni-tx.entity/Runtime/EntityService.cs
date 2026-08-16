using System.Collections.Generic;
using System.Linq;
using UniTx.Content;
using UniTx.IoC;

namespace UniTx.Entity
{
    /// <summary>
    /// Registers, loads, and unloads entities described by the content service.
    /// </summary>
    public sealed class EntityService : IEntityService, IEntityLoader
    {
        private readonly IDictionary<string, IEntity> _registry;
        private readonly IContentService _contentService;
        private readonly IResolver _resolver;

        /// <summary>
        /// Creates the service resolving content and resolver dependencies from the global container.
        /// </summary>
        public EntityService() : this(IoCStatics.Resolver)
        {
        }

        /// <summary>
        /// Creates the service with explicitly provided dependencies for testability.
        /// </summary>
        /// <param name="resolver">The resolver to use for dependency resolution.</param>
        public EntityService(IResolver resolver)
        {
            _registry = new Dictionary<string, IEntity>();
            _resolver = resolver ?? throw new System.ArgumentNullException(nameof(resolver));
            _contentService = _resolver.Resolve<IContentService>();
        }

        /// <summary>
        /// Creates and registers all entities described by the loaded content data.
        /// </summary>
        public void LoadEntities()
        {
            var data = _contentService.GetAllData<IEntityData>();

            foreach (var datum in data)
            {
                var entity = datum.CreateEntity();
                entity.Inject(_resolver);
                entity.Initialize();
                _registry[entity.Id] = entity;
            }
        }

        /// <summary>
        /// Resets and unregisters all currently loaded entities.
        /// </summary>
        public void UnloadEntities()
        {
            foreach (var entity in _registry.Values)
            {
                entity.Reset();
            }

            _registry.Clear();
        }

        /// <summary>
        /// Retrieves the entity with the given id.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to cast to.</typeparam>
        /// <param name="id">The unique entity id.</param>
        /// <returns>The matching entity instance.</returns>
        public TEntity Get<TEntity>(string id)
            where TEntity : IEntity
        {
            if (_registry.TryGetValue(id, out var entity) && entity is TEntity typedEntity)
            {
                return typedEntity;
            }

            throw new KeyNotFoundException($"Entity with Id '{id}' not found.");
        }

        /// <summary>
        /// Retrieves all registered entities of the given type.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to filter by.</typeparam>
        /// <returns>An enumerable of matching entities.</returns>
        public IEnumerable<TEntity> GetAll<TEntity>()
            where TEntity : IEntity
            => _registry.Values.OfType<TEntity>();
    }
}
