using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;
using UniTx.Store;
using UnityEngine;

namespace UniTx.Bootstrap
{
    /// <summary>
    /// Binds and starts the shop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A step of its own rather than part of <see cref="BindDependenciesStep"/>, which every
    /// project runs: a game without a shop should not pay for one.
    /// </para>
    /// <para>
    /// Place it <b>after</b> content is loaded and after the game's own economy is bound.
    /// Store definitions come from the content service, so nothing can be selected before
    /// they exist, and the first refresh can claim rewards — which needs the game's
    /// <see cref="IStoreRewardGranter"/> to already be resolvable.
    /// </para>
    /// </remarks>
    public sealed class StoreStep : LoadingStepBase
    {
        [Tooltip("Policy asset. Leave empty to load Resources/UniStoreConfig.")]
        [SerializeField] private UniStoreConfig _config;

        [Tooltip("Also install the service into the UniStore static facade. Turn off " +
                 "if the game resolves IStoreService explicitly everywhere.")]
        [SerializeField] private bool _installFacade = true;

        /// <summary>Policy asset used to build the service.</summary>
        public UniStoreConfig Config => _config;

        /// <summary>Whether the static <see cref="UniStore"/> facade is installed.</summary>
        public bool InstallFacade => _installFacade;

        /// <inheritdoc />
        public override async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            var binder = IoCStatics.Binder;
            var resolver = IoCStatics.Resolver;

            var backend = ResolveBackend(resolver);

            // Constructed rather than bound by type, because the config is a serialized field
            // on this step and the container cannot supply constructor arguments.
            var service = new StoreService(resolver.Resolve<IClock>(),
                resolver.Resolve<UniTx.Content.IContentService>(), backend, _config);

            if (resolver.TryResolve<IStoreRewardGranter>(out var granter))
            {
                service.SetRewardGranter(granter);
            }
            else if (resolver.TryResolve<UniTx.Rewards.IRewardService>(out var rewards))
            {
                // Default on the kit's reward service: currency rewards land in the
                // currency system, entity rewards land on registered consumer entities.
                service.SetRewardGranter(new StoreRewardGranter(rewards));
            }

            binder.BindInstance(service).AsSingleton().Conclude();

            if (_installFacade) await UniStore.InitializeAsync(service, cToken);
            else await service.InitializeAsync(cToken);
        }

        private static IStoreBackend ResolveBackend(IResolver resolver)
        {
            if (resolver.TryResolve<IStoreBackend>(out var bound)) return bound;

            var local = new LocalStoreBackend();
            local.Inject(resolver);

            return local;
        }
    }
}
