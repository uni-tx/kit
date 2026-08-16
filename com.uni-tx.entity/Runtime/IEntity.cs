using UniTx.Core;
using UniTx.IoC;

namespace UniTx.Entity
{
    /// <summary>
    /// Runtime entity contract with lifecycle and persistence support.
    /// </summary>
    public interface IEntity : IInjectable, IInitializable, IResettable
    {
        /// <summary>
        /// Gets the unique identifier of the entity.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Persists the entity's saved data via the serialisation service.
        /// </summary>
        void Save();
    }
}
