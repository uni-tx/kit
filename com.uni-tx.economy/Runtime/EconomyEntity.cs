using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Entity;
using UniTx.IoC;

namespace UniTx.Economy
{
    /// <summary>
    /// An economy as an entity: static <see cref="EconomyData"/> joined with the per-player
    /// <see cref="EconomySavedData"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One entity per economy, content-defined. The save key is <c>economy:&lt;id&gt;</c> so
    /// N economies never collide on disk, and the content key is the economy id — the
    /// service re-points it when a replacement economy ships, without the save moving.
    /// </para>
    /// <para>
    /// Persistence routes through <see cref="IEconomyBackend"/>, so server authority slots
    /// in without touching the entity's lifecycle: the backend owns where the record lives,
    /// the entity owns when it loads and saves.
    /// </para>
    /// </remarks>
    public sealed class EconomyEntity : EntityBase<EconomyData, EconomySavedData>
    {
        private readonly IEconomyBackend _backend;

        /// <summary>
        /// Creates an economy entity.
        /// </summary>
        /// <param name="saveId">The stable save key, e.g. <c>economy:core</c>.</param>
        /// <param name="backend">Where the record is stored.</param>
        /// <param name="content">The content service holding economy definitions.</param>
        public EconomyEntity(string saveId, IEconomyBackend backend, IContentService content)
            : base(saveId)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));

            // The content key starts as the save key and is re-pointed to the selected
            // economy on the first refresh. Content is supplied directly rather than
            // resolved from IoC, so the entity works from the service's explicit wiring.
            _contentService = content;
        }

        /// <inheritdoc />
        protected override void OnInject(IResolver resolver)
        {
        }

        /// <inheritdoc />
        protected override UniTask OnInitAsync(CancellationToken cToken)
        {
            // Custom backends may not migrate; the built-in ones do, so this is a no-op
            // there. Migrate is idempotent, so double-running it costs nothing.
            SavedData.Migrate();

            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        protected override void OnReset()
        {
        }

        /// <inheritdoc />
        protected override EconomyData LoadData()
            => _contentService != null &&
               _contentService.TryGetData<EconomyData>(DataId, out var data)
                ? data
                : null;

        /// <inheritdoc />
        protected override UniTask<EconomySavedData> LoadSavedDataAsync(CancellationToken cToken)
            => _backend.LoadAsync(Id, cToken);

        /// <inheritdoc />
        protected override UniTask SaveSavedDataAsync(bool immediate, CancellationToken cToken)
            => _backend.SaveAsync(SavedData, immediate, cToken);
    }
}
