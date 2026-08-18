using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.Currency;
using UniTx.Economy;
using UniTx.IoC;
using UniTx.Rewards;
using UnityEngine;

namespace UniTx.Bootstrap
{
    /// <summary>
    /// Binds and starts the economy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A step of its own rather than part of <see cref="BindDependenciesStep"/>, which every
    /// project runs: a game without an economy should not pay for one.
    /// </para>
    /// <para>
    /// Place it <b>after</b> content is loaded and after the currency wallet is bound.
    /// Economy definitions come from the content service, balances live in the currency
    /// system, and purchases grant through the reward service — so nothing can work before
    /// those exist.
    /// </para>
    /// </remarks>
    public sealed class EconomyStep : LoadingStepBase
    {
        [Tooltip("Policy asset. Leave empty to load Resources/UniEconomyConfig.")]
        [SerializeField] private UniEconomyConfig _config;

        [Tooltip("Also install the service into the UniEconomy static facade. Turn off " +
                 "if the game resolves IEconomyService explicitly everywhere.")]
        [SerializeField] private bool _installFacade = true;

        /// <summary>Policy asset used to build the service.</summary>
        public UniEconomyConfig Config => _config;

        /// <summary>Whether the static <see cref="UniEconomy"/> facade is installed.</summary>
        public bool InstallFacade => _installFacade;

        /// <inheritdoc />
        public override async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            var binder = IoCStatics.Binder;
            var resolver = IoCStatics.Resolver;

            var backend = ResolveBackend(resolver);

            var currencies = resolver.Resolve<ICurrencyService>();

            var rewards = resolver.TryResolve<IRewardService>(out var bound)
                ? bound
                : null;

            // Constructed rather than bound by type, because the config is a serialized field
            // on this step and the container cannot supply constructor arguments.
            var service = new EconomyService(resolver.Resolve<IClock>(),
                resolver.Resolve<UniTx.Content.IContentService>(), backend, currencies,
                rewards);

            binder.BindInstance(service).AsSingleton().Conclude();

            if (_installFacade) await UniEconomy.InitializeAsync(service, cToken);
            else await service.InitializeAsync(cToken);
        }

        private static IEconomyBackend ResolveBackend(IResolver resolver)
        {
            if (resolver.TryResolve<IEconomyBackend>(out var bound)) return bound;

            var local = new LocalEconomyBackend();
            local.Inject(resolver);

            return local;
        }
    }
}
