using UniTx.Core;

namespace UniTx.Pooling
{
    /// <summary>
    /// Interface for a pool item.
    /// </summary>
    public interface IPoolItem : IInitializable, IResettable, ISceneEntity
    {
        /// <summary>
        /// Sets the pool item returner.
        /// </summary>
        /// <param name="returner">The pool item returner.</param>
        void SetPoolItemReturner(IPoolItemReturner returner);

        /// <summary>
        /// Returns the pool item to the pool.
        /// </summary>
        void Return();
    }

    /// <summary>
    /// Interface for a pool item with data.
    /// </summary>
    /// <typeparam name="TData">The type of data to be stored in the pool item.</typeparam>
    public interface IPoolItem<TData> : IPoolItem, IPoolItemDataReceiver
        where TData : IPoolItemData
    {
        /// <summary>
        /// The data of the pool item.
        /// </summary>
        TData Data { get; }
    }
}