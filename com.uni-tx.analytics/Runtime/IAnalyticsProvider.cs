using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniTx.Analytics
{
    /// <summary>
    /// A single analytics backend. Implement one adapter per SDK.
    /// </summary>
    /// <remarks>
    /// The kit ships no SDK dependency of its own. Every analytics vendor has its own
    /// pricing, consent obligations and build-size cost, so the choice stays with the game;
    /// the facade only defines the shape an adapter must satisfy. A project with no adapter
    /// registered still runs — events go nowhere instead of crashing.
    /// </remarks>
    public interface IAnalyticsProvider
    {
        /// <summary>
        /// Gets a short name used in logs, e.g. "GameAnalytics".
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Indicates whether the provider is ready to receive events.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Starts the underlying SDK.
        /// </summary>
        /// <param name="cToken">Token to cancel initialization.</param>
        UniTask InitializeAsync(CancellationToken cToken = default);

        /// <summary>
        /// Records an event.
        /// </summary>
        /// <param name="eventName">The event name.</param>
        /// <param name="parameters">Event parameters, or null.</param>
        void TrackEvent(string eventName, IReadOnlyDictionary<string, object> parameters);

        /// <summary>
        /// Sets a property that persists across subsequent events.
        /// </summary>
        /// <param name="key">Property name.</param>
        /// <param name="value">Property value.</param>
        void SetUserProperty(string key, object value);

        /// <summary>
        /// Records real-money revenue.
        /// </summary>
        /// <param name="productId">Store product identifier.</param>
        /// <param name="currency">ISO 4217 currency code, e.g. "USD".</param>
        /// <param name="amount">Amount in major units.</param>
        void TrackRevenue(string productId, string currency, decimal amount);

        /// <summary>
        /// Records or withdraws the player's tracking consent.
        /// </summary>
        /// <param name="hasConsent">Whether the player consented to analytics.</param>
        /// <remarks>
        /// Required by GDPR, the Google Play Families policy and Apple's ATT. An adapter
        /// must not send anything before consent is granted.
        /// </remarks>
        void SetConsent(bool hasConsent);

        /// <summary>
        /// Flushes anything buffered by the SDK.
        /// </summary>
        void Flush();
    }
}
