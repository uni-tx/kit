using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Analytics
{
    /// <summary>
    /// Writes analytics events to the Unity console instead of sending them anywhere.
    /// </summary>
    /// <remarks>
    /// Ships in the box so instrumentation is verifiable before any SDK is integrated, and
    /// so editor and CI runs never contaminate production dashboards. Register it in
    /// development builds and swap in real adapters for release.
    /// </remarks>
    public sealed class DebugAnalyticsProvider : IAnalyticsProvider
    {
        private readonly StringBuilder _builder = new();
        private readonly List<string> _log = new();
        private bool _hasConsent = true;

        /// <inheritdoc />
        public string Name => "Debug";

        /// <inheritdoc />
        public bool IsReady { get; private set; }

        /// <summary>
        /// Gets every event recorded so far, formatted for assertions in tests.
        /// </summary>
        public IReadOnlyList<string> RecordedEvents => _log;

        /// <inheritdoc />
        public UniTask InitializeAsync(CancellationToken cToken = default)
        {
            IsReady = true;
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public void TrackEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            if (!_hasConsent) return;

            _builder.Clear();
            _builder.Append(eventName);

            if (parameters is { Count: > 0 })
            {
                _builder.Append(" { ");

                var first = true;

                foreach (var pair in parameters)
                {
                    if (!first) _builder.Append(", ");

                    _builder.Append(pair.Key).Append('=').Append(pair.Value);
                    first = false;
                }

                _builder.Append(" }");
            }

            var line = _builder.ToString();
            _log.Add(line);
            UniStatics.LogInfo(line, this, Color.cyan);
        }

        /// <inheritdoc />
        public void SetUserProperty(string key, object value)
        {
            if (!_hasConsent) return;

            var line = $"user.{key}={value}";
            _log.Add(line);
            UniStatics.LogInfo(line, this, Color.cyan);
        }

        /// <inheritdoc />
        public void TrackRevenue(string productId, string currency, decimal amount)
        {
            if (!_hasConsent) return;

            var line = $"revenue {productId} {amount} {currency}";
            _log.Add(line);
            UniStatics.LogInfo(line, this, Color.green);
        }

        /// <inheritdoc />
        public void SetConsent(bool hasConsent) => _hasConsent = hasConsent;

        /// <inheritdoc />
        public void Flush() => UniStatics.LogInfo($"flush ({_log.Count} event(s) so far)", this, Color.cyan);

        /// <summary>
        /// Clears the recorded event log.
        /// </summary>
        public void Clear() => _log.Clear();
    }
}
