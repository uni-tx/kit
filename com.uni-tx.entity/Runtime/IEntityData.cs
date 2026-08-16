using UniTx.Content;

namespace UniTx.Entity
{
    /// <summary>
    /// Describes the content data required to create an entity.
    /// </summary>
    public interface IEntityData : IData
    {
        /// <summary>
        /// Gets the display name of the entity.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Creates a new entity instance from this data.
        /// </summary>
        /// <returns>The created entity.</returns>
        IEntity CreateEntity();
    }
}
