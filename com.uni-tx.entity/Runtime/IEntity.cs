using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;

namespace UniTx.Entity
{
    /// <summary>
    /// Runtime entity contract: static content data joined with per-player saved data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An entity pairs two things that must stay independent: the <b>content</b> half, which
    /// ships with the build and is the same for every player, and the <b>saved</b> half,
    /// which belongs to one player. Keeping them apart is what lets a balance patch ship
    /// without rewriting progress.
    /// </para>
    /// <para>
    /// The two halves are keyed separately on purpose. <see cref="Id"/> is the stable
    /// identity and the key the save is stored under; <see cref="DataId"/> is the content
    /// key, which may change at runtime — a season pass has a content id that changes every
    /// season and a save key that must not.
    /// </para>
    /// </remarks>
    public interface IEntity : IInjectable, IInitializableAsync, IResettable
    {
        /// <summary>
        /// Gets the stable identity of the entity. Also the key its saved data is stored under.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the content key the static data is loaded under. May differ from
        /// <see cref="Id"/> and may change at runtime.
        /// </summary>
        string DataId { get; }

        /// <summary>
        /// Indicates whether content and saved data are loaded.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Queues the entity's saved data for the next save batch.
        /// </summary>
        void Save();

        /// <summary>
        /// Persists the entity's saved data.
        /// </summary>
        /// <param name="immediate">Write to disk now rather than at the next batch.</param>
        /// <param name="cToken">Token to cancel the write.</param>
        UniTask SaveAsync(bool immediate = false, CancellationToken cToken = default);
    }
}
