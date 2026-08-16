using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniTx.Core
{
    /// <summary>
    /// Base interface for all initializable components.
    /// </summary>
    public interface IInitializableBase
    {
        // Empty
    }

    /// <summary>
    /// Interface for synchronous initialization.
    /// </summary>
    public interface IInitializable : IInitializableBase
    {
        /// <summary>
        /// Initializes the component synchronously.
        /// </summary>
        void Initialize();
    }

    /// <summary>
    /// Interface for asynchronous initialization.
    /// </summary>
    public interface IInitializableAsync : IInitializableBase
    {
        /// <summary>
        /// Initializes the component asynchronously.
        /// </summary>
        /// <param name="cToken">Cancellation token.</param>
        /// <returns>Async task.</returns>
        UniTask InitializeAsync(CancellationToken cToken = default);
    }
}