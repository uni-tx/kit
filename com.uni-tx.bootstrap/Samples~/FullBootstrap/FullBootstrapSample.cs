using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Analytics;
using UniTx.Content;
using UniTx.Core;
using UniTx.Entity;
using UniTx.IoC;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.Bootstrap.Samples
{
    /// <summary>
    /// A game-specific step that runs after the kit is wired up.
    /// </summary>
    /// <remarks>
    /// <b>Scene setup</b> — one GameObject with <see cref="AppLoader"/>, and these components
    /// dragged into its step list in this order:
    /// <list type="number">
    ///   <item><description><c>UniTxStep</c> — config, root object, events, resources, widgets, audio.</description></item>
    ///   <item><description><c>BindDependenciesStep</c> — binds the kit's services into the container.</description></item>
    ///   <item><description><c>InitDependenciesStep</c> — injects, then initializes everything bound.</description></item>
    ///   <item><description><c>GameContentStep</c> (this) — your own content, entities and services.</description></item>
    /// </list>
    /// The order matters: nothing can be injected before it is bound, and nothing should be
    /// initialized before its dependencies are injected.
    /// </remarks>
    public sealed class GameContentStep : LoadingStepBase
    {
        [Tooltip("Addressables labels holding this build's JSON content files.")]
        [SerializeField] private string[] _contentLabels = { "content" };

        /// <inheritdoc />
        public override async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            var resolver = IoCStatics.Resolver;

            // Register every content type before loading; unregistered files are skipped
            // with a warning rather than silently ignored.
            ContentRegistry.Register<GameSettingsData>("settings");

            var content = resolver.Resolve<IContentLoader>();
            await content.LoadContentAsync(_contentLabels, cToken);

            // Entities are built from content, so this has to come after the load.
            await resolver.Resolve<IEntityLoader>().LoadEntitiesAsync(cToken);

            // Analytics last: instrumentation should not delay anything the player sees.
            UniAnalytics.Register(new DebugAnalyticsProvider());
            await UniAnalytics.InitializeAsync(cToken);

            UniStatics.LogInfo("Game content ready.", this);
        }
    }

    /// <summary>
    /// Example content type loaded by <see cref="GameContentStep"/>.
    /// </summary>
    [System.Serializable]
    public sealed class GameSettingsData : IData
    {
        [SerializeField] private string _id;
        [SerializeField] private float _musicVolume = 0.7f;

        /// <inheritdoc />
        public string Id => _id;

        /// <summary>
        /// Gets the default music volume for a fresh install.
        /// </summary>
        public float MusicVolume => _musicVolume;
    }

    /// <summary>
    /// Reads the fully bootstrapped kit once loading finishes.
    /// </summary>
    public sealed class FullBootstrapSample : MonoBehaviour
    {
        [SerializeField] private AppLoader _appLoader;

        private void OnEnable()
        {
            if (_appLoader != null) _appLoader.OnCompleted += HandleReady;
        }

        private void OnDisable()
        {
            if (_appLoader != null) _appLoader.OnCompleted -= HandleReady;
        }

        private static void HandleReady()
        {
            var resolver = IoCStatics.Resolver;

            // Everything bound by BindDependenciesStep is resolvable from here on.
            var clock = resolver.Resolve<IClock>();
            var saves = resolver.Resolve<ISerialisationService>();
            var content = resolver.Resolve<IContentService>();

            Debug.Log($"Ready at {clock.UtcNow:O}");
            Debug.Log($"Saves at {((SerialisationService)saves).SaveDirectoryPath}");

            if (content.TryGetData<GameSettingsData>("default", out var settings))
            {
                Debug.Log($"Default music volume: {settings.MusicVolume}");
            }
        }
    }
}
