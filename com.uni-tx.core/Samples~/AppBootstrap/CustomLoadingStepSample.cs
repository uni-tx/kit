using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.IoC;
using UnityEngine;

namespace UniTx.Core.Samples
{
    /// <summary>
    /// A game-specific loading step, added to an <see cref="AppLoader"/> after the kit's own.
    /// </summary>
    /// <remarks>
    /// <b>Setup:</b> create an empty GameObject, add <see cref="AppLoader"/>, then add this
    /// component (and any kit steps) and drag them into the loader's step list in order.
    /// Each step completes before the next begins.
    /// </remarks>
    public sealed class WarmUpStep : LoadingStepBase
    {
        [SerializeField, Min(0f)] private float _simulatedWorkSeconds = 0.5f;

        /// <inheritdoc />
        public override async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            UniStatics.LogInfo("Warming up caches...", this);

            // Always forward the token. The loader cancels it when destroyed, so a step left
            // awaiting through a scene change unwinds instead of touching dead objects.
            await UniTask.Delay(TimeSpan.FromSeconds(_simulatedWorkSeconds), cancellationToken: cToken);

            // Anything bound by an earlier step is resolvable here.
            if (IoCStatics.Resolver.TryResolve<IClock>(out var clock))
            {
                UniStatics.LogInfo($"Clock ready, UTC is {clock.UtcNow:O}", this);
            }
        }
    }

    /// <summary>
    /// Drives a loading bar from <see cref="AppLoader"/>'s events.
    /// </summary>
    public sealed class LoadingScreenSample : MonoBehaviour
    {
        [SerializeField] private AppLoader _appLoader;
        [SerializeField] private CanvasGroup _loadingScreen;

        private void OnEnable()
        {
            if (_appLoader == null) return;

            _appLoader.OnProgress += HandleProgress;
            _appLoader.OnCompleted += HandleCompleted;

            // A failed bootstrap used to be swallowed and surface later as confusing null
            // references; now it is an event you can show a retry prompt from.
            _appLoader.OnFailed += HandleFailed;
        }

        private void OnDisable()
        {
            if (_appLoader == null) return;

            _appLoader.OnProgress -= HandleProgress;
            _appLoader.OnCompleted -= HandleCompleted;
            _appLoader.OnFailed -= HandleFailed;
        }

        private static void HandleProgress(float normalized) => Debug.Log($"Loading {normalized:P0}");

        private void HandleCompleted()
        {
            Debug.Log("Bootstrap complete.");

            if (_loadingScreen != null) _loadingScreen.alpha = 0f;
        }

        private static void HandleFailed(Exception exception)
            => Debug.LogError($"Bootstrap failed, showing retry: {exception.Message}");
    }
}
