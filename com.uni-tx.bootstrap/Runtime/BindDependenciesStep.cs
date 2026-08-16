using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Core;
using UniTx.Entity;
using UniTx.IoC;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.Bootstrap
{
    /// <summary>
    /// Binds the kit's core services into the IoC container.
    /// </summary>
    public sealed class BindDependenciesStep : LoadingStepBase
    {
        [Tooltip("Bind ServerClock instead of LocalClock. Server time survives the player " +
                 "changing the device clock, at the cost of one HTTPS request at startup.")]
        [SerializeField] private bool _useServerClock;

        /// <inheritdoc />
        public override UniTask InitializeAsync(CancellationToken cToken = default)
        {
            // Resolved here rather than via IInjectable so the step works even when the
            // AppLoader is driven manually in a test.
            var binder = IoCStatics.Binder;

            binder.Bind<UnityEventListener>().AsSingleton().Conclude();

            if (_useServerClock) binder.Bind<ServerClock>().AsSingleton().Conclude();
            else binder.Bind<LocalClock>().AsSingleton().Conclude();

            binder.Bind<ContentService>().AsSingleton().Conclude();
            binder.Bind<SerialisationService>().AsSingleton().Conclude();
            binder.Bind<EntityService>().AsSingleton().Conclude();

            return UniTask.CompletedTask;
        }
    }
}
