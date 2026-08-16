using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;

namespace UniTx.Analytics
{
    /// <summary>
    /// Static facade that fans analytics events out to every registered provider.
    /// </summary>
    /// <remarks>
    /// Games routinely run two or three analytics SDKs at once. Calling each one at every
    /// event site is how instrumentation drifts apart; this keeps one call site per event.
    /// </remarks>
    public static class UniAnalytics
    {
        private static readonly List<IAnalyticsProvider> Providers = new();
        private static readonly Dictionary<string, object> ScratchParameters = new();

        private static bool _hasConsent = true;

        /// <summary>
        /// Gets the registered providers.
        /// </summary>
        public static IReadOnlyList<IAnalyticsProvider> RegisteredProviders => Providers;

        /// <summary>
        /// Indicates whether events are currently allowed to be sent.
        /// </summary>
        public static bool HasConsent => _hasConsent;

        /// <summary>
        /// Registers a provider.
        /// </summary>
        /// <param name="provider">The adapter to add.</param>
        public static void Register(IAnalyticsProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            if (Providers.Contains(provider)) return;

            Providers.Add(provider);
            provider.SetConsent(_hasConsent);
        }

        /// <summary>
        /// Removes a provider.
        /// </summary>
        /// <param name="provider">The adapter to remove.</param>
        public static bool Unregister(IAnalyticsProvider provider) => Providers.Remove(provider);

        /// <summary>
        /// Initializes every registered provider.
        /// </summary>
        /// <param name="cToken">Token to cancel initialization.</param>
        /// <remarks>
        /// One provider failing to start must not stop the others, so failures are logged
        /// and skipped rather than propagated.
        /// </remarks>
        public static async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            foreach (var provider in Providers)
            {
                try
                {
                    await provider.InitializeAsync(cToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    UniStatics.LogError($"Analytics provider '{provider.Name}' failed to initialize: {ex.Message}",
                        null);
                }
            }
        }

        /// <summary>
        /// Records the player's tracking consent and forwards it to every provider.
        /// </summary>
        /// <param name="hasConsent">Whether the player consented to analytics.</param>
        public static void SetConsent(bool hasConsent)
        {
            _hasConsent = hasConsent;

            foreach (var provider in Providers)
            {
                provider.SetConsent(hasConsent);
            }
        }

        /// <summary>
        /// Records an event with no parameters.
        /// </summary>
        /// <param name="eventName">The event name.</param>
        public static void Track(string eventName) => Track(eventName, null);

        /// <summary>
        /// Records an event with parameters.
        /// </summary>
        /// <param name="eventName">The event name.</param>
        /// <param name="parameters">Event parameters, or null.</param>
        public static void Track(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                UniStatics.LogWarning("Analytics event name is empty; ignoring.", null);
                return;
            }

            // Gate centrally so a provider that forgets to honour consent cannot leak.
            if (!_hasConsent) return;

            foreach (var provider in Providers)
            {
                if (!provider.IsReady) continue;

                try
                {
                    provider.TrackEvent(eventName, parameters);
                }
                catch (Exception ex)
                {
                    // Analytics must never take gameplay down.
                    UniStatics.LogError($"Analytics provider '{provider.Name}' threw on '{eventName}': {ex.Message}",
                        null);
                }
            }
        }

        /// <summary>
        /// Records an event with a single parameter, without allocating a dictionary.
        /// </summary>
        /// <param name="eventName">The event name.</param>
        /// <param name="key">Parameter name.</param>
        /// <param name="value">Parameter value.</param>
        /// <remarks>
        /// Reuses one scratch dictionary. Events fired every few frames — a combo counter,
        /// say — would otherwise allocate a dictionary per call.
        /// </remarks>
        public static void Track(string eventName, string key, object value)
        {
            ScratchParameters.Clear();
            ScratchParameters[key] = value;
            Track(eventName, ScratchParameters);
        }

        /// <summary>
        /// Sets a property that persists across subsequent events.
        /// </summary>
        /// <param name="key">Property name.</param>
        /// <param name="value">Property value.</param>
        public static void SetUserProperty(string key, object value)
        {
            if (!_hasConsent) return;

            foreach (var provider in Providers)
            {
                if (provider.IsReady) provider.SetUserProperty(key, value);
            }
        }

        /// <summary>
        /// Records real-money revenue.
        /// </summary>
        /// <param name="productId">Store product identifier.</param>
        /// <param name="currency">ISO 4217 currency code, e.g. "USD".</param>
        /// <param name="amount">Amount in major units.</param>
        public static void TrackRevenue(string productId, string currency, decimal amount)
        {
            if (!_hasConsent) return;

            foreach (var provider in Providers)
            {
                if (provider.IsReady) provider.TrackRevenue(productId, currency, amount);
            }
        }

        /// <summary>
        /// Flushes every provider's buffer.
        /// </summary>
        /// <remarks>Call from <c>IUnityEventListener.OnPause</c> so a backgrounded app does not drop events.</remarks>
        public static void Flush()
        {
            foreach (var provider in Providers)
            {
                if (provider.IsReady) provider.Flush();
            }
        }

        /// <summary>
        /// Removes every provider.
        /// </summary>
        public static void Reset()
        {
            Providers.Clear();
            ScratchParameters.Clear();
            _hasConsent = true;
        }
    }
}
