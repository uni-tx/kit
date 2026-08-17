using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Entity;
using UniTx.IoC;

namespace UniTx.DailyRewards
{
    /// <summary>
    /// The daily rewards calendar as an entity: static <see cref="DailyRewardsData"/> joined
    /// with the per-player <see cref="DailyRewardsSavedData"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the split that makes a calendar replacement safe, now expressed as a
    /// first-class entity. <see cref="EntityBase{TData,TSavedData}.Id"/> is the <b>save
    /// key</b> and never changes — the player's position lives under <c>daily_rewards</c>
    /// forever. The <b>content key</b> (<see cref="EntityBase{TData,TSavedData}.DataId"/>)
    /// is the calendar id, which the service re-points when a new calendar ships, without
    /// the save ever moving.
    /// </para>
    /// <para>
    /// Persistence routes through <see cref="IDailyRewardsBackend"/>, so server authority
    /// slots in without touching the entity's lifecycle: the backend owns where the record
    /// lives, the entity owns when it loads and saves.
    /// </para>
    /// </remarks>
    public sealed class DailyRewardsEntity : EntityBase<DailyRewardsData, DailyRewardsSavedData>
    {
        private readonly IDailyRewardsBackend _backend;

        /// <summary>
        /// Creates the daily rewards entity.
        /// </summary>
        /// <param name="saveId">The stable save key. Never changes across calendar versions.</param>
        /// <param name="backend">Where the record is stored.</param>
        /// <param name="content">The content service holding calendar definitions.</param>
        public DailyRewardsEntity(string saveId, IDailyRewardsBackend backend,
            IContentService content)
            : base(saveId)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));

            // The content key starts as the save key and is re-pointed to the selected
            // calendar on the first refresh. Content is supplied directly rather than
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
        /// The calendar is selected at runtime, so at any moment there may be none — tolerate
        /// that here rather than throwing.
        /// </remarks>
        protected override DailyRewardsData LoadData()
            => _contentService != null &&
               _contentService.TryGetData<DailyRewardsData>(DataId, out var data)
                ? data
                : null;

        /// <inheritdoc />
        protected override UniTask<DailyRewardsSavedData> LoadSavedDataAsync(CancellationToken cToken)
            => _backend.LoadAsync(Id, cToken);

        /// <inheritdoc />
        protected override UniTask SaveSavedDataAsync(bool immediate, CancellationToken cToken)
            => _backend.SaveAsync(SavedData, immediate, cToken);
    }
}
