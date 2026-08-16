using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace UniTx.Core
{
    /// <summary>
    /// A clock anchored to server time, immune to the player changing the device clock.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads the standard HTTP <c>Date</c> response header from any HTTPS endpoint
    /// (RFC 9110 §6.6.1). That needs no API key, no account and no third-party time
    /// service, so it stays free and has no rate limit to exhaust — unlike a public
    /// time API, which can start failing or paywalling without warning.
    /// </para>
    /// <para>
    /// After a successful sync the clock advances from
    /// <see cref="Time.realtimeSinceStartupAsDouble"/>, which the player cannot alter,
    /// and is computed on read rather than ticked every frame.
    /// </para>
    /// </remarks>
    public sealed class ServerClock : IClock, IInitializableAsync, IResettable
    {
        private const string DefaultTimeServerUrl = "https://www.cloudflare.com/cdn-cgi/trace";
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2f);

        private DateTime _anchorUtc;
        private double _realtimeAtAnchor;
        private bool _isSynchronized;

        /// <summary>
        /// Gets the current UTC time, extrapolated from the last successful server sync.
        /// </summary>
        /// <remarks>
        /// Falls back to <see cref="DateTime.UtcNow"/> until the first sync succeeds, so the
        /// value is always usable — check <see cref="IsSynchronized"/> when that matters.
        /// </remarks>
        public DateTime UtcNow => _isSynchronized
            ? _anchorUtc.AddSeconds(Time.realtimeSinceStartupAsDouble - _realtimeAtAnchor)
            : DateTime.UtcNow;

        /// <summary>
        /// Gets the current Unix timestamp in seconds.
        /// </summary>
        public long UnixTimestampNow => UtcNow.ToUnixTimestamp();

        /// <summary>
        /// Indicates whether the clock has completed at least one successful server sync.
        /// </summary>
        public bool IsSynchronized => _isSynchronized;

        /// <summary>
        /// Synchronizes against the configured time server.
        /// </summary>
        /// <param name="cToken">Token to cancel the sync.</param>
        /// <remarks>
        /// Retries up to <c>UniTxConfig.TimeServerMaxRetries</c> times, then gives up and
        /// leaves the clock on device time rather than blocking startup forever.
        /// </remarks>
        public async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            var url = string.IsNullOrWhiteSpace(UniStatics.Config?.TimeServerUrl)
                ? DefaultTimeServerUrl
                : UniStatics.Config.TimeServerUrl;
            var maxRetries = Mathf.Max(0, UniStatics.Config?.TimeServerMaxRetries ?? 3);

            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                cToken.ThrowIfCancellationRequested();

                if (await TrySyncAsync(url, cToken))
                {
                    return;
                }

                if (attempt < maxRetries)
                {
                    await UniTask.Delay(RetryDelay, cancellationToken: cToken);
                }
            }

            UniStatics.LogWarning(
                $"Could not reach '{url}' after {maxRetries + 1} attempt(s); falling back to device time.", this);
        }

        /// <summary>
        /// Discards the current sync, reverting to device time.
        /// </summary>
        public void Reset()
        {
            _isSynchronized = false;
            _anchorUtc = default;
            _realtimeAtAnchor = 0d;
        }

        private async UniTask<bool> TrySyncAsync(string url, CancellationToken cToken)
        {
            // HEAD keeps the payload to headers only; the Date header is all we need.
            using var request = UnityWebRequest.Head(url);

            try
            {
                await request.SendWebRequest().WithCancellation(cToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnityWebRequestException ex)
            {
                UniStatics.LogInfo($"Time sync request failed: {ex.Message}", this, Color.yellow);
                return false;
            }

            var dateHeader = request.GetResponseHeader("Date");

            if (string.IsNullOrEmpty(dateHeader) ||
                !DateTime.TryParseExact(dateHeader, "r", CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var serverUtc))
            {
                UniStatics.LogInfo($"Time server returned no parsable Date header ('{dateHeader}').", this, Color.yellow);
                return false;
            }

            _anchorUtc = DateTime.SpecifyKind(serverUtc, DateTimeKind.Utc);
            _realtimeAtAnchor = Time.realtimeSinceStartupAsDouble;
            _isSynchronized = true;

            UniStatics.LogInfo($"Clock synchronized to {_anchorUtc:O}.", this);
            return true;
        }
    }
}
