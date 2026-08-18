using System;
using System.Collections.Generic;
using UniTx.Analytics;
using UniTx.Events;

namespace UniTx.Store.Integrations
{
    /// <summary>
    /// Reports the shop funnel to every registered analytics provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Subscribes to the kit's event bus rather than sitting on the call path, so
    /// instrumentation is opt-in, cannot slow a claim down, and cannot be forgotten at a
    /// new call site — every route into the system ends in the same events.
    /// </para>
    /// <para>
    /// The funnel these events answer: what gets claimed (offer claimed), and what failed
    /// to deliver (delivery failed) — the two numbers a LiveOps dashboard wants most.
    /// </para>
    /// </remarks>
    public sealed class StoreAnalytics : IDisposable
    {
        /// <summary>
        /// Event name reported when an offer's rewards are delivered.
        /// </summary>
        public const string OfferClaimedEvent = "store_offer_claimed";

        /// <summary>
        /// Event name reported when a delivery failed and is queued for retry.
        /// </summary>
        public const string DeliveryFailedEvent = "store_delivery_failed";

        private readonly Dictionary<string, object> _parameters = new();

        private bool _isDisposed;

        /// <summary>
        /// Starts reporting store events.
        /// </summary>
        public StoreAnalytics()
        {
            UniEvents.Subscribe<StoreOfferClaimed>(OnClaimed);
            UniEvents.Subscribe<StoreDeliveryFailed>(OnDeliveryFailed);
        }

        /// <summary>
        /// Stops reporting.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;

            UniEvents.Unsubscribe<StoreOfferClaimed>(OnClaimed);
            UniEvents.Unsubscribe<StoreDeliveryFailed>(OnDeliveryFailed);
        }

        private void OnClaimed(StoreOfferClaimed @event)
        {
            _parameters.Clear();
            _parameters["store_id"] = @event.StoreId;
            _parameters["offer_id"] = @event.OfferId;
            _parameters["kind"] = @event.Kind.ToString();

            UniAnalytics.Track(OfferClaimedEvent, _parameters);
        }

        private void OnDeliveryFailed(StoreDeliveryFailed @event)
        {
            _parameters.Clear();
            _parameters["store_id"] = @event.StoreId;
            _parameters["offer_id"] = @event.OfferId;
            _parameters["reward_id"] = @event.RewardId;

            UniAnalytics.Track(DeliveryFailedEvent, _parameters);
        }
    }
}
