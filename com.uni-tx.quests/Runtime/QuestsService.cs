using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Core;
using UniTx.Events;
using UniTx.IoC;
using UniTx.Rewards;
using UnityEngine;

namespace UniTx.Quests
{
    /// <summary>
    /// The quest board itself: counter objectives fed from gameplay events, one-time, daily
    /// and weekly cadences on UTC resets, and claims that never cost a player what they
    /// earned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every rule that could cost a player something is concentrated here rather than
    /// spread across call sites — a claim is recorded only after delivery succeeds, progress
    /// only ever moves forward, a failed delivery is retried on the same quest, and a board
    /// replacement resets progress without deleting the grant ledger. A game calls two or
    /// three methods; the invariants are not its problem.
    /// </para>
    /// <para>
    /// Static and saved data live in a <see cref="QuestsEntity"/> — the same entity
    /// foundation every kit system builds on. The entity's save key is stable while its
    /// content key (the set id) can be re-pointed, and persistence routes through
    /// <see cref="IQuestsBackend"/> so a server can take authority later.
    /// </para>
    /// <para>
    /// Time comes from <see cref="IClock"/> and is passed through a high-water mark, so the
    /// device clock can be moved forward but never back. Bind <c>ServerClock</c> for
    /// anything where forward travel matters too.
    /// </para>
    /// </remarks>
    public sealed class QuestsService : IQuestsService
    {
        private UniQuestsConfig _config;
        private IClock _clock;
        private IContentService _content;
        private IQuestsBackend _backend;
        private IQuestRewardGranter _granter;

        private QuestsEntity _entity;
        private bool _hasWarnedMultipleSets;

        /// <summary>
        /// Creates the service; dependencies arrive through <see cref="Inject"/>.
        /// </summary>
        public QuestsService()
        {
        }

        /// <summary>
        /// Creates the service with explicit dependencies, for tests and manual wiring.
        /// </summary>
        /// <param name="clock">The time source.</param>
        /// <param name="content">The content service holding quest set definitions.</param>
        /// <param name="backend">Where progress is stored.</param>
        /// <param name="config">Policy. Falls back to Resources/UniQuestsConfig.</param>
        public QuestsService(IClock clock, IContentService content, IQuestsBackend backend,
            UniQuestsConfig config = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _config = config;
        }

        /// <inheritdoc />
        public bool IsReady { get; private set; }

        /// <inheritdoc />
        public QuestSetData Set => _entity?.Data;

        /// <inheritdoc />
        public QuestsSnapshot Snapshot => BuildSnapshot();

        /// <inheritdoc />
        public event Action<QuestsSnapshot> OnChanged;

        /// <summary>
        /// Gets the player's persisted progress, or null before initialization.
        /// </summary>
        public QuestsSavedData SavedData => _entity?.SavedData;

        /// <inheritdoc />
        public void Inject(IResolver resolver)
        {
            _clock ??= resolver.Resolve<IClock>();
            _content ??= resolver.Resolve<IContentService>();

            if (_backend == null && !resolver.TryResolve(out _backend))
            {
                var local = new LocalQuestsBackend();
                local.Inject(resolver);
                _backend = local;
            }

            // Optional by design: a game without an economy yet still gets a working board.
            if (_granter == null && resolver.TryResolve<IQuestRewardGranter>(out var granter))
            {
                _granter = granter;
            }

            // Default on top of the entity foundation: rewards route through the kit's
            // reward service when it is registered.
            if (_granter == null && resolver.TryResolve<IRewardService>(out var rewards))
            {
                _granter = new QuestRewardGranter(rewards);
            }
        }

        /// <inheritdoc />
        public async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            _config ??= Resources.Load<UniQuestsConfig>(UniQuestsConfig.DefaultResourcePath);

            if (_config == null)
            {
                UniStatics.LogWarning(
                    "No UniQuestsConfig supplied and none found at " +
                    $"Resources/{UniQuestsConfig.DefaultResourcePath}; using defaults.", this);

                _config = ScriptableObject.CreateInstance<UniQuestsConfig>();
            }
            else
            {
                var problems = _config.DescribeProblems();

                if (!string.IsNullOrEmpty(problems))
                {
                    UniStatics.LogWarning($"UniQuestsConfig has problems: {problems}.", this);
                }
            }

            _granter ??= LoggingQuestGranter.Instance;

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
            _hasWarnedMultipleSets = false;

            _entity?.Reset();
            _entity = null;
        }

        /// <inheritdoc />
        public void SetRewardGranter(IQuestRewardGranter granter) =>
            _granter = granter ?? throw new ArgumentNullException(nameof(granter));

        /// <inheritdoc />
        public async UniTask<int> ReportProgressAsync(string objectiveKey, int amount,
            CancellationToken cToken = default)
        {
            if (!IsReady || Set == null) return 0;

            if (string.IsNullOrWhiteSpace(objectiveKey) || amount <= 0) return 0;

            cToken.ThrowIfCancellationRequested();

            var now = EffectiveUnixNow;

            // A session left open across a reset boundary reports into a stale period unless
            // rollovers run first. Cheap: a few boundary comparisons per quest.
            RolloverPeriods(now);

            var advanced = 0;

            foreach (var quest in Set.Quests)
            {
                if (quest == null || !quest.IsValid) continue;

                var prerequisiteClaimed = IsPrerequisiteClaimed(quest);
                var record = SavedData.GetRecord(quest.Id);
                var periodStart = QuestTime.GetPeriodStart(quest.Reset, now,
                    _config.ResetHourUtc, _config.WeekStartDay);

                var plan = QuestCalculator.PlanReport(quest, record, prerequisiteClaimed,
                    objectiveKey, amount, periodStart);

                if (plan == null) continue;

                var report = plan.Value;
                var wasStarted = record != null && QuestCalculator.HasProgress(record);
                var wasComplete = record != null &&
                                  QuestCalculator.IsComplete(quest, record);

                record ??= SavedData.GetOrCreateRecord(quest.Id, periodStart);
                record.AddProgress(objectiveKey, report.Added);
                advanced++;

                if (!wasStarted)
                {
                    Raise(new QuestStarted(Set.Id, quest.Id));
                }

                Raise(new QuestProgressed(Set.Id, quest.Id, report.ObjectiveKey,
                    report.Current, report.Target));

                if (!wasComplete && QuestCalculator.IsComplete(quest, record))
                {
                    Raise(new QuestCompleted(Set.Id, quest.Id));
                }

                if (_config.VerboseLogging)
                {
                    UniStatics.LogInfo(
                        $"Quest progress: '{quest.Id}' {report.ObjectiveKey} " +
                        $"{report.Current}/{report.Target}.", this);
                }
            }

            if (advanced > 0)
            {
                await PersistAsync(false, cToken);
                RaiseChanged();
            }

            return advanced;
        }

        /// <inheritdoc />
        public async UniTask<QuestClaimResult> ClaimAsync(string questId,
            CancellationToken cToken = default)
        {
            if (!IsReady || Set == null) return QuestClaimResult.NoSet;

            var quest = Set.GetQuest(questId);

            if (quest == null) return QuestClaimResult.NoQuest;

            cToken.ThrowIfCancellationRequested();

            var now = EffectiveUnixNow;

            RolloverPeriods(now);

            var record = SavedData.GetRecord(questId);
            var periodStart = QuestTime.GetPeriodStart(quest.Reset, now,
                _config.ResetHourUtc, _config.WeekStartDay);

            var plan = QuestCalculator.PlanClaim(quest, record, IsPrerequisiteClaimed(quest),
                periodStart);

            switch (plan.Outcome)
            {
                case QuestClaimResult.AlreadyClaimed: return QuestClaimResult.AlreadyClaimed;
                case QuestClaimResult.NotCompleted: return QuestClaimResult.NotCompleted;
                case QuestClaimResult.Locked: return QuestClaimResult.Locked;
                case QuestClaimResult.Rejected: return QuestClaimResult.Rejected;
            }

            return await DeliverAsync(quest, record, periodStart, cToken);
        }

        /// <inheritdoc />
        public async UniTask RefreshAsync(CancellationToken cToken = default)
        {
            if (!IsReady) return;

            cToken.ThrowIfCancellationRequested();

            var now = EffectiveUnixNow;

            SetBoard(SelectSet());

            RolloverPeriods(now);

            // A claim that failed earlier is retried here, so a player who saw the failure
            // and simply closed the app still gets the reward on the next launch.
            await RetryFailedClaimsAsync(cToken);

            await PersistAsync(false, cToken);

            RaiseChanged();
        }

        private long EffectiveUnixNow => SavedData == null
            ? _clock.UnixTimestampNow
            : SavedData.AdvanceSeen(_clock.UnixTimestampNow);

        private bool IsPrerequisiteClaimed(QuestData quest)
        {
            if (string.IsNullOrWhiteSpace(quest.RequiredQuestId)) return true;

            return SavedData.GetRecord(quest.RequiredQuestId) is { IsClaimed: true };
        }

        private void RolloverPeriods(long now)
        {
            if (Set == null) return;

            foreach (var quest in Set.Quests)
            {
                if (quest == null || quest.Reset == QuestReset.None) continue;

                var periodStart = QuestTime.GetPeriodStart(quest.Reset, now,
                    _config.ResetHourUtc, _config.WeekStartDay);

                var record = SavedData.GetRecord(quest.Id);

                // A stale record belongs to an earlier period: wipe it, and tell the world.
                if (record != null && record.PeriodStartUnix != 0 &&
                    record.PeriodStartUnix != periodStart)
                {
                    record.BeginPeriod(periodStart);
                    Raise(new QuestPeriodReset(Set.Id, quest.Id, quest.Reset));
                }
            }
        }

        private async UniTask RetryFailedClaimsAsync(CancellationToken cToken)
        {
            if (Set == null) return;

            var now = EffectiveUnixNow;

            foreach (var quest in Set.Quests)
            {
                if (quest == null || !quest.IsValid) continue;

                var record = SavedData.GetRecord(quest.Id);

                if (record == null) continue;

                var periodStart = QuestTime.GetPeriodStart(quest.Reset, now,
                    _config.ResetHourUtc, _config.WeekStartDay);

                if (record.FailedPeriodStartUnix != periodStart) continue;

                await DeliverAsync(quest, record, periodStart, cToken);
            }
        }

        private async UniTask<QuestClaimResult> DeliverAsync(QuestData quest,
            QuestRecord record, long periodStart, CancellationToken cToken)
        {
            var failedRewardId = string.Empty;

            foreach (var reward in quest.Rewards)
            {
                if (reward == null || !reward.IsValid) continue;

                var grantId = QuestRef.GrantId(Set.Id, quest.Id, periodStart, reward.RewardId);

                // Belt and braces on top of the claim flag: the same period cannot be
                // claimed twice, but a replayed delivery with the same id must not double-pay.
                if (SavedData.HasAppliedGrant(grantId)) continue;

                var reference = new QuestRef(Set.Id, quest.Id);

                bool granted;

                try
                {
                    granted = await _granter.GrantAsync(quest, reward, reference, grantId, cToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // A granter that throws is a bug in the game's economy code, not a reason
                    // to mark the quest collected. The quest stays claimable and the next
                    // refresh or claim retries it.
                    UniStatics.LogException(exception, this);
                    granted = false;
                }

                if (!granted)
                {
                    failedRewardId = reward.RewardId;
                    break;
                }

                SavedData.RecordGrantId(grantId);
            }

            if (!string.IsNullOrEmpty(failedRewardId))
            {
                record.MarkClaimFailed(periodStart);
                Raise(new QuestGrantFailed(Set.Id, quest.Id, failedRewardId));

                await PersistAsync(true, cToken);
                RaiseChanged();

                return QuestClaimResult.GrantFailed;
            }

            record.RecordClaim(periodStart);

            Raise(new QuestClaimed(Set.Id, quest.Id, quest.Rewards));

            if (_config.VerboseLogging)
            {
                UniStatics.LogInfo($"Quest claimed: '{quest.Id}' from '{Set.Id}'.", this);
            }

            await PersistAsync(true, cToken);

            RaiseChanged();

            return QuestClaimResult.Claimed;
        }

        private void EnsureEntity()
        {
            if (_entity != null) return;

            _entity = new QuestsEntity(_config.SaveId, _backend, _content);
        }

        private QuestSetData SelectSet()
        {
            var forcedId = _config.ForcedSetId;

            if (!string.IsNullOrWhiteSpace(forcedId))
            {
                return _content.TryGetData<QuestSetData>(forcedId, out var forced)
                    ? forced
                    : null;
            }

            QuestSetData first = null;
            var count = 0;

            foreach (var candidate in _content.GetAllData<QuestSetData>())
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.Id)) continue;

