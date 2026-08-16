namespace UniTx.Content
{
    /// <summary>
    /// Applies a single <see cref="IData"/> object to the content registry.
    /// </summary>
    internal interface IProcessStrategy
    {
        /// <summary>
        /// Applies the given data object to the registry.
        /// </summary>
        /// <param name="data">The data object to process.</param>
        void Process(IData data);
    }
}
