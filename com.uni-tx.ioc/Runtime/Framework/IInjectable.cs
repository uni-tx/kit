namespace UniTx.IoC
{
    /// <summary>
    /// Defines a contract for types that support dependency injection.
    /// </summary>
    public interface IInjectable
    {
        /// <summary>
        /// Injects dependencies into the current instance using the given resolver.
        /// </summary>
        void Inject(IResolver resolver);
    }
}
