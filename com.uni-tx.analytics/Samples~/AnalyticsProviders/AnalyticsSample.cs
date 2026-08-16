using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Analytics;
using UniTx.Core;
using UniTx.IoC;
using UnityEngine;

namespace UniTx.Analytics.Samples
{
    /// <summary>
    /// Instrumenting a game through the facade, and writing a provider adapter.
    /// </summary>
    public sealed class AnalyticsSample : MonoBehaviour
    {
        private readonly Dictionary<string, object> _parameters = new();

        private IUnityEventListener _listener;
        private CancellationTokenSource _cts;

        private async void Start()
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            // Register every backend you ship. Events fan out to all of them, so each call
            // site stays a single line no matter how many SDKs are in the build.
            UniAnalytics.Register(new DebugAnalyticsProvider());
            UniAnalytics.Register(new ExampleSdkAdapter());

            await UniAnalytics.InitializeAsync(_cts.Token);

            // Consent gates the facade centrally, so a provider that forgets to honour it
            // still cannot send anything. Wire this to your consent dialog.
            UniAnalytics.SetConsent(true);

            UniAnalytics.SetUserProperty("platform", Application.platform.ToString());

            // Flush on pause: on mobile that is the last callback before the OS may kill the
            // process, and buffered events are lost with it.
            if (IoCStatics.Resolver.TryResolve(out _listener)) _listener.OnPause += HandlePause;

            TrackLevelStart(1);
        }

        private void OnDestroy()
        {
            if (_listener != null) _listener.OnPause -= HandlePause;

            _cts.SafeCancelAndDispose();
        }

        private void HandlePause(bool isPaused)
        {
            if (isPaused) UniAnalytics.Flush();
        }

        /// <summary>
        /// Records a level start with a single parameter.
        /// </summary>
        public void TrackLevelStart(int level)
            // The single-parameter overload reuses one scratch dictionary, so an event fired
            // every few frames does not allocate one per call.
            => UniAnalytics.Track("level_start", "level", level);

        /// <summary>
        /// Records a level end with several parameters.
        /// </summary>
        public void TrackLevelEnd(int level, bool won, float seconds)
        {
            // Reuse one dictionary for multi-parameter events rather than building a new
            // one at every call site.
            _parameters.Clear();
            _parameters["level"] = level;
            _parameters["result"] = won ? "win" : "lose";
            _parameters["duration"] = Mathf.Round(seconds);

            UniAnalytics.Track("level_end", _parameters);
        }

        /// <summary>
        /// Records a completed purchase.
        /// </summary>
        [ContextMenu("Track Purchase")]
        public void TrackPurchase()
            => UniAnalytics.TrackRevenue("starter_pack", "USD", 4.99m);

        /// <summary>
        /// Withdraws consent, e.g. from a privacy screen.
        /// </summary>
        [ContextMenu("Withdraw Consent")]
        public void WithdrawConsent() => UniAnalytics.SetConsent(false);

        /// <summary>
        /// Sketch of a real adapter. Replace the bodies with your SDK's calls.
        /// </summary>
        private sealed class ExampleSdkAdapter : IAnalyticsProvider
        {
            private bool _hasConsent;

            public string Name => "ExampleSdk";

            public bool IsReady { get; private set; }

            public UniTask InitializeAsync(CancellationToken cToken = default)
            {
                // await Sdk.InitializeAsync(apiKey).AsUniTask();
                IsReady = true;
                return UniTask.CompletedTask;
            }

            public void TrackEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
            {
                // An adapter must double-check consent: it may be registered by code that
                // does not go through the facade.
                if (!_hasConsent) return;

                // Sdk.LogEvent(eventName, parameters);
            }

            public void SetUserProperty(string key, object value)
            {
                // Sdk.SetUserProperty(key, value?.ToString());
            }

            public void TrackRevenue(string productId, string currency, decimal amount)
            {
                // Sdk.LogPurchase(productId, currency, (double)amount);
            }

            public void SetConsent(bool hasConsent)
            {
                _hasConsent = hasConsent;
                // Sdk.SetDataCollectionEnabled(hasConsent);
            }

            public void Flush()
            {
                // Sdk.Flush();
            }
        }
    }
}
