using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniTx.Core
{
    /// <summary>
    /// Async scene loading with progress, cancellation and deferred activation.
    /// </summary>
    public static class UniSceneLoader
    {
        // Unity stalls LoadSceneAsync at 0.9 while allowSceneActivation is false, so
        // progress has to be rescaled or a loading bar sticks short of full.
        private const float ActivationThreshold = 0.9f;

        /// <summary>
        /// Asynchronously loads a scene by name.
        /// </summary>
        /// <param name="sceneName">Name or path of a scene included in the build.</param>
        /// <param name="additive">Load alongside the current scenes instead of replacing them.</param>
        /// <param name="progress">Optional progress reporter, normalized to 0..1.</param>
        /// <param name="cToken">Token to cancel the load.</param>
        /// <returns>The loaded scene.</returns>
        public static async UniTask<Scene> LoadSceneAsync(string sceneName, bool additive = false,
            IProgress<float> progress = null, CancellationToken cToken = default)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name cannot be null or empty.", nameof(sceneName));
            }

            var mode = additive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            var operation = SceneManager.LoadSceneAsync(sceneName, mode)
                ?? throw new InvalidOperationException(
                    $"Scene '{sceneName}' is not in the build profile's scene list.");

            await operation.ToUniTask(progress, cancellationToken: cToken);

            return additive
                ? SceneManager.GetSceneByName(sceneName)
                : SceneManager.GetActiveScene();
        }

        /// <summary>
        /// Asynchronously loads a scene but holds activation until the returned handle is completed.
        /// </summary>
        /// <param name="sceneName">Name or path of a scene included in the build.</param>
        /// <param name="additive">Load alongside the current scenes instead of replacing them.</param>
        /// <param name="progress">Optional progress reporter, normalized to 0..1.</param>
        /// <param name="cToken">Token to cancel the load.</param>
        /// <returns>A handle that reports readiness and activates the scene on demand.</returns>
        /// <remarks>
        /// Use this to hold a loading screen at 100% until an animation finishes, instead of
        /// cutting to the new scene the instant streaming completes.
        /// </remarks>
        public static async UniTask<PendingScene> LoadSceneDeferredAsync(string sceneName, bool additive = false,
            IProgress<float> progress = null, CancellationToken cToken = default)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name cannot be null or empty.", nameof(sceneName));
            }

            var mode = additive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            var operation = SceneManager.LoadSceneAsync(sceneName, mode)
                ?? throw new InvalidOperationException(
                    $"Scene '{sceneName}' is not in the build profile's scene list.");

            operation.allowSceneActivation = false;

            while (operation.progress < ActivationThreshold)
            {
                cToken.ThrowIfCancellationRequested();
                progress?.Report(operation.progress / ActivationThreshold);
                await UniTask.Yield(PlayerLoopTiming.Update, cToken);
            }

            progress?.Report(1f);
            return new PendingScene(operation, sceneName, additive);
        }

        /// <summary>
        /// Asynchronously unloads a loaded scene.
        /// </summary>
        /// <param name="scene">The scene to unload.</param>
        /// <param name="cToken">Token to cancel the unload.</param>
        public static async UniTask UnloadSceneAsync(Scene scene, CancellationToken cToken = default)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            var operation = SceneManager.UnloadSceneAsync(scene);

            if (operation == null) return;

            await operation.ToUniTask(cancellationToken: cToken);
        }

        /// <summary>
        /// Asynchronously unloads a loaded scene by name.
        /// </summary>
        /// <param name="sceneName">Name of the scene to unload.</param>
        /// <param name="cToken">Token to cancel the unload.</param>
        public static UniTask UnloadSceneAsync(string sceneName, CancellationToken cToken = default)
            => UnloadSceneAsync(SceneManager.GetSceneByName(sceneName), cToken);

        /// <summary>
        /// A scene that finished streaming but has not been activated yet.
        /// </summary>
        public readonly struct PendingScene
        {
            private readonly AsyncOperation _operation;
            private readonly string _sceneName;
            private readonly bool _additive;

            internal PendingScene(AsyncOperation operation, string sceneName, bool additive)
            {
                _operation = operation;
                _sceneName = sceneName;
                _additive = additive;
            }

            /// <summary>
            /// Activates the scene and waits until it is fully loaded.
            /// </summary>
            /// <param name="cToken">Token to cancel the wait.</param>
            /// <returns>The activated scene.</returns>
            public async UniTask<Scene> ActivateAsync(CancellationToken cToken = default)
            {
                if (_operation == null)
                {
                    throw new InvalidOperationException("This PendingScene was never initialized.");
                }

                _operation.allowSceneActivation = true;
                await _operation.ToUniTask(cancellationToken: cToken);

                return _additive
                    ? SceneManager.GetSceneByName(_sceneName)
                    : SceneManager.GetActiveScene();
            }
        }
    }
}
