using System;
using System.Collections.Generic;

namespace UniTx.IoC
{
    /// <summary>
    /// Default <see cref="IResolver"/> / <see cref="IBinder"/> implementation.
    /// </summary>
    internal sealed class UniContainer : IResolver, IBinder
    {
        private readonly Dictionary<Type, List<UniBinding>> _registry = new();

        /// <inheritdoc />
        public IBinding Bind<TConcrete>()
            where TConcrete : class, new()
            => BindInternal(typeof(TConcrete), null);

        /// <inheritdoc />
        public IBinding BindInstance<TConcrete>(TConcrete instance)
            where TConcrete : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            // Bind against the runtime type, not TConcrete: passing a subclass through a base
            // type parameter would otherwise register only the base's interfaces.
            return BindInternal(instance.GetType(), instance);
        }

        /// <inheritdoc />
        public IBinding Bind(Type type, object instance = null)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            if (type.IsInterface || type.IsAbstract)
            {
                throw new ArgumentException(
                    $"Cannot bind '{type.Name}': bind a concrete type, not an interface or abstract class.",
                    nameof(type));
            }

            if (instance != null && !type.IsInstanceOfType(instance))
            {
                throw new ArgumentException(
                    $"Instance of type '{instance.GetType().Name}' is not a '{type.Name}'.", nameof(instance));
            }

            return BindInternal(type, instance);
        }

        /// <inheritdoc />
        public void Unbind<TConcrete>() => Unbind(typeof(TConcrete));

        /// <inheritdoc />
        public void Unbind(Type type)
        {
            if (type == null) return;

            // A concrete type is registered under itself and every interface it implements,
            // so all of those keys have to be swept or stale entries resolve to a dead binding.
            RemoveBindingsFor(type, type);

            foreach (var contract in type.GetInterfaces())
            {
                RemoveBindingsFor(contract, type);
            }
        }

        /// <inheritdoc />
        public void Unbind(object instance)
        {
            if (instance == null) return;

            Unbind(instance.GetType());
        }

        /// <inheritdoc />
        public void UnbindAll() => _registry.Clear();

        /// <inheritdoc />
        public TContract Resolve<TContract>()
        {
            if (_registry.TryGetValue(typeof(TContract), out var bindings) && bindings.Count > 0)
            {
                return (TContract)bindings[0].GetInstance();
            }

            throw new InvalidOperationException(
                $"No binding registered for '{typeof(TContract).Name}'. Bind it before the step " +
                "that resolves it — check the order of your AppLoader's loading steps.");
        }

        /// <inheritdoc />
        public bool TryResolve<TContract>(out TContract instance)
        {
            if (_registry.TryGetValue(typeof(TContract), out var bindings) && bindings.Count > 0)
            {
                instance = (TContract)bindings[0].GetInstance();
                return true;
            }

            instance = default;
            return false;
        }

        /// <inheritdoc />
        public bool IsBound<TContract>()
            => _registry.TryGetValue(typeof(TContract), out var bindings) && bindings.Count > 0;

        /// <inheritdoc />
        public IEnumerable<TContract> ResolveAll<TContract>()
        {
            if (!_registry.TryGetValue(typeof(TContract), out var bindings)) yield break;

            // Snapshot: callers routinely bind or unbind while iterating a ResolveAll pass
            // (the bulk inject/initialize step does exactly that), which would otherwise
            // invalidate the enumerator mid-loop.
            var snapshot = bindings.ToArray();

            foreach (var binding in snapshot)
            {
                yield return (TContract)binding.GetInstance();
            }
        }

        private IBinding BindInternal(Type type, object instance)
        {
            var binding = new UniBinding(type, instance);

            RegisterBinding(type, binding);

            foreach (var contract in type.GetInterfaces())
            {
                RegisterBinding(contract, binding);
            }

            return binding;
        }

        private void RegisterBinding(Type contract, UniBinding binding)
        {
            if (!_registry.TryGetValue(contract, out var bindings))
            {
                bindings = new List<UniBinding>();
                _registry[contract] = bindings;
            }

            bindings.Add(binding);
        }

        private void RemoveBindingsFor(Type contract, Type concreteType)
        {
            if (!_registry.TryGetValue(contract, out var bindings)) return;

            bindings.RemoveAll(b => b.ConcreteType == concreteType);

            if (bindings.Count == 0) _registry.Remove(contract);
        }
    }
}
