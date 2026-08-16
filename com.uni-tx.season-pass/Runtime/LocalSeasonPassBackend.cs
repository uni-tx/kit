using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.IoC;
using UniTx.Serialization;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// Stores season progress on the device through the kit's serialisation service.
    /// </summary>
    /// <remarks>
    /// The default backend, and a complete one: a single-player game needs nothing else.
    /// Writes go through <see cref="ISerialisationService"/>, so they inherit its batching and
    /// its atomic temp-file-then-replace behaviour — an interrupted write costs the last batch
    /// rather than the whole season.
    /// </remarks>
    public sealed class LocalSeasonPassBackend : ISeasonPassBackend, IInjectable
    {
        private ISerialisationService _serialisation;

        /// <summary>
        /// Creates the backend, resolving the serialisation service from the global container.
        /// </summary>
        public LocalSeasonPassBackend()
        {
        }

        /// <summary>
        /// Creates the backend with an explicit serialisation service, for tests.
        /// </summary>
        /// <param name="serialisation">The service to persist through.</param>
        public LocalSeasonPassBackend(ISerialisationService serialisation) =>
            _serialisation = serialisation ?? throw new ArgumentNullException(nameof(serialisation));

        /// <inheritdoc />
        /// <remarks>
        /// Always false. A device save is editable by the player, so nothing valuable should
        /// be settled here.
        /// </remarks>
        public bool IsAuthoritative => false;

        /// <inheritdoc />
        public bool IsOnline => true;

        /// <inheritdoc />
        public void Inject(IResolver resolver) => _serialisation ??= resolver.Resolve<ISerialisationService>();

        /// <inheritdoc />
        public UniTask<SeasonPassSavedData> LoadAsync(string saveId, CancellationToken cToken = default)
        {
            cToken.ThrowIfCancellationRequested();

            var data = _serialisation.Load<SeasonPassSavedData>(saveId);
            data.Migrate();

            return UniTask.FromResult(data);
        }

        /// <inheritdoc />
        public UniTask SaveAsync(SeasonPassSavedData data, bool immediate,
            CancellationToken cToken = default)
        {
            cToken.ThrowIfCancellationRequested();

            _serialisation.Save(data);

            // Flush turns a queued write into a landed one. Worth the disk hit at a checkpoint:
            // a purchase or a claim lost to a crash is a support ticket, not a rounding error.
            if (immediate) _serialisation.Flush();

            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Nothing to sync against — the device already holds the only copy — so queued grants
        /// are simply acknowledged by the caller and dropped.
        /// </remarks>
        public UniTask<SeasonPassSavedData> SyncAsync(SeasonPassSavedData local,
            CancellationToken cToken = default) => UniTask.FromResult<SeasonPassSavedData>(null);
    }
}
