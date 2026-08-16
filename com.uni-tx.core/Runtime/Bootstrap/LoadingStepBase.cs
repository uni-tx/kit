using System.Threading;
using UniTx.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniTx.Core
{
    /// <summary>
    /// Base class for loading steps.
    /// </summary>
    public abstract class LoadingStepBase : MonoBehaviour, IInitializableAsync
    {
        /// <inheritdoc/>
        public abstract UniTask InitializeAsync(CancellationToken cToken = default);
    }
}