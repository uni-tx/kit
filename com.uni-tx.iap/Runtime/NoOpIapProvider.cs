using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniTx.Iap
{
    /// <summary>
    /// A provider that sells nothing, used when no store SDK is installed.
    /// </summary>
    /// <remarks>
    /// Keeps store code on one path everywhere. A desktop build, an automated test and a
    /// player build with billing unavailable all take this branch and resolve to
    /// <see cref="IapResult.Unsupported"/>, so the shop screen degrades to a disabled button
    /// instead of a null reference.
    /// </remarks>
    public sealed class NoOpIapProvider : IIapProvider
    {
        /// <inheritdoc />
        public string Name => "NoOp";

        /// <inheritdoc />
        public bool IsInitialized { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// Never raised. Declared so the facade can subscribe unconditionally.
        /// </remarks>
        public event Action<IapPurchase> OnPurchaseRestored
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public UniTask InitializeAsync(UniIapConfig config, CancellationToken cToken = default)
        {
            IsInitialized = true;
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public UniTask<IapPurchase> PurchaseAsync(string productId, CancellationToken cToken = default) =>
            UniTask.FromResult(IapPurchase.Fail(IapResult.Unsupported, productId, "no store provider installed"));

        /// <inheritdoc />
        public UniTask<bool> RestoreAsync(CancellationToken cToken = default) => UniTask.FromResult(false);

        /// <inheritdoc />
        public bool IsOwned(string productId) => false;

        /// <inheritdoc />
        public string GetLocalizedPrice(string productId) => null;

        /// <inheritdoc />
        public string GetLocalizedTitle(string productId) => null;
    }
}
