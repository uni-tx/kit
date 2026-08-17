using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniTx.Entity
{
    /// <summary>
    /// Loads and unloads all registered entity instances.
    /// </summary>
    public interface IEntityLoader
    {
        /// <summary>
        /// Creates and registers all entities described by the loaded content data.
        /// </summary>
        /// <param name="cToken">Token to cancel the load.</param>
        UniTask LoadEntitiesAsync(CancellationToken cToken = default);

        /// <summary>
        /// Resets and unregisters all currently loaded entities.
        /// </summary>
        void UnloadEntities();
    }
}
