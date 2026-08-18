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

namespace UniTx.Ladder
{
    /// <summary>
    /// The ladder itself: a cumulative climb fed by reported steps, with rung claims that
    /// never cost a player what they earned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every rule that could cost a player something is concentrated here rather than
    /// spread across call sites — a claim is recorded only after delivery succeeds, the
    /// climb only ever moves forward, a failed delivery is retried on the same rung, and a
    /// ladder replacement restarts the climb without deleting the grant ledger. A game
    /// calls two or three methods; the invariants are not its problem.
    /// </para>
    /// <para>
    /// Static and saved data live in a <see cref="LadderEntity"/> — the same entity
    /// foundation every kit system builds on. The entity's save key is stable while its
    /// content key (the ladder id) can be re-pointed, and persistence routes through
    /// <see cref="ILadderBackend"/> so a server can take authority later.
    /// </para>
    /// </remarks>
    public sealed class LadderService : ILadderService
    {
        private UniLadderConfig _config;
        private IContentService _content;
        private ILadderBackend _backend;
        private ILadderRewardGranter _granter;

        private LadderEntity _entity;
        private bool _hasWarnedMultipleLadders;

        /// <summary>
        /// Creates the service; dependencies arrive through <see cref="Inject"/>.
        /// </summary>
        public LadderService()
        {
        }

        /// <summary>
        /// Creates the service with explicit dependencies, for tests and manual wiring.
        /// </summary>
        /// <param name="content">The content service holding ladder definitions.</param>
        /// <param name="backend">Where progress is stored.</param>
        /// <param name="config">Policy. Falls back to Resources/UniLadderConfig.</param>
        public LadderService(IContentService content, ILadderBackend backend,
            UniLadderConfig config = null)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _config = config;
        }

        /// <inheritdoc />
        public bool IsReady { get; private set; }

        /// <inheritdoc />
        public LadderData Ladder => _entity?.Data;

        /// <inheritdoc />
        public LadderSnapshot Snapshot => BuildSnapshot();

        /// <inheritdoc />
        public event Action<LadderSnapshot> OnChanged;

        /// <summary>
        /// Gets the player's persisted progress, or null before initialization.
        /// </summary>
        public LadderSavedData SavedData => _entity?.SavedData;

        /// <inheritdoc />
        public void Inject(IResolver resolver)
        {
            _content ??= resolver.Resolve<IContentService>();

            if (_backend == null && !resolver.TryResolve(out _backend))
            {
                var local = new LocalLadderBackend();
                local.Inject(resolver);
                _backend = local;
            }

            // Optional by design: a game without an economy yet still gets a working ladder.
            if (_granter == null && resolver.TryResolve<ILadderRewardGranter>(out var granter))
            {
                _granter = granter;
            }

            // Default on top of the entity foundation: rewards route through the kit's
            // reward service when it is registered.
            if (_granter == null && resolver.TryResolve<IRewardService>(out var rewards))
            {
                _granter = new LadderRewardGranter(rewards);
            }
        }

        /// <inheritdoc />
        public async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            _config ??= Resources.Load<UniLadderConfig>(UniLadderConfig.DefaultResourcePath);

            if (_config == null)
            {
                UniStatics.LogWarning(
                    "No UniLadderConfig supplied and none found at " +
                    $"Resources/{UniLadderConfig.DefaultResourcePath}; using defaults.", this);

                _config = ScriptableObject.CreateInstance<UniLadderConfig>();
            }
            else
            {
                var problems = _config.DescribeProblems();

                if (!string.IsNullOrEmpty(problems))
                {
                    UniStatics.LogWarning($"UniLadderConfig has problems: {problems}.", this);
                }
            }

            _granter ??= LoggingLadderGranter.Instance;

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
            _hasWarnedMultipleLadders = false;

