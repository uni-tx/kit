using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniTx.Quests
{
    /// <summary>
    /// Where quest progress is stored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The seam that lets a game ship offline-first and add server authority later without
    /// touching a single call site. <see cref="LocalQuestsBackend"/> keeps everything on the
    /// device; a remote implementation forwards the same operations to a validated endpoint
    /// and returns what the server believes.
    /// </para>
    /// <para>
    /// A client-side backend cannot be trusted with anything valuable — save files are
    /// editable, and so are the numbers in them. Treat the local backend as correct for
    /// single-player progression and as UI state everywhere else: a server is what decides
    /// whether a claimed quest was really claimed.
    /// </para>
    /// </remarks>
    public interface IQuestsBackend
    {
        /// <summary>
        /// Indicates whether a server, rather than this device, owns the truth.
        /// </summary>
        bool IsAuthoritative { get; }

        /// <summary>
        /// Indicates whether the backend can be reached right now.
        /// </summary>
        bool IsOnline { get; }

        /// <summary>
        /// Reads the player's stored progress, or a fresh record when none exists.
        /// </summary>
        /// <param name="saveId">The stable save id.</param>
        /// <param name="cToken">Token to cancel the read.</param>
        UniTask<QuestsSavedData> LoadAsync(string saveId, CancellationToken cToken = default);

        /// <summary>
        /// Persists the player's progress.
        /// </summary>
        /// <param name="data">The record to store.</param>
        /// <param name="immediate">Write now rather than at the next batch.</param>
        /// <param name="cToken">Token to cancel the write.</param>
        /// <remarks>
        /// <paramref name="immediate"/> is set for checkpoints a player would notice losing —
        /// a claim — and left clear for routine refreshes.
        /// </remarks>
        UniTask SaveAsync(QuestsSavedData data, bool immediate, CancellationToken cToken = default);
    }
}
