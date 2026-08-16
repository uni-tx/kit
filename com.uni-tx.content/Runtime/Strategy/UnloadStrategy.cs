using System.Collections.Generic;

namespace UniTx.Content
{
    /// <summary>
    /// Removes processed data objects from the registry by their id.
    /// </summary>
    internal sealed class UnloadStrategy : IProcessStrategy
    {
        private readonly IDictionary<string, IData> _registry;

        /// <summary>
        /// Creates the strategy against the given registry.
        /// </summary>
        /// <param name="registry">The registry to remove from.</param>
        public UnloadStrategy(IDictionary<string, IData> registry)
            => _registry = registry;

        public void Process(IData data)
        {
            if (data?.Id == null) return;

            _registry.Remove(data.Id);
        }
    }
}