            _entity?.Reset();
            _entity = null;
        }

        /// <inheritdoc />
        public void SetRewardGranter(ILadderRewardGranter granter) =>
            _granter = granter ?? throw new ArgumentNullException(nameof(granter));

        /// <inheritdoc />
        public async UniTask<int> ReportStepsAsync(int steps,
            CancellationToken cToken = default)
        {
            if (!IsReady || Ladder == null) return 0;

            if (steps <= 0) return 0;

            cToken.ThrowIfCancellationRequested();

            var previousTotal = SavedData.TotalSteps;
            var total = SavedData.AddSteps(steps);

            Raise(new LadderStepsAdded(Ladder.Id, steps, total));

            // Tell the world about every rung this report crossed, in authoring order.
            var reached = 0;

            foreach (var rung in Ladder.Rungs)
            {
                if (rung == null || !rung.IsValid) continue;

                // A rung the climb already crossed before this report is not "newly" reached,
                // and a claimed one must not re-toast even if the total swings over it again
                // after a ladder re-point. Only a crossing from below that is not yet claimed
                // counts.
                if (previousTotal < rung.Steps && total >= rung.Steps &&
                    SavedData.GetRecord(rung.Id) is not { IsClaimed: true })
                {
                    reached++;
                    Raise(new LadderRungReached(Ladder.Id, rung.Id, total));
                }
            }

            if (_config.VerboseLogging)
            {
                UniStatics.LogInfo(
                    $"Ladder steps: +{steps}, now {total} in '{Ladder.Id}'. " +
                    $"{reached} rung(s) reached.", this);
            }

            await PersistAsync(false, cToken);

            RaiseChanged();

            return reached;
        }

        /// <inheritdoc />
        public async UniTask<LadderClaimResult> ClaimAsync(string rungId,
            CancellationToken cToken = default)
        {
            if (!IsReady || Ladder == null) return LadderClaimResult.NoLadder;

            var rung = Ladder.GetRung(rungId);

            if (rung == null) return LadderClaimResult.NoRung;

            cToken.ThrowIfCancellationRequested();

            var record = SavedData.GetRecord(rungId);

            var plan = LadderCalculator.PlanClaim(rung, record, SavedData.TotalSteps);

            switch (plan.Outcome)
            {
                case LadderClaimResult.AlreadyClaimed: return LadderClaimResult.AlreadyClaimed;
                case LadderClaimResult.NotReached: return LadderClaimResult.NotReached;
                case LadderClaimResult.Rejected: return LadderClaimResult.Rejected;
            }

            return await DeliverAsync(rung, record, cToken);
        }

        /// <inheritdoc />
        public async UniTask RefreshAsync(CancellationToken cToken = default)
        {
            if (!IsReady) return;

            cToken.ThrowIfCancellationRequested();

            SetLadder(SelectLadder());

            // A claim that failed earlier is retried here, so a player who saw the failure
            // and simply closed the app still gets the reward on the next launch.
            await RetryFailedClaimsAsync(cToken);

            await PersistAsync(false, cToken);

            RaiseChanged();
        }

        private async UniTask RetryFailedClaimsAsync(CancellationToken cToken)
        {
            if (Ladder == null) return;

            foreach (var rung in Ladder.Rungs)
            {
                if (rung == null || !rung.IsValid) continue;

                var record = SavedData.GetRecord(rung.Id);

                if (record is not { IsFailed: true }) continue;

                await DeliverAsync(rung, record, cToken);
            }
        }

        private async UniTask<LadderClaimResult> DeliverAsync(LadderRungData rung,
            LadderRungRecord record, CancellationToken cToken)
        {
            var failedRewardId = string.Empty;

            foreach (var reward in rung.Rewards)
            {
                if (reward == null || !reward.IsValid) continue;

                var grantId = LadderRungRef.GrantId(Ladder.Id, rung.Id, reward.RewardId);

                // Belt and braces on top of the claim flag: the same rung cannot be
                // claimed twice, but a replayed delivery with the same id must not double-pay.
                if (SavedData.HasAppliedGrant(grantId)) continue;

                var reference = new LadderRungRef(Ladder.Id, rung.Id);

                bool granted;

                try
                {
                    granted = await _granter.GrantAsync(rung, reward, reference, grantId, cToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // A granter that throws is a bug in the game's economy code, not a reason
                    // to mark the rung collected. The rung stays claimable and the next
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
                record ??= SavedData.GetOrCreateRecord(rung.Id);
                record.MarkClaimFailed();
                Raise(new LadderGrantFailed(Ladder.Id, rung.Id, failedRewardId));

                await PersistAsync(true, cToken);
                RaiseChanged();

                return LadderClaimResult.GrantFailed;
            }

            record ??= SavedData.GetOrCreateRecord(rung.Id);
            record.RecordClaim();

            Raise(new LadderRungClaimed(Ladder.Id, rung.Id, rung.Rewards));

            if (Ladder.IsTop(rung))
            {
                Raise(new LadderCompleted(Ladder.Id, rung.Id));
            }

            if (_config.VerboseLogging)
            {
                UniStatics.LogInfo($"Ladder rung claimed: '{rung.Id}' from '{Ladder.Id}'.", this);
            }

            await PersistAsync(true, cToken);

            RaiseChanged();

            return LadderClaimResult.Claimed;
        }

        private void EnsureEntity()
        {
            if (_entity != null) return;

            _entity = new LadderEntity(_config.SaveId, _backend, _content);
        }

        private LadderData SelectLadder()
        {
            var forcedId = _config.ForcedLadderId;

            if (!string.IsNullOrWhiteSpace(forcedId))
            {
                return _content.TryGetData<LadderData>(forcedId, out var forced)
                    ? forced
                    : null;
            }

            LadderData first = null;
            var count = 0;

            foreach (var candidate in _content.GetAllData<LadderData>())
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.Id)) continue;

                count++;

                if (first == null) first = candidate;
            }

            if (count > 1 && !_hasWarnedMultipleLadders)
            {
                _hasWarnedMultipleLadders = true;

                UniStatics.LogWarning(
                    $"{count} ladders are registered but none is forced; using " +
                    $"'{first?.Id}'. Pin UniLadderConfig.ForcedLadderId to select " +
                    "deterministically.", this);
            }

            return first;
        }

        /// <summary>
        /// Points the entity's content key at a ladder and reloads its static data.
        /// </summary>
        /// <param name="ladder">The ladder to show, or null for none.</param>
        private void SetLadder(LadderData ladder)
        {
            _entity.SetDataId(ladder?.Id);
            _entity.ReloadData();

            // A ladder id that changed means the rungs the progress points into are gone;
            // restart the climb while the grant ledger survives.
            if (ladder != null &&
                !string.Equals(SavedData.LadderId, ladder.Id, StringComparison.Ordinal))
            {
                SavedData.BeginLadder(ladder.Id);
            }
        }

        private LadderSnapshot BuildSnapshot()
        {
            if (!IsReady || Ladder == null)
            {
                return new LadderSnapshot(null, null, 0, Array.Empty<LadderRungSnapshot>(),
                    0, 0f, false);
            }

            var rungs = new List<LadderRungSnapshot>(Ladder.Rungs.Count);

            foreach (var rung in Ladder.Rungs)
            {
                if (rung == null) continue;

                var state = LadderCalculator.EvaluateState(rung, SavedData.GetRecord(rung.Id),
                    SavedData.TotalSteps);

                rungs.Add(new LadderRungSnapshot(rung.Id, rung.DisplayName, rung.IconAddress,
                    rung.Steps, state, rung.Rewards, state == LadderState.Reached));
            }

            var progress = LadderCalculator.GetProgress(Ladder, SavedData);

            return new LadderSnapshot(Ladder.Id, Ladder.DisplayName, SavedData.TotalSteps,
                rungs, progress.NextRungSteps, progress.Progress, progress.IsComplete);
        }

        private void RaiseChanged() => OnChanged.SafeInvoke(BuildSnapshot());

        private UniTask PersistAsync(bool isCheckpoint, CancellationToken cToken) =>
            _entity.SaveAsync(isCheckpoint && _config.FlushOnCheckpoint, cToken);

        private static void Raise<TEvent>(TEvent @event)
            where TEvent : struct, IEvent
        {
            // The bus is optional: a game that never bootstrapped UniEvents still gets a
            // working ladder through OnChanged and the awaited results.
            if (UniEvents.IsInitialized) UniEvents.Raise(@event);
        }
    }
}
