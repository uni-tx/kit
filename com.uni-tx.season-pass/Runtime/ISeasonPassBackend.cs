using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// Where season progress is stored, and who decides what it says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The seam that lets a game ship offline-first and add server authority later without
    /// touching a single call site. <see cref="LocalSeasonPassBackend"/> keeps everything on
    /// the device; a remote implementation forwards the same operations to a validated
    /// endpoint and returns what the server believes.
    /// </para>
    /// <para>
    /// A client-side backend cannot be trusted with anything valuable — save files are
    /// editable, and so are the numbers in them. Treat the local backend as correct for
    /// single-player progression and as UI state everywhere else.
    /// </para>
    /// </remarks>
    public interface ISeasonPassBackend
    {
        /// <summary>
        /// Indicates whether a server, rather than this device, owns the truth.
        /// </summary>
        bool IsAuthoritative { get; }

        /// <summary>
        /// Indicates whether the backend can be reached right now.
        /// </summary>
        /// <remarks>
        /// When false, the service still applies grants locally and queues them, so play
        /// never blocks on connectivity.
        /// </remarks>
        bool IsOnline { get; }

        /// <summary>
        /// Reads the player's stored progress, or a fresh record when none exists.
        /// </summary>
        /// <param name="saveId">The stable save id.</param>
        /// <param name="cToken">Token to cancel the read.</param>
        UniTask<SeasonPassSavedData> LoadAsync(string saveId, CancellationToken cToken = default);

        /// <summary>
        /// Persists the player's progress.
        /// </summary>
        /// <param name="data">The record to store.</param>
        /// <param name="immediate">Write now rather than at the next batch.</param>
        /// <param name="cToken">Token to cancel the write.</param>
        /// <remarks>
        /// <paramref name="immediate"/> is set for checkpoints a player would notice losing —
        /// a claim, a purchase, a rollover — and left clear for routine XP so a value that
        /// changes every match does not cost a disk write every match.
        /// </remarks>
        UniTask SaveAsync(SeasonPassSavedData data, bool immediate, CancellationToken cToken = default);

        /// <summary>
        /// Replays queued grants and returns the authoritative record to reconcile against.
        /// </summary>
        /// <param name="local">The device's current record, including its pending queue.</param>
        /// <param name="cToken">Token to cancel the sync.</param>
        /// <returns>
        /// The record to reconcile with, or null when the backend had nothing to say.
        /// </returns>
        UniTask<SeasonPassSavedData> SyncAsync(SeasonPassSavedData local,
            CancellationToken cToken = default);
    }
}
