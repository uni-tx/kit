using System;
using System.Collections.Generic;
using UniTx.Analytics;
using UniTx.Events;

namespace UniTx.Economy.Integrations
{
    /// <summary>
    /// Reports the economy funnel to every registered analytics provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Subscribes to the kit's event bus rather than sitting on the call path, so
    /// instrumentation is opt-in, cannot slow an exchange down, and cannot be forgotten at
    /// a new call site — every route into the system ends in the same events.
    /// </para>
    /// <para>
    /// The funnel these events answer: what gets converted (currency exchanged), and what
    /// gets bought (purchase completed) — the two numbers a LiveOps dashboard wants most.
    /// </para>
    /// </remarks>
    public sealed class EconomyAnalytics : IDisposable
    {
        /// <summary>
        /// Event name reported when a currency exchange completed.
        /// </summary>
        public const string ExchangedEvent = "economy_currency_exchanged";

        /// <summary>
        /// Event name reported when a virtual purchase completed.
        /// </summary>
        public const string PurchaseCompletedEvent = "economy_purchase_completed";

        /// <summary>
        /// Event name reported when a purchase's rewards could not be delivered.
        /// </summary>
        public const string PurchaseFailedEvent = "economy_purchase_failed";

        private bool _isDisposed;

        /// <summary>
        /// Starts reporting economy events.
        /// </summary>
        public EconomyAnalytics()
        {
            UniEvents.Subscribe<CurrencyExchanged>(OnExchanged);
            UniEvents.Subscribe<PurchaseCompleted>(OnPurchaseCompleted);
            UniEvents.Subscribe<PurchaseDeliveryFailed>(OnPurchaseFailed);
        }

        /// <summary>
        /// Stops reporting.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;

            UniEvents.Unsubscribe<CurrencyExchanged>(OnExchanged);
            UniEvents.Unsubscribe<PurchaseCompleted>(OnPurchaseCompleted);
            UniEvents.Unsubscribe<PurchaseDeliveryFailed>(OnPurchaseFailed);
        }

        private void OnExchanged(CurrencyExchanged @event)
        {
            var parameters = new Dictionary<string, object>
            {
                ["economy_id"] = @event.EconomyId,
                ["from_currency_id"] = @event.FromCurrencyId,
                ["to_currency_id"] = @event.ToCurrencyId,
                ["amount"] = @event.Amount,
                ["received"] = @event.Received
            };

            UniAnalytics.Track(ExchangedEvent, parameters);
        }

        private void OnPurchaseCompleted(PurchaseCompleted @event)
        {
            var parameters = new Dictionary<string, object>
            {
                ["economy_id"] = @event.EconomyId,
                ["purchase_id"] = @event.PurchaseId
            };

            UniAnalytics.Track(PurchaseCompletedEvent, parameters);
        }

        private void OnPurchaseFailed(PurchaseDeliveryFailed @event)
        {
            var parameters = new Dictionary<string, object>
            {
                ["economy_id"] = @event.EconomyId,
                ["purchase_id"] = @event.PurchaseId
            };

            UniAnalytics.Track(PurchaseFailedEvent, parameters);
        }
    }
}
