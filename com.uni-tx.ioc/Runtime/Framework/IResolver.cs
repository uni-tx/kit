using System.Collections.Generic;

namespace UniTx.IoC
{
    /// <summary>
    /// Resolves registered instances by contract type.
    /// </summary>
    public interface IResolver
    {
        /// <summary>
        /// Resolves a single instance of the specified contract type.
        /// </summary>
        /// <typeparam name="TContract">The contract to resolve.</typeparam>
        /// <returns>The resolved instance.</returns>
        /// <exception cref="System.InvalidOperationException">No binding is registered.</exception>
        TContract Resolve<TContract>();

        /// <summary>
        /// Resolves a single instance, returning false instead of throwing when unbound.
        /// </summary>
        /// <typeparam name="TContract">The contract to resolve.</typeparam>
        /// <param name="instance">The resolved instance, or <c>default</c> when unbound.</param>
        /// <returns><c>true</c> when a binding was found.</returns>
        /// <remarks>
        /// Use this for genuinely optional dependencies, so a missing binding reads as a
        /// feature being off rather than surfacing as a startup exception.
        /// </remarks>
        bool TryResolve<TContract>(out TContract instance);

        /// <summary>
        /// Indicates whether a binding exists for the specified contract type.
        /// </summary>
        /// <typeparam name="TContract">The contract to check.</typeparam>
        bool IsBound<TContract>();

        /// <summary>
        /// Resolves all registered instances of the specified contract type.
        /// </summary>
        /// <typeparam name="TContract">The contract to resolve.</typeparam>
        IEnumerable<TContract> ResolveAll<TContract>();
    }
}
