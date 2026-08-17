using System.Collections.Generic;

namespace UniTx.Entity
{
    /// <summary>
    /// Service for retrieving registered entity instances by id or type.
    /// </summary>
    public interface IEntityService : IEntityLoader
    {
        /// <summary>
        /// Registers an entity explicitly.
        /// </summary>
        /// <param name="entity">The entity to register.</param>
        /// <remarks>
        /// Content-driven entities register themselves through
        /// <see cref="LoadEntitiesAsync"/>. This is the explicit route for singleton entities
        /// that are not described by <see cref="IEntityData"/> — a season pass or a wallet.
        /// </remarks>
        void Register(IEntity entity);

        /// <summary>
        /// Removes an entity from the registry without resetting it.
        /// </summary>
        /// <param name="entity">The entity to unregister.</param>
        void Unregister(IEntity entity);

        /// <summary>
        /// Retrieves the entity with the given id.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to cast to.</typeparam>
        /// <param name="id">The unique entity id.</param>
        /// <returns>The matching entity instance.</returns>
        TEntity Get<TEntity>(string id)
            where TEntity : IEntity;

        /// <summary>
        /// Retrieves the entity with the given id, without throwing.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to cast to.</typeparam>
        /// <param name="id">The unique entity id.</param>
        /// <param name="entity">The matching entity, or null.</param>
        /// <returns><c>true</c> when a matching entity of the requested type was found.</returns>
        bool TryGet<TEntity>(string id, out TEntity entity)
            where TEntity : IEntity;

        /// <summary>
        /// Retrieves all registered entities of the given type.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to filter by.</typeparam>
        /// <returns>An enumerable of matching entities.</returns>
        IEnumerable<TEntity> GetAll<TEntity>()
            where TEntity : IEntity;
    }
}
