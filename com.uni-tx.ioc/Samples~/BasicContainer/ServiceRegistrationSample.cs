using UnityEngine;

namespace UniTx.IoC.Samples
{
    /// <summary>
    /// Binding, resolving and unbinding services with the UniTx container.
    /// </summary>
    /// <remarks>
    /// Drop this on any GameObject and press Play. The container itself is created
    /// automatically at <c>BeforeSceneLoad</c>, so nothing needs to be wired in the scene.
    /// </remarks>
    public sealed class ServiceRegistrationSample : MonoBehaviour
    {
        private void Start()
        {
            var binder = IoCStatics.Binder;
            var resolver = IoCStatics.Resolver;

            // ---------------------------------------------------------------------------
            // 1. Singleton — one instance shared by everyone that resolves it.
            //    Conclude() constructs it now rather than on first resolve, so the
            //    allocation lands during loading instead of mid-gameplay.
            // ---------------------------------------------------------------------------
            binder.Bind<ScoreService>().AsSingleton().Conclude();

            var a = resolver.Resolve<IScoreService>();
            var b = resolver.Resolve<IScoreService>();
            Debug.Log($"Same instance: {ReferenceEquals(a, b)}");   // True

            // A type is registered under itself *and* every interface it implements, so
            // either of these resolves the same object.
            Debug.Log($"By concrete type: {resolver.Resolve<ScoreService>() == a}");

            // ---------------------------------------------------------------------------
            // 2. Transient — a fresh instance for every resolve.
            //    Note there is no Conclude(): there is nothing to pre-construct.
            // ---------------------------------------------------------------------------
            binder.Bind<DamageCalculator>().AsTransient();

            var first = resolver.Resolve<IDamageCalculator>();
            var second = resolver.Resolve<IDamageCalculator>();
            Debug.Log($"Different instances: {!ReferenceEquals(first, second)}");   // True

            // ---------------------------------------------------------------------------
            // 3. BindInstance — for objects the container cannot build itself, e.g. a
            //    MonoBehaviour from the scene or a service with constructor arguments.
            // ---------------------------------------------------------------------------
            binder.BindInstance(new ConfiguredService("live")).AsSingleton().Conclude();
            Debug.Log($"Environment: {resolver.Resolve<IConfiguredService>().Environment}");

            // ---------------------------------------------------------------------------
            // 4. Optional dependencies — TryResolve instead of a try/catch around Resolve.
            //    A missing binding then reads as "the feature is off", not as an error.
            // ---------------------------------------------------------------------------
            if (resolver.TryResolve<IAnalyticsStub>(out var analytics)) analytics.Report("ready");
            else Debug.Log("No analytics bound — skipping telemetry.");

            Debug.Log($"IsBound<IScoreService>: {resolver.IsBound<IScoreService>()}");

            // ---------------------------------------------------------------------------
            // 5. ResolveAll — every binding for a contract. This is what the bootstrap's
            //    bulk inject/initialize pass uses.
            // ---------------------------------------------------------------------------
            binder.Bind<BonusScoreListener>().AsSingleton().Conclude();

            foreach (var listener in resolver.ResolveAll<IScoreListener>())
            {
                listener.OnScoreChanged(100);
            }

            // ---------------------------------------------------------------------------
            // 6. Manual injection — the kit injects rather than using constructor
            //    injection, so a service states its dependencies in one place.
            // ---------------------------------------------------------------------------
            var consumer = new ScoreConsumer();
            consumer.Inject(resolver);
            consumer.AddPoints(50);

            // ---------------------------------------------------------------------------
            // 7. Teardown. Unbind removes the concrete type and every interface it was
            //    registered under.
            // ---------------------------------------------------------------------------
            binder.Unbind<ScoreService>();
            Debug.Log($"Still bound after unbind: {resolver.IsBound<IScoreService>()}");   // False
        }

        // ------------------------------------------------------------------------------
        // Contracts and implementations
        // ------------------------------------------------------------------------------

        public interface IScoreService
        {
            int Score { get; }
            void Add(int points);
        }

        public interface IScoreListener
        {
            void OnScoreChanged(int score);
        }

        public interface IDamageCalculator
        {
            int Calculate(int attack, int defence);
        }

        public interface IConfiguredService
        {
            string Environment { get; }
        }

        /// <summary>
        /// Marker for a contract nothing binds, to demonstrate TryResolve.
        /// </summary>
        public interface IAnalyticsStub
        {
            void Report(string message);
        }

        public sealed class ScoreService : IScoreService
        {
            public int Score { get; private set; }

            public void Add(int points)
            {
                Score += points;
                Debug.Log($"Score is now {Score}");
            }
        }

        public sealed class DamageCalculator : IDamageCalculator
        {
            public int Calculate(int attack, int defence) => Mathf.Max(1, attack - defence);
        }

        public sealed class BonusScoreListener : IScoreListener
        {
            public void OnScoreChanged(int score) => Debug.Log($"Listener saw score {score}");
        }

        /// <summary>
        /// Has no parameterless constructor, so it must be bound via BindInstance.
        /// </summary>
        public sealed class ConfiguredService : IConfiguredService
        {
            public ConfiguredService(string environment) => Environment = environment;

            public string Environment { get; }
        }

        /// <summary>
        /// Pulls its dependencies from the resolver when injected.
        /// </summary>
        public sealed class ScoreConsumer : IInjectable
        {
            private IScoreService _scores;

            public void Inject(IResolver resolver) => _scores = resolver.Resolve<IScoreService>();

            public void AddPoints(int points) => _scores.Add(points);
        }
    }
}
