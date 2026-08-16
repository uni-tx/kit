using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.IoC;
using UnityEngine;

namespace UniTx.Core
{
    /// <summary>
    /// Executes a sequence of <see cref="LoadingStepBase"/> steps in order at startup.
    /// </summary>
    public sealed class AppLoader : MonoBehaviour
    {
        [Tooltip("Loading steps, executed top to bottom. Each step completes before the next starts.")]
        [SerializeField] private LoadingStepBase[] _loadingSteps = Array.Empty<LoadingStepBase>();

        [Tooltip("Report a normalized 0..1 value as each step completes. Wire a loading bar here.")]
        [SerializeField] private bool _reportProgress = true;

        private readonly CancellationTokenSource _cts = new();

        /// <summary>
        /// Raised after each step completes, with progress normalized to 0..1.
        /// </summary>
        public event Action<float> OnProgress;

        /// <summary>
        /// Raised once every step has completed successfully.
        /// </summary>
        public event Action OnCompleted;

        /// <summary>
        /// Raised when a step throws, with the exception that stopped the sequence.
        /// </summary>
        public event Action<Exception> OnFailed;

        /// <summary>
        /// Gets the steps this loader will execute, in order.
        /// </summary>
        public IReadOnlyList<LoadingStepBase> LoadingSteps => _loadingSteps;

        /// <summary>
        /// Indicates whether every step has completed successfully.
        /// </summary>
        public bool IsCompleted { get; private set; }

        private void Start() => RunAsync(_cts.Token).Forget();

        private void OnDestroy() => _cts.SafeCancelAndDispose();

        private async UniTaskVoid RunAsync(CancellationToken cToken)
        {
            if (_loadingSteps == null || _loadingSteps.Length == 0)
            {
                UniStatics.LogWarning("No loading steps assigned; nothing to do.", this);
                IsCompleted = true;
                OnCompleted.SafeInvoke();
                return;
            }

            var total = _loadingSteps.Length;

            try
            {
                for (var i = 0; i < total; i++)
                {
                    var step = _loadingSteps[i];

                    if (step == null)
                    {
                        throw new InvalidOperationException(
                            $"Loading step {i + 1}/{total} is unassigned. Fix the AppLoader's step list.");
                    }

                    UniStatics.LogInfo($"Step {i + 1}/{total}: {step.GetType().Name}", this);

                    if (step is IInjectable injectable)
                    {
                        injectable.Inject(IoCStatics.Resolver);
                    }

                    await step.InitializeAsync(cToken);

                    if (_reportProgress) OnProgress.SafeInvoke((i + 1) / (float)total);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the loader is destroyed mid-startup; not a failure.
                return;
            }
            catch (Exception ex)
            {
                // A half-finished bootstrap leaves services bound but uninitialized, which
                // surfaces later as confusing null references. Fail loudly at the source.
                UniStatics.LogError($"Loading failed: {ex.Message}", this);
                UniStatics.LogException(ex, this);
                OnFailed.SafeInvoke(ex);
                return;
            }

            IsCompleted = true;
            UniStatics.LogInfo($"All {total} loading step(s) completed.", this);
            OnCompleted.SafeInvoke();
        }
    }
}
