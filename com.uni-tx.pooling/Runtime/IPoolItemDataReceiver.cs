namespace UniTx.Pooling
{
    /// <summary>
    /// Receives pool item data and applies it to the pool item.
    /// </summary>
    public interface IPoolItemDataReceiver
    {
        /// <summary>
        /// Sets or updates the data for this pool item.
        /// </summary>
        /// <param name="data">The pool item data to apply.</param>
        void SetData(IPoolItemData data);
    }
}