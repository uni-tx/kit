using System;

namespace UniTx.IoC
{
    /// <summary>
    /// Registers concrete types against every interface they implement.
    /// </summary>
    public interface IBinder
    {
        /// <summary>
        /// Binds <typeparamref name="TConcrete"/>, creating it on demand.
        /// </summary>
        /// <typeparam name="TConcrete">A class with a parameterless constructor.</typeparam>
        IBinding Bind<TConcrete>()
            where TConcrete : class, new();

        /// <summary>
        /// Binds an already-constructed instance.
        /// </summary>
        /// <typeparam name="TConcrete">The concrete type of the instance.</typeparam>
        /// <param name="instance">The instance to register.</param>
        /// <remarks>
        /// Unlike <see cref="Bind{TConcrete}()"/> this has no <c>new()</c> constraint, so it
        /// accepts types the container could not construct itself — a MonoBehaviour pulled
        /// from the scene, or a service with constructor arguments.
        /// </remarks>
        IBinding BindInstance<TConcrete>(TConcrete instance)
            where TConcrete : class;

        /// <summary>
        /// Binds the specified <see cref="Type"/>, optionally with a pre-built instance.
        /// </summary>
        /// <param name="type">The concrete type to bind.</param>
        /// <param name="instance">An optional pre-built instance.</param>
        IBinding Bind(Type type, object instance = null);

        /// <summary>
        /// Unbinds all registrations of <typeparamref name="TConcrete"/>.
        /// </summary>
        void Unbind<TConcrete>();

        /// <summary>
        /// Unbinds all registrations of the specified <see cref="Type"/>.
        /// </summary>
        void Unbind(Type type);

        /// <summary>
        /// Unbinds all registrations matching the instance's concrete type.
        /// </summary>
        void Unbind(object instance);

        /// <summary>
        /// Removes every binding.
        /// </summary>
        void UnbindAll();
    }
}
