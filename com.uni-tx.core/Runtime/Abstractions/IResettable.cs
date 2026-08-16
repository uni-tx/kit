using Cysharp.Threading.Tasks;
using System.Threading;

namespace UniTx.Core
{
    /// <summary>
    /// Base interface for all resettable components.
    /// </summary>
    public interface IResettableBase
    {
        // Empty
    }

    /// <summary>
    /// Interface for synchronous reset.
    /// </summary>
    public interface IResettable : IResettableBase
    {
        /// <summary>
        /// Resets the component synchronously.
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// Interface for asynchronous reset.
    /// </summary>
    public interface IResettableAsync : IResettableBase
    {
        /// <summary>
        /// Resets the component asynchronously.
        /// </summary>
        /// <param name="cToken">Cancellation token.</param>
        /// <returns>Async task.</returns>
        UniTask ResetAsync(CancellationToken cToken = default);
    }
}