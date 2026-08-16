namespace UniTx.Pooling
{
    /// <summary>
    /// Interface for pool item returner.
    /// </summary>
    public interface IPoolItemReturner
    {
        /// <summary>
        /// Returns the pool item to the pool.
        /// </summary>
        /// <param name="item">The pool item to return.</param>
        void Return(IPoolItem item);
    }
}