using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Core;
using UniTx.Events;
using UniTx.IoC;
using UniTx.Rewards;
using UnityEngine;

namespace UniTx.DailyRewards
{
    /// <summary>
    /// The daily rewards calendar itself: one idempotent claim per day, streak tracking,
    /// and retries that never cost a player what they earned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every rule that could cost a player something is concentrated here rather than
    /// spread across call sites — a claim is recorded only after delivery succeeds, the
    /// position only ever moves forward, a failed delivery is retried on the same slot, and
    /// a calendar replacement resets the position without deleting the history. A game
    /// calls two or three methods; the invariants are not its problem.
    /// </para>
    /// <para>
    /// Static and saved data live in a <see cref="DailyRewardsEntity"/> — the same entity
    /// foundation every kit system builds on. The entity's save key is stable while its
    /// content key (the calendar id) can be re-pointed, and persistence routes through
    /// <see cref="IDailyRewardsBackend"/> so a server can take authority later.
    /// </para>
    /// <para>
    /// Time comes from <see cref="IClock"/> and is passed through a high-water mark, so the
    /// device clock can be moved forward but never back. Bind <c>ServerClock</c> for
    /// anything where forward travel matters too.
    /// </para>
    /// </remarks>
    public sealed class DailyRewardsService : IDailyRewardsService
    {
        private UniDailyRewardsConfig _config;
        private IClock _clock;
        private IContentService _content;
        private IDailyRewardsBackend _backend;
        private IDailyRewardsRewardGranter _granter;

        private DailyRewardsEntity _entity;
        private bool _hasWarnedMultipleCalendars;

        /// <summary>
        /// Creates the service; dependencies arrive through <see cref="Inject"/>.
        /// </summary>
        public DailyRewardsService()
        {
        }

        /// <summary>
        /// Creates the service with explicit dependencies, for tests and manual wiring.
        /// </summary>
        /// <param name="clock">The time source.</param>
        /// <param name="content">The content service holding calendar definitions.</param>
        /// <param name="backend">Where progress is stored.</param>
        /// <param name="config">Policy. Falls back to Resources/UniDailyRewardsConfig.</param>
        public DailyRewardsService(IClock clock, IContentService content,
            IDailyRewardsBackend backend, UniDailyRewardsConfig config = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _config = config;
        }

        /// <inheritdoc />
        public bool IsReady { get; private set; }

        /// <inheritdoc />
        public DailyRewardsData Calendar => _entity?.Data;

        /// <inheritdoc />
        public DailyRewardsSnapshot Snapshot => BuildSnapshot();

        /// <inheritdoc />
        public event Action<DailyRewardsSnapshot> OnChanged;

        /// <summary>
        /// Gets the player's persisted progress, or null before initialization.
        /// </summary>
        public DailyRewardsSavedData SavedData => _entity?.SavedData;

        /// <summary>
        /// Gets the player's persisted progress, or null before initialization.
        /// </summary>
        private DailyRewardsSavedData Saved => _entity?.SavedData;

        /// <inheritdoc />
        public void Inject(IResolver resolver)
        {
            _clock ??= resolver.Resolve<IClock>();
            _content ??= resolver.Resolve<IContentService>();

            if (_backend == null && !resolver.TryResolve(out _backend))
            {
                var local = new LocalDailyRewardsBackend();
                local.Inject(resolver);
                _backend = local;
            }

            // Optional by design: a game without an economy yet still gets a working calendar.
            if (_granter == null && resolver.TryResolve<IDailyRewardsRewardGranter>(out var granter))
            {
                _granter = granter;
            }

            // Default on top of the entity foundation: rewards route through the kit's
            // reward service when it is registered.
            if (_granter == null && resolver.TryResolve<IRewardService>(out var rewards))
            {
                _granter = new DailyRewardsRewardGranter(rewards);
            }
        }

        /// <inheritdoc />
        public async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            _config ??= Resources.Load<UniDailyRewardsConfig>(UniDailyRewardsConfig.DefaultResourcePath);

            if (_config == null)
            {
                UniStatics.LogWarning(
                    "No UniDailyRewardsConfig supplied and none found at " +
                    $"Resources/{UniDailyRewardsConfig.DefaultResourcePath}; using defaults.", this);

                _config = ScriptableObject.CreateInstance<UniDailyRewardsConfig>();
            }
            else
            {
                var problems = _config.DescribeProblems();

                if (!string.IsNullOrEmpty(problems))
                {
                    UniStatics.LogWarning($"UniDailyRewardsConfig has problems: {problems}.", this);
                }
            }

            _granter ??= LoggingDailyRewardsGranter.Instance;

            EnsureEntity();

            // Loads the save through the backend and prepares the entity's data half.
            await _entity.InitializeAsync(cToken);

            IsReady = true;

            await RefreshAsync(cToken);
        }

        /// <inheritdoc />
        public void Reset()
        {
            IsReady = false;
            _hasWarnedMultipleCalendars = false;

            _entity?.Reset();
            _entity = null;
        }

        /// <inheritdoc />
        public void SetRewardGranter(IDailyRewardsRewardGranter granter) =>
            _granter = granter ?? throw new ArgumentNullException(nameof(granter));

        /// <inheritdoc />
        public bool IsClaimable
        {
            get
            {
                if (!IsReady || Calendar == null) return false;

                var plan = DailyRewardsCalculator.PlanClaim(Calendar, Saved, TodayDayStart);

                return plan.Outcome == DailyClaimOutcome.Claimable;
            }
        }

        /// <inheritdoc />
        public async UniTask<DailyClaimResult> ClaimAsync(CancellationToken cToken = default)
        {
            if (!IsReady || Calendar == null) return DailyClaimResult.NoCalendar;

            cToken.ThrowIfCancellationRequested();

            var dayStart = TodayDayStart;

            var plan = DailyRewardsCalculator.PlanClaim(Calendar, Saved, dayStart);

            switch (plan.Outcome)
            {
                case DailyClaimOutcome.AlreadyClaimed: return DailyClaimResult.AlreadyClaimed;
                case DailyClaimOutcome.Finished: return DailyClaimResult.Finished;
            }

            return await DeliverAsync(plan.SlotIndex, dayStart, plan.ResetsStreak, cToken);
        }

        /// <inheritdoc />
        public async UniTask RefreshAsync(CancellationToken cToken = default)
        {
            if (!IsReady) return;

            cToken.ThrowIfCancellationRequested();

            Saved.AdvanceSeen(_clock.UnixTimestampNow);

            SetCalendar(SelectCalendar());

            // A claim that failed earlier today is retried here, so a player who saw the
            // failure and simply closed the app still gets the reward on the next launch.
            if (Calendar != null && Saved.FailedClaimDayStartUnix == TodayDayStart)
            {
                await ClaimAsync(cToken);
            }

            await PersistAsync(false, cToken);

            RaiseChanged();
        }

        private long TodayDayStart =>
            DailyRewardsTime.StartOfDay(EffectiveUnixNow, _config.ResetHourUtc);

        private long EffectiveUnixNow => Saved == null
            ? _clock.UnixTimestampNow
            : Saved.AdvanceSeen(_clock.UnixTimestampNow);

        private async UniTask<DailyClaimResult> DeliverAsync(int slotIndex, long dayStart,
            bool resetsStreak, CancellationToken cToken)
        {
            var slot = Calendar.GetSlot(slotIndex);

            if (slot == null || !slot.IsValid) return DailyClaimResult.Rejected;

            var grantId = DailyRewardRef.GrantId(Calendar.Id, dayStart);

            // Belt and braces on top of the day-boundary guard: the same day cannot be
            // claimed twice, but a replayed delivery with the same id must not double-pay.
            if (Saved.HasAppliedGrant(grantId)) return DailyClaimResult.AlreadyClaimed;

            var reference = new DailyRewardRef(Calendar.Id, slotIndex, slot.Day, slot.RewardId);

            bool delivered;

            try
            {
                delivered = await _granter.GrantAsync(slot, reference, grantId, cToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // A granter that throws is a bug in the game's economy code, not a reason to
                // mark the day collected. The slot stays claimable and the next refresh or
                // claim retries it.
                UniStatics.LogException(exception, this);
                delivered = false;
            }

            if (!delivered)
            {
                Saved.MarkClaimFailed(dayStart);
                Raise(new DailyRewardGrantFailed(Calendar.Id, slotIndex, slot.RewardId));

                await PersistAsync(true, cToken);
                RaiseChanged();

                return DailyClaimResult.GrantFailed;
            }

            var previousStreak = Saved.Streak;
            var streak = resetsStreak || Saved.LastClaimDayStartUnix == 0 ? 1 : previousStreak + 1;

            Saved.RecordClaim(Calendar.Id, slot.Day, slotIndex,
                DailyRewardsCalculator.GetNextSlotIndex(Calendar, slotIndex), streak,
                dayStart, EffectiveUnixNow, grantId);

            if (resetsStreak) Raise(new DailyStreakReset(Calendar.Id, previousStreak));

            Raise(new DailyRewardClaimed(Calendar.Id, slot.Day, slotIndex, slot.RewardId,
                slot.ItemId, slot.Kind, slot.Amount, streak, grantId));

            if (_config.VerboseLogging)
            {
                UniStatics.LogInfo(
                    $"Daily reward claimed: day {slot.Day} of '{Calendar.Id}', " +
                    $"streak {streak}.", this);
            }

            await PersistAsync(true, cToken);

            RaiseChanged();

            return DailyClaimResult.Claimed;
        }

        private void EnsureEntity()
        {
            if (_entity != null) return;

            _entity = new DailyRewardsEntity(
                _config.SaveId, _backend, _content);
        }

        private DailyRewardsData SelectCalendar()
        {
            var forcedId = _config.ForcedCalendarId;

            if (!string.IsNullOrWhiteSpace(forcedId))
            {
                return _content.TryGetData<DailyRewardsData>(forcedId, out var forced)
                    ? forced
                    : null;
            }

            DailyRewardsData first = null;
            var count = 0;

            foreach (var candidate in _content.GetAllData<DailyRewardsData>())
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.Id)) continue;

                count++;

                if (first == null) first = candidate;
            }

            if (count > 1 && !_hasWarnedMultipleCalendars)
            {
                _hasWarnedMultipleCalendars = true;

                UniStatics.LogWarning(
                    $"{count} daily reward calendars are registered but none is forced; " +
                    $"using '{first?.Id}'. Pin UniDailyRewardsConfig.ForcedCalendarId to " +
                    "select deterministically.", this);
            }

            return first;
        }

        /// <summary>
        /// Points the entity's content key at a calendar and reloads its static data.
        /// </summary>
        /// <param name="calendar">The calendar to show, or null for none.</param>
        private void SetCalendar(DailyRewardsData calendar)
        {
            _entity.SetDataId(calendar?.Id);
            _entity.ReloadData();

            // A calendar id that changed means the ladder the position points into is gone;
            // reset the position while the collected-claims history survives.
            if (calendar != null &&
                !string.Equals(Saved.CalendarId, calendar.Id, StringComparison.Ordinal))
            {
                Saved.BeginCalendar(calendar.Id);
            }
        }

        private DailyRewardsSnapshot BuildSnapshot()
        {
            if (!IsReady || Calendar == null)
            {
                return new DailyRewardsSnapshot(null, DailyRewardsState.None, 0, 0, 0, null,
                    false, 0, 0);
            }

            var now = EffectiveUnixNow;
            var dayStart = TodayDayStart;

            var plan = DailyRewardsCalculator.PlanClaim(Calendar, Saved, dayStart);

            var state = plan.Outcome switch
            {
                DailyClaimOutcome.AlreadyClaimed => DailyRewardsState.Claimed,
                DailyClaimOutcome.Finished => DailyRewardsState.Finished,
                _ => DailyRewardsState.Claimable,
            };

            var slotIndex = state switch
            {
                DailyRewardsState.Claimed => DailyRewardsCalculator.GetCurrentSlotIndex(Saved,
                    Calendar.SlotCount),
                DailyRewardsState.Claimable => plan.SlotIndex,
                _ => 0,
            };

            var slot = Calendar.GetSlot(slotIndex);
            var nextClaimUnix = dayStart + DailyRewardsTime.SecondsPerDay;
            var remaining = Math.Max(0, nextClaimUnix - now);

            return new DailyRewardsSnapshot(Calendar.Id, state,
                DailyRewardsCalculator.GetCurrentStreak(Saved, dayStart), Calendar.SlotCount,
                slotIndex, slot, slot?.IsMilestone ?? false, nextClaimUnix, remaining);
        }

        private void RaiseChanged() => OnChanged.SafeInvoke(BuildSnapshot());

        private UniTask PersistAsync(bool isCheckpoint, CancellationToken cToken) =>
            _entity.SaveAsync(isCheckpoint && _config.FlushOnCheckpoint, cToken);

        private static void Raise<TEvent>(TEvent @event)
            where TEvent : struct, IEvent
        {
            // The bus is optional: a game that never bootstrapped UniEvents still gets a
            // working calendar through OnChanged and the awaited results.
            if (UniEvents.IsInitialized) UniEvents.Raise(@event);
        }
    }
}
