using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;
using UniTx.SeasonPass;
using UnityEngine;

namespace UniTx.Bootstrap
{
    /// <summary>
    /// Binds and starts the season pass.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A step of its own rather than part of <see cref="BindDependenciesStep"/>, which every
    /// project runs: a game without a season pass should not pay for one.
    /// </para>
    /// <para>
    /// Place it <b>after</b> content is loaded and after the game's own economy is bound.
    /// Season definitions come from the content service, so nothing can be selected before
    /// they exist, and the first refresh can claim rewards — which needs the game's
    /// <see cref="ISeasonPassRewardGranter"/> to already be resolvable.
    /// </para>
    /// </remarks>
    public sealed class SeasonPassStep : LoadingStepBase
    {
        [Tooltip("Policy asset. Leave empty to load Resources/UniSeasonPassConfig.")]
        [SerializeField] private UniSeasonPassConfig _config;

        [Tooltip("Also install the service into the UniSeasonPass static facade. Turn off if " +
                 "the game resolves ISeasonPassService explicitly everywhere.")]
        [SerializeField] private bool _installFacade = true;

        /// <inheritdoc />
        public override async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            var binder = IoCStatics.Binder;
            var resolver = IoCStatics.Resolver;

            var backend = ResolveBackend(resolver);

            // Constructed rather than bound by type, because the config is a serialized field
            // on this step and the container cannot supply constructor arguments.
            var service = new SeasonPassService(resolver.Resolve<IClock>(),
                resolver.Resolve<UniTx.Content.IContentService>(), backend, _config);

            if (resolver.TryResolve<ISeasonPassRewardGranter>(out var granter))
            {
                service.SetRewardGranter(granter);
            }

            if (resolver.TryResolve<ISeasonPassWallet>(out var wallet)) service.SetWallet(wallet);

            binder.BindInstance(service).AsSingleton().Conclude();

            if (_installFacade) await UniSeasonPass.InitializeAsync(service, cToken);
            else await service.InitializeAsync(cToken);
        }

        private static ISeasonPassBackend ResolveBackend(IResolver resolver)
        {
            if (resolver.TryResolve<ISeasonPassBackend>(out var bound)) return bound;

            var local = new LocalSeasonPassBackend();
            local.Inject(resolver);

            return local;
        }
    }
}
