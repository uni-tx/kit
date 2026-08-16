using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Audio;
using UniTx.Core;
using UniTx.Events;
using UniTx.IoC;
using UniTx.Resources;
using UniTx.Serialization;
using UniTx.Widgets;
using UnityEngine;

namespace UniTx.Bootstrap
{
    /// <summary>
    /// Root loading step: config, root object, and every UniTx static facade.
    /// </summary>
    public class UniTxStep : LoadingStepBase
    {
        [Tooltip("Config asset. Leave empty to load Resources/UniTxConfig instead.")]
        [SerializeField] private UniTxConfig _config;

        [Tooltip("Pause and resume audio with the application, so sound stops when the " +
                 "game is backgrounded on mobile.")]
        [SerializeField] private bool _pauseAudioWithApplication = true;

        [Tooltip("Flush saves and release caches when the OS reports memory pressure. " +
                 "On mobile this is the only warning before the process is killed.")]
        [SerializeField] private bool _respondToLowMemory = true;

        private IUnityEventListener _listener;

        /// <inheritdoc />
        public sealed override async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            SetupRoot();
            LoadConfig();
            await InitializeFrameworkAsync(cToken);
            WireApplicationLifecycle();
        }

        /// <summary>
        /// Initializes the events, resources, widgets and audio facades, in dependency order.
        /// </summary>
        /// <param name="cToken">Token to cancel initialization.</param>
        protected virtual async UniTask InitializeFrameworkAsync(CancellationToken cToken = default)
        {
            UniEvents.Initialize();

            // Resources first: widgets loads its prefab map through it.
            await UniResources.InitializeAsync(cToken);
            await UniWidgets.InitializeAsync(cToken);
            await UniAudio.InitializeAsync(cToken);
        }

        private void WireApplicationLifecycle()
        {
            // Optional: BindDependenciesStep may not have run, or a project may not use the
            // listener at all.
            if (!IoCStatics.IsInitialized ||
                !IoCStatics.Resolver.TryResolve<IUnityEventListener>(out var listener))
            {
                // Safe-area values are still correct when read; only change notifications
                // need the listener.
                UniSafeArea.Initialize(null);
                return;
            }

            _listener = listener;

            // Unity raises no event when the safe area changes, so it has to be polled off
            // the shared update loop. Everything that positions to screen edges — UI
            // containers and ad banners alike — reads from here.
            UniSafeArea.Initialize(listener);

            // The OS gives one warning before it kills the process on Android and iOS.
            // Responding to it is the difference between dropping some cached audio and
            // having the player report that the game "keeps crashing".
            if (_respondToLowMemory) listener.OnLowMemory += HandleLowMemory;

            if (!_pauseAudioWithApplication) return;

            // A method group, not a lambda: an anonymous delegate cannot be unsubscribed,
            // so re-running this step would stack a second handler on the same listener.
            listener.OnPause += HandleApplicationPause;
        }

        private static void HandleLowMemory()
        {
            UniStatics.LogWarning("Low memory reported by the OS — releasing what we can.", null);

            // Flush first: whatever is still queued is lost if the process is killed, and a
            // save is far more valuable to the player than any cache.
            if (IoCStatics.IsInitialized &&
                IoCStatics.Resolver.TryResolve<ISerialisationService>(out var saves))
            {
                saves.Flush();
            }

            // Stop every one-shot effect; each holds a pooled AudioSource and a decoded clip.
            if (UniAudio.IsInitialized) UniAudio.StopAllSfx();

            if (UniResources.IsInitialized) UniResources.UnloadUnusedAssetsAsync().Forget();
        }

        private void OnDestroy()
        {
            if (_listener == null) return;

            _listener.OnPause -= HandleApplicationPause;
            _listener.OnLowMemory -= HandleLowMemory;
            UniSafeArea.Reset(_listener);
            _listener = null;
        }

        private static void HandleApplicationPause(bool isPaused)
        {
            if (isPaused) UniAudio.PauseAll();
            else UniAudio.ResumeAll();
        }

        private void LoadConfig()
        {
            if (_config != null)
            {
                UniStatics.Config = _config;
                return;
            }

            // Fully qualified: `using UniTx.Resources` shadows UnityEngine.Resources here.
            UniStatics.Config = UnityEngine.Resources.Load<UniTxConfig>(UniTxConfig.DefaultResourcePath);

            if (UniStatics.Config == null)
            {
                UniStatics.LogWarning(
                    "No UniTxConfig assigned and none found at Resources/UniTxConfig. " +
                    "Defaults will be used for save interval, widget keys and the time server.", this);
            }
        }

        private void SetupRoot()
        {
            if (UniStatics.Root != null) return;

            UniStatics.Root = new GameObject("[UniTx] Root");
            DontDestroyOnLoad(UniStatics.Root);
        }
    }
}