                count++;

                if (first == null) first = candidate;
            }

            if (count > 1 && !_hasWarnedMultipleSets)
            {
                _hasWarnedMultipleSets = true;

                UniStatics.LogWarning(
                    $"{count} quest sets are registered but none is forced; using " +
                    $"'{first?.Id}'. Pin UniQuestsConfig.ForcedSetId to select " +
                    "deterministically.", this);
            }

            return first;
        }

        /// <summary>
        /// Points the entity's content key at a set and reloads its static data.
        /// </summary>
        /// <param name="set">The set to show, or null for none.</param>
        private void SetBoard(QuestSetData set)
        {
            _entity.SetDataId(set?.Id);
            _entity.ReloadData();

            // A set id that changed means the quests the progress points into are gone;
            // reset the progress while the grant ledger survives.
            if (set != null &&
                !string.Equals(SavedData.SetId, set.Id, StringComparison.Ordinal))
            {
                SavedData.BeginSet(set.Id);
            }
        }

        private QuestsSnapshot BuildSnapshot()
        {
            if (!IsReady || Set == null)
            {
                return new QuestsSnapshot(null, Array.Empty<QuestSnapshot>(), 0, 0);
            }

            var now = EffectiveUnixNow;

            var quests = new List<QuestSnapshot>(Set.Quests.Count);

            foreach (var quest in Set.Quests)
            {
                if (quest == null) continue;

                var record = SavedData.GetRecord(quest.Id);
                var prerequisiteClaimed = IsPrerequisiteClaimed(quest);
                var state = QuestCalculator.EvaluateState(quest, record, prerequisiteClaimed);

                var objectives = new List<QuestObjectiveSnapshot>(quest.Objectives.Count);
                var completed = 0;

                foreach (var objective in quest.Objectives)
                {
                    if (objective == null) continue;

                    var current = Math.Min(record?.GetCurrent(objective.Key) ?? 0,
                        objective.Target);
                    var isComplete = current >= objective.Target;

                    if (isComplete) completed++;

                    objectives.Add(new QuestObjectiveSnapshot(objective, current, isComplete));
                }

                quests.Add(new QuestSnapshot(quest.Id, quest.DisplayName, quest.Description,
                    quest.IconAddress, state, quest.Order, completed, quest.Objectives.Count,
                    objectives, quest.Rewards, state == QuestState.Completed));
            }

            var nextReset = QuestCalculator.GetNextResetUnix(Set.Quests, now,
                _config.ResetHourUtc, _config.WeekStartDay);
            var remaining = nextReset == 0 ? 0 : Math.Max(0, nextReset - now);

            return new QuestsSnapshot(Set.Id, quests, nextReset, remaining);
        }

        private void RaiseChanged() => OnChanged.SafeInvoke(BuildSnapshot());

        private UniTask PersistAsync(bool isCheckpoint, CancellationToken cToken) =>
            _entity.SaveAsync(isCheckpoint && _config.FlushOnCheckpoint, cToken);

        private static void Raise<TEvent>(TEvent @event)
            where TEvent : struct, IEvent
        {
            // The bus is optional: a game that never bootstrapped UniEvents still gets a
            // working board through OnChanged and the awaited results.
            if (UniEvents.IsInitialized) UniEvents.Raise(@event);
        }
    }
}
