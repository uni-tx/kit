using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.DailyRewards;
using UniTx.IoC;
using UnityEngine;

namespace UniTx.Bootstrap
{
    /// <summary>
    /// Binds and starts the daily rewards calendar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A step of its own rather than part of <see cref="BindDependenciesStep"/>, which every
    /// project runs: a game without daily rewards should not pay for one.
    /// </para>
    /// <para>
    /// Place it <b>after</b> content is loaded and after the game's own economy is bound.
    /// Calendar definitions come from the content service, so nothing can be selected before
    /// they exist, and the first refresh can claim rewards — which needs the game's
    /// <see cref="IDailyRewardsRewardGranter"/> to already be resolvable.
    /// </para>
    /// </remarks>
    public sealed class DailyRewardsStep : LoadingStepBase
    {
        [Tooltip("Policy asset. Leave empty to load Resources/UniDailyRewardsConfig.")]
        [SerializeField] private UniDailyRewardsConfig _config;

        [Tooltip("Also install the service into the UniDailyRewards static facade. Turn off " +
                 "if the game resolves IDailyRewardsService explicitly everywhere.")]
        [SerializeField] private bool _installFacade = true;

        /// <summary>Policy asset used to build the service.</summary>
        public UniDailyRewardsConfig Config => _config;

        /// <summary>Whether the static <see cref="UniDailyRewards"/> facade is installed.</summary>
        public bool InstallFacade => _installFacade;

        /// <inheritdoc />
        public override async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            var binder = IoCStatics.Binder;
            var resolver = IoCStatics.Resolver;

            var backend = ResolveBackend(resolver);

            // Constructed rather than bound by type, because the config is a serialized field
            // on this step and the container cannot supply constructor arguments.
            var service = new DailyRewardsService(resolver.Resolve<IClock>(),
                resolver.Resolve<UniTx.Content.IContentService>(), backend, _config);

            if (resolver.TryResolve<IDailyRewardsRewardGranter>(out var granter))
            {
                service.SetRewardGranter(granter);
            }
            else if (resolver.TryResolve<UniTx.Rewards.IRewardService>(out var rewards))
            {
                // Default on the kit's reward service: currency rewards land in the
                // currency system, entity rewards land on registered consumer entities.
                service.SetRewardGranter(new DailyRewardsRewardGranter(rewards));
            }

            binder.BindInstance(service).AsSingleton().Conclude();

            if (_installFacade) await UniDailyRewards.InitializeAsync(service, cToken);
            else await service.InitializeAsync(cToken);
        }

        private static IDailyRewardsBackend ResolveBackend(IResolver resolver)
        {
            if (resolver.TryResolve<IDailyRewardsBackend>(out var bound)) return bound;

            var local = new LocalDailyRewardsBackend();
            local.Inject(resolver);

            return local;
        }
    }
}
