using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Entity;
using UniTx.IoC;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// The season pass as an entity: static <see cref="SeasonPassData"/> joined with the
    /// per-player <see cref="SeasonPassSavedData"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the split that makes rollover safe, now expressed as a first-class entity.
    /// <see cref="EntityBase{TData,TSavedData}.Id"/> is the <b>save key</b> and never
    /// changes — the player's progress lives under <c>season_pass</c> forever. The
    /// <b>content key</b> (<see cref="EntityBase{TData,TSavedData}.DataId"/>) is the season
    /// id, which changes every season; the service re-points it and reloads on rollover
    /// without the save ever moving.
    /// </para>
    /// <para>
    /// Persistence routes through <see cref="ISeasonPassBackend"/>, so server authority
    /// slots in without touching the entity's lifecycle: the backend owns where the record
    /// lives, the entity owns when it loads and saves.
    /// </para>
    /// </remarks>
    public sealed class SeasonPassEntity : EntityBase<SeasonPassData, SeasonPassSavedData>
    {
        private readonly ISeasonPassBackend _backend;

        /// <summary>
        /// Creates the season pass entity.
        /// </summary>
        /// <param name="saveId">The stable save key. Never changes across seasons.</param>
        /// <param name="backend">Where the record is stored.</param>
        /// <param name="content">The content service holding season definitions.</param>
        public SeasonPassEntity(string saveId, ISeasonPassBackend backend, IContentService content)
            : base(saveId)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));

            // The content key starts as the save key and is re-pointed to the selected
            // season on the first refresh. Content is supplied directly rather than
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
        /// <remarks>
        /// The season is selected at runtime by date, so at any moment there may be none —
        /// tolerate that here rather than throwing.
        /// </remarks>
        protected override SeasonPassData LoadData()
            => _contentService != null &&
               _contentService.TryGetData<SeasonPassData>(DataId, out var data)
                ? data
                : null;

        /// <inheritdoc />
        protected override UniTask<SeasonPassSavedData> LoadSavedDataAsync(CancellationToken cToken)
            => _backend.LoadAsync(Id, cToken);

        /// <inheritdoc />
        protected override UniTask SaveSavedDataAsync(bool immediate, CancellationToken cToken)
            => _backend.SaveAsync(SavedData, immediate, cToken);
    }
}
