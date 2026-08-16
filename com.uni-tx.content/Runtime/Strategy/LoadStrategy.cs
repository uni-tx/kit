using System.Collections.Generic;

namespace UniTx.Content
{
    /// <summary>
    /// Adds processed data objects into the registry keyed by their id.
    /// </summary>
    internal sealed class LoadStrategy : IProcessStrategy
    {
        private readonly IDictionary<string, IData> _registry;

        /// <summary>
        /// Creates the strategy against the given registry.
        /// </summary>
        /// <param name="registry">The registry to populate.</param>
        public LoadStrategy(IDictionary<string, IData> registry)
            => _registry = registry;

        public void Process(IData data)
        {
            if (data?.Id == null) return;

            _registry[data.Id] = data;
        }
    }
}
