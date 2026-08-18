using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Entity;
using UniTx.IoC;

namespace UniTx.Ladder
{
    /// <summary>
    /// The ladder as an entity: static <see cref="LadderData"/> joined with the per-player
    /// <see cref="LadderSavedData"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the split that makes a ladder replacement safe, now expressed as a
    /// first-class entity. <see cref="EntityBase{TData,TSavedData}.Id"/> is the <b>save
    /// key</b> and never changes — the player's climb lives under <c>ladder</c> forever.
    /// The <b>content key</b> (<see cref="EntityBase{TData,TSavedData}.DataId"/>) is the
    /// ladder id, which the service re-points when a new event ships, without the save ever
    /// moving.
    /// </para>
    /// <para>
    /// Persistence routes through <see cref="ILadderBackend"/>, so server authority slots in
    /// without touching the entity's lifecycle: the backend owns where the record lives,
    /// the entity owns when it loads and saves.
    /// </para>
    /// </remarks>
    public sealed class LadderEntity : EntityBase<LadderData, LadderSavedData>
    {
        private readonly ILadderBackend _backend;

        /// <summary>
        /// Creates the ladder entity.
        /// </summary>
        /// <param name="saveId">The stable save key. Never changes across ladder versions.</param>
        /// <param name="backend">Where the record is stored.</param>
        /// <param name="content">The content service holding ladder definitions.</param>
        public LadderEntity(string saveId, ILadderBackend backend, IContentService content)
            : base(saveId)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));

            // The content key starts as the save key and is re-pointed to the selected
            // ladder on the first refresh. Content is supplied directly rather than resolved
            // from IoC, so the entity works from the service's explicit wiring.
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
        /// <remarks>
        /// The ladder is selected at runtime, so at any moment there may be none — tolerate
        /// that here rather than throwing.
        /// </remarks>
        protected override LadderData LoadData()
            => _contentService != null &&
               _contentService.TryGetData<LadderData>(DataId, out var data)
                ? data
                : null;

        /// <inheritdoc />
        protected override UniTask<LadderSavedData> LoadSavedDataAsync(CancellationToken cToken)
            => _backend.LoadAsync(Id, cToken);

        /// <inheritdoc />
        protected override UniTask SaveSavedDataAsync(bool immediate, CancellationToken cToken)
            => _backend.SaveAsync(SavedData, immediate, cToken);
    }
}
