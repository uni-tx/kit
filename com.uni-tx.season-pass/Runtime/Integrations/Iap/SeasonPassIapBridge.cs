using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.Iap;

namespace UniTx.SeasonPass.Integrations
{
    /// <summary>
    /// Turns store entitlements into season pass unlocks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Listens to <see cref="UniIap.OnPurchased"/> rather than the result of a purchase call,
    /// because that event is the only place a restore, a subscription renewal, an Ask-to-Buy
    /// approval or an order interrupted by an app kill ever shows up. Wiring the unlock to the
    /// purchase call instead ships a game that loses passes players paid for.
    /// </para>
    /// <para>
    /// Every unlock it performs is idempotent, so a store that re-delivers the same
    /// entitlement on every launch — which iOS does — costs nothing after the first time.
    /// </para>
    /// </remarks>
    public sealed class SeasonPassIapBridge : IDisposable
    {
        private readonly ISeasonPassService _service;
        private readonly CancellationTokenSource _cts = new();

        private bool _isDisposed;

        /// <summary>
        /// Starts listening for entitlements and applying them to the season pass.
        /// </summary>
        /// <param name="service">The season pass to unlock against.</param>
        /// <exception cref="ArgumentNullException">Thrown when the service is null.</exception>
        public SeasonPassIapBridge(ISeasonPassService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));

            UniIap.OnPurchased += HandlePurchased;
        }

        /// <summary>
        /// Stops listening.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;

            UniIap.OnPurchased -= HandlePurchased;

            _cts.Cancel();
            _cts.Dispose();
        }

        private void HandlePurchased(IapPurchase purchase)
        {
            if (!purchase.IsSuccess || string.IsNullOrEmpty(purchase.ProductId)) return;

            ApplyAsync(purchase.ProductId, _cts.Token).Forget();
        }

        private async UniTaskVoid ApplyAsync(string productId, CancellationToken cToken)
        {
            try
            {
                var season = _service.Season;

                if (season == null) return;

                var offer = season.GetOfferByProductId(productId);

                if (offer != null)
                {
                    // Already paid at the store, so no wallet charge — and the service ignores
                    // a track the player already owns, which is what makes a restore free.
                    await _service.UnlockTrackAsync(offer.Track, SeasonPassPayment.External, cToken);

                    return;
                }

                if (string.Equals(productId, season.TierSkipProductId, StringComparison.Ordinal))
                {
                    await _service.BuyTierSkipsAsync(1, SeasonPassPayment.External, cToken);
                }
            }
            catch (OperationCanceledException)
            {
                // The bridge was disposed mid-flight; nothing to report.
            }
            catch (Exception exception)
            {
                UniStatics.LogException(exception, this);
            }
        }
    }
}
