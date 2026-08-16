using System.Collections.Generic;

namespace UniTx.Entity
{
    /// <summary>
    /// Service for retrieving registered entity instances by id or type.
    /// </summary>
    public interface IEntityService
    {
        /// <summary>
        /// Retrieves the entity with the given id.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to cast to.</typeparam>
        /// <param name="id">The unique entity id.</param>
        /// <returns>The matching entity instance.</returns>
        TEntity Get<TEntity>(string id)
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
