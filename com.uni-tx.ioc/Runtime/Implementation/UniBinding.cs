using System;

namespace UniTx.IoC
{
    /// <summary>
    /// Default <see cref="IBinding"/> implementation.
    /// </summary>
    internal sealed class UniBinding : IBinding
    {
        private object _instance;
        private bool _isSingleton = true;

        /// <summary>
        /// Gets the concrete type this binding resolves to.
        /// </summary>
        public Type ConcreteType { get; }

        /// <summary>
        /// Creates a binding for the given concrete type.
        /// </summary>
        /// <param name="concreteType">The type to construct or return.</param>
        /// <param name="instance">An optional pre-built instance.</param>
        public UniBinding(Type concreteType, object instance)
        {
            ConcreteType = concreteType ?? throw new ArgumentNullException(nameof(concreteType));
            _instance = instance;
        }

        /// <inheritdoc />
        public IBinding AsSingleton()
        {
            _isSingleton = true;
            return this;
        }

        /// <inheritdoc />
        public IBinding AsTransient()
        {
            if (_instance != null)
            {
                throw new InvalidOperationException(
                    $"'{ConcreteType.Name}' was bound with an existing instance, so it cannot be " +
                    "transient — a transient binding constructs a new instance per resolve.");
            }

            _isSingleton = false;
            return this;
        }

        /// <inheritdoc />
        public void Conclude()
        {
            // Constructing the singleton now, rather than on first resolve, keeps allocation
            // inside the loading step where a hitch is invisible instead of mid-gameplay.
            if (_isSingleton && _instance == null) _instance = Create();
        }

        /// <summary>
        /// Returns the singleton instance, or a fresh one for a transient binding.
        /// </summary>
        public object GetInstance()
        {
            if (!_isSingleton) return Create();

            return _instance ??= Create();
        }

        private object Create()
        {
            try
            {
                return Activator.CreateInstance(ConcreteType);
            }
            catch (MissingMethodException ex)
            {
                throw new InvalidOperationException(
                    $"'{ConcreteType.Name}' has no public parameterless constructor, so the " +
                    "container cannot build it. Construct it yourself and use " +
                    "IBinder.BindInstance instead.", ex);
            }
        }
    }
}
