using System;
using UnityEngine;

namespace UniTx.IoC
{
    /// <summary>
    /// Global access to the kit's dependency container.
    /// </summary>
    public static class IoCStatics
    {
        private static UniContainer _container;

        /// <summary>
        /// Gets the global resolver, initialized before the first scene loads.
        /// </summary>
        public static IResolver Resolver => _container
            ?? throw new InvalidOperationException(
                "IoC container is not initialized. It is created at BeforeSceneLoad; " +
                "call IoCStatics.Initialize() first when resolving from an edit-mode test.");

        /// <summary>
        /// Gets the global binder, initialized before the first scene loads.
        /// </summary>
        /// <remarks>
        /// Previously the only route to the binder was
        /// <c>Resolver.Resolve&lt;IBinder&gt;()</c>, which worked by accident because the
        /// container binds itself. Exposing it directly makes the intent explicit.
        /// </remarks>
        public static IBinder Binder => _container
            ?? throw new InvalidOperationException(
                "IoC container is not initialized. It is created at BeforeSceneLoad; " +
                "call IoCStatics.Initialize() first when binding from an edit-mode test.");

        /// <summary>
        /// Indicates whether the container has been created.
        /// </summary>
        public static bool IsInitialized => _container != null;

        /// <summary>
        /// Creates a fresh container, discarding any existing bindings.
        /// </summary>
        /// <remarks>
        /// Runs automatically at <see cref="RuntimeInitializeLoadType.BeforeSceneLoad"/>.
        /// Call it manually from edit-mode tests, which never enter play mode.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            _container = new UniContainer();

            // Bind the container to itself so IResolver and IBinder are resolvable like any
            // other service — services that take an IResolver dependency then work uniformly.
            _container.BindInstance(_container).AsSingleton().Conclude();
        }

        /// <summary>
        /// Destroys the container and all its bindings.
        /// </summary>
        /// <remarks>
        /// With <b>Enter Play Mode Options ▸ Reload Domain</b> disabled, statics survive
        /// between play sessions; <see cref="Initialize"/> replaces the container each run,
        /// so leftover singletons never leak into the next session.
        /// </remarks>
        public static void Reset() => _container = null;
    }
}
