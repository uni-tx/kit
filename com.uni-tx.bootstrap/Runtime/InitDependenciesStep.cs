using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;

namespace UniTx.Bootstrap
{
    /// <summary>
    /// Injects dependencies into every bound service, then initializes them.
    /// </summary>
    public sealed class InitDependenciesStep : LoadingStepBase
    {
        /// <inheritdoc />
        public override async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            var resolver = IoCStatics.Resolver;

            // Materialize before iterating. ResolveAll is lazy, and a service's Inject or
            // Initialize may bind further services — enumerating live would then either miss
            // them or revisit them depending on ordering.
            var injectables = resolver.ResolveAll<IInjectable>().ToArray();
            var initializables = resolver.ResolveAll<IInitializable>().ToArray();
            var initializablesAsync = resolver.ResolveAll<IInitializableAsync>().ToArray();

            foreach (var injectable in injectables)
            {
                injectable.Inject(resolver);
            }

            // Injection completes for everything before any Initialize runs, so a service is
            // never initialized while one of its dependencies is still unset.
            foreach (var initializable in Distinct(initializables))
            {
                UniStatics.LogInfo($"Initializing {initializable.GetType().Name}", this);
                initializable.Initialize();
            }

            foreach (var initializable in Distinct(initializablesAsync))
            {
                cToken.ThrowIfCancellationRequested();
                UniStatics.LogInfo($"Initializing {initializable.GetType().Name} (async)", this);
                await initializable.InitializeAsync(cToken);
            }
        }

        private static IEnumerable<T> Distinct<T>(IReadOnlyList<T> source)
            where T : class
        {
            // A service bound under several contracts appears once per contract; initializing
            // it twice is at best wasted work and at worst duplicated event subscriptions.
            // Compared by reference, not Equals, so a service that overrides equality is not
            // silently collapsed with a different instance.
            var seen = new HashSet<T>(ReferenceComparer<T>.Instance);

            foreach (var item in source)
            {
                if (seen.Add(item)) yield return item;
            }
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public static readonly ReferenceComparer<T> Instance = new();

            public bool Equals(T x, T y) => ReferenceEquals(x, y);

            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
