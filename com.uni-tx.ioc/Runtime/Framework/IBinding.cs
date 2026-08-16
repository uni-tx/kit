namespace UniTx.IoC
{
    /// <summary>
    /// Represents a binding configuration for a registered type.
    /// </summary>
    public interface IBinding
    {
        /// <summary>
        /// Configures the binding as a singleton. The same instance will be returned for every resolution.
        /// </summary>
        IBinding AsSingleton();

        /// <summary>
        /// Configures the binding as transient. A new instance will be created for every resolution.
        /// </summary>
        IBinding AsTransient();

        /// <summary>
        /// Finalizes the binding configuration and ensures immediate instantiation if configured as a singleton.
        /// </summary>
        void Conclude();
    }
}