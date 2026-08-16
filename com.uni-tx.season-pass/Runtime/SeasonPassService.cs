using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Core;
using UniTx.Events;
using UniTx.IoC;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// The season pass itself: earning, ownership, claiming, expiry and rollover.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every rule that could cost a player something they earned is concentrated here rather
    /// than spread across call sites — a claim is recorded only after delivery succeeds, XP
    /// only ever moves up, ownership back-grants what it should, and a rollover archives
    /// instead of deleting. A game calls four or five methods; the invariants are not its
    /// problem.
    /// </para>
    /// <para>
    /// Time comes from <see cref="IClock"/> and is passed through a high-water mark, so the
    /// device clock can be moved forward but never back. Bind <c>ServerClock</c> for anything
    /// where forward travel matters too.
    /// </para>
    /// </remarks>
    public sealed class SeasonPassService : ISeasonPassService
    {
        private const string QuestXpSource = "__quest";
        private const string TierSkipXpSource = "__tier_skip";

        private readonly List<SeasonRewardRef> _claimBuffer = new();
        private readonly List<SeasonRewardRef> _workBuffer = new();
        private readonly List<string> _questIdBuffer = new();

        private UniSeasonPassConfig _config;
        private IClock _clock;
        private IContentService _content;
        private ISeasonPassBackend _backend;
        private ISeasonPassRewardGranter _granter;
        private ISeasonPassWallet _wallet;

        private SeasonPassSavedData _saved;
        private SeasonPassData _season;
        private SeasonPhase _phase = SeasonPhase.None;
        private bool _hasRaisedEndingSoon;

        /// <summary>
        /// Creates the service; dependencies arrive through <see cref="Inject"/>.
        /// </summary>
        public SeasonPassService()
        {
        }

        /// <summary>
        /// Creates the service with explicit dependencies, for tests and manual wiring.
        /// </summary>
        /// <param name="clock">The time source.</param>
        /// <param name="content">The content service holding season definitions.</param>
        /// <param name="backend">Where progress is stored.</param>
        /// <param name="config">Policy. Falls back to Resources/UniSeasonPassConfig.</param>
        public SeasonPassService(IClock clock, IContentService content, ISeasonPassBackend backend,
            UniSeasonPassConfig config = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _config = config;
        }

        /// <inheritdoc />
        public bool IsReady { get; private set; }

        /// <inheritdoc />
        public SeasonPassData Season => _season;

        /// <inheritdoc />
        public SeasonPhase Phase => _phase;

        /// <inheritdoc />
        public SeasonPassSnapshot Snapshot => BuildSnapshot();

        /// <inheritdoc />
        public event Action<SeasonPassSnapshot> OnChanged;

        /// <summary>
        /// Gets the player's persisted progress, or null before initialization.
        /// </summary>
        public SeasonPassSavedData SavedData => _saved;

        /// <inheritdoc />
        public void Inject(IResolver resolver)
        {
            _clock ??= resolver.Resolve<IClock>();
            _content ??= resolver.Resolve<IContentService>();

            if (_backend == null && !resolver.TryResolve(out _backend))
            {
                var local = new LocalSeasonPassBackend();
                local.Inject(resolver);
                _backend = local;
            }

            // Optional by design: a game without an economy yet still gets a working pass.
            if (_granter == null) resolver.TryResolve(out _granter);
            if (_wallet == null) resolver.TryResolve(out _wallet);
        }

        /// <inheritdoc />
        public async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            _config ??= UnityEngine.Resources.Load<UniSeasonPassConfig>(
                UniSeasonPassConfig.DefaultResourcePath);

            if (_config == null)
            {
                UniStatics.LogWarning(
                    "No UniSeasonPassConfig supplied and none found at " +
                    $"Resources/{UniSeasonPassConfig.DefaultResourcePath}; using defaults.", this);

                _config = UnityEngine.ScriptableObject.CreateInstance<UniSeasonPassConfig>();
            }
            else
            {
                var problems = _config.DescribeProblems();

                if (!string.IsNullOrEmpty(problems))
                {
                    UniStatics.LogWarning($"UniSeasonPassConfig has problems: {problems}.", this);
                }
            }

            _granter ??= new LoggingRewardGranter();
            _wallet ??= new NoOpSeasonPassWallet();

            _saved = await _backend.LoadAsync(_config.SaveId, cToken);
            _saved.Id ??= _config.SaveId;
            _saved.Migrate();

            IsReady = true;

            await RefreshAsync(cToken);
        }

        /// <inheritdoc />
        public void Reset()
        {
            IsReady = false;
            _saved = null;
            _season = null;
            _phase = SeasonPhase.None;
            _hasRaisedEndingSoon = false;

            _claimBuffer.Clear();
            _workBuffer.Clear();
            _questIdBuffer.Clear();
        }

        /// <inheritdoc />
        public void SetRewardGranter(ISeasonPassRewardGranter granter) =>
            _granter = granter ?? throw new ArgumentNullException(nameof(granter));

        /// <inheritdoc />
        public void SetWallet(ISeasonPassWallet wallet) =>
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));

        /// <inheritdoc />
        public bool OwnsTrack(SeasonTrack track) => _saved != null && _saved.Owns(track);

        /// <inheritdoc />
        public bool IsClaimable(SeasonRewardRef reward)
        {
            if (!IsReady || _season == null || !CanClaim) return false;
            if (_saved.HasClaimed(reward.ToClaimKey())) return false;
            if (!_saved.Owns(reward.Track)) return false;
            if (reward.Tier > CurrentTier) return false;

            return FindReward(reward) != null;
        }

        /// <inheritdoc />
        public int GetClaimable(List<SeasonRewardRef> buffer)
        {
            if (buffer == null) return 0;

            buffer.Clear();

            if (!IsReady || _season == null || !CanClaim) return 0;

            SeasonPassCalculator.CollectUnlockedRewards(_season, CurrentTier, _workBuffer);

            foreach (var reward in _workBuffer)
            {
                if (!_saved.Owns(reward.Track)) continue;
                if (_saved.HasClaimed(reward.ToClaimKey())) continue;

                buffer.Add(reward);
            }

            return buffer.Count;
        }

        /// <inheritdoc />
        public async UniTask<XpGrantResult> GrantXpAsync(string sourceId, int amount = 0,
            string grantId = null, CancellationToken cToken = default)
        {
            if (!IsReady || _season == null) return XpGrantResult.Rejected;

            if (!_season.TryGetXpSource(sourceId, out var source)) return XpGrantResult.UnknownSource;

            if (!CanEarn) return XpGrantResult.SeasonInactive;

            if (source.RequiresPaidTrack && !_saved.Owns(SeasonTrack.Premium))
            {
                return XpGrantResult.TrackNotOwned;
            }

            var requested = amount > 0 ? amount : source.XpPerEvent;

            if (requested <= 0) return XpGrantResult.Rejected;

            if (!string.IsNullOrEmpty(grantId) && _saved.HasAppliedGrant(grantId))
            {
                return XpGrantResult.Duplicate;
            }

            if (!_config.AllowOfflineGrants && !_backend.IsOnline) return XpGrantResult.Offline;

            RollDailyWindow();

            var granted = requested;
            SeasonSourceDailyXp daily = null;

            if (source.DailyCap > 0)
            {
                daily = _saved.GetOrCreateDailyXp(source.SourceId);
                granted = Math.Min(requested, Math.Max(0, source.DailyCap - daily.Xp));

                if (granted <= 0) return XpGrantResult.Capped;
            }

            await ApplyXpAsync(granted, source.SourceId, grantId, cToken);

            // Charged only once the XP has actually landed. Charging first meant a cancelled
            // grant consumed the player's daily allowance and gave them nothing for it.
            daily?.Add(granted);

            return granted < requested ? XpGrantResult.Capped : XpGrantResult.Granted;
        }

        /// <inheritdoc />
        public async UniTask<ClaimResult> ClaimAsync(SeasonRewardRef reward,
            CancellationToken cToken = default)
        {
            if (!IsReady || _season == null) return ClaimResult.NoSeason;
            if (!CanClaim) return ClaimResult.SeasonExpired;

            var data = FindReward(reward);

            if (data == null) return ClaimResult.NothingToClaim;
            if (_saved.HasClaimed(reward.ToClaimKey())) return ClaimResult.AlreadyClaimed;
            if (reward.Tier > CurrentTier) return ClaimResult.TierNotReached;
            if (!_saved.Owns(reward.Track)) return ClaimResult.TrackNotOwned;

            var delivered = await DeliverAsync(data, reward, false, cToken);

            if (delivered) await PersistAsync(true, cToken);

            RaiseChanged();

            return delivered ? ClaimResult.Claimed : ClaimResult.GrantFailed;
        }

        /// <inheritdoc />
        public async UniTask<int> ClaimTierAsync(int tier, SeasonTrack track,
            CancellationToken cToken = default)
        {
            if (!IsReady || _season == null || !CanClaim) return 0;
            if (tier > CurrentTier || !_saved.Owns(track)) return 0;

            var claimed = 0;

            foreach (var reward in _season.GetRewards(tier))
            {
                if (reward == null || !reward.IsValid || reward.Track != track) continue;

                var reference = new SeasonRewardRef(_season.Id, tier, track, reward.RewardId);

                if (_saved.HasClaimed(reference.ToClaimKey())) continue;

                if (await DeliverAsync(reward, reference, false, cToken)) claimed++;
            }

            // One flush for the whole tier rather than one per reward. Routing through
            // ClaimAsync here meant a three-reward tier cost three synchronous disk writes.
            if (claimed > 0) await PersistAsync(true, cToken);

            RaiseChanged();

            return claimed;
        }

        /// <inheritdoc />
        public async UniTask<int> ClaimAllAsync(CancellationToken cToken = default)
        {
            if (!IsReady || _season == null || !CanClaim) return 0;

            var claimed = await RetryFailedClaimsAsync(cToken);

            GetClaimable(_claimBuffer);

            // Snapshot into a local array: delivering mutates the claimed set, and the buffer
            // is shared with the UI, which may be enumerating it on the same frame.
            var pending = _claimBuffer.ToArray();

            foreach (var reward in pending)
            {
                var data = FindReward(reward);

                if (data == null) continue;

                if (await DeliverAsync(data, reward, false, cToken)) claimed++;
            }

            if (claimed > 0) await PersistAsync(true, cToken);

            RaiseChanged();

            return claimed;
        }

        /// <inheritdoc />
        public async UniTask<TrackUnlockResult> UnlockTrackAsync(SeasonTrack track,
            SeasonPassPayment payment = SeasonPassPayment.Currency,
            CancellationToken cToken = default)
        {
            if (!IsReady || _season == null || track == SeasonTrack.Free)
            {
                return TrackUnlockResult.Rejected;
            }

            if (_saved.Owns(track)) return TrackUnlockResult.AlreadyOwned;
            if (!CanEarn) return TrackUnlockResult.SeasonInactive;

            var offer = _season.GetOffer(track);

            if (offer == null) return TrackUnlockResult.NotPurchasable;

            if (payment == SeasonPassPayment.Currency)
            {
                if (!offer.SellsForCurrency) return TrackUnlockResult.NotPurchasable;

                if (!_wallet.TrySpend(offer.CurrencyId, offer.CurrencyCost))
                {
                    return TrackUnlockResult.InsufficientFunds;
                }
            }

            var previouslyOwned = _saved.HighestOwnedTrack;

            _saved.GrantTrack(track);

            Raise(new SeasonTrackUnlocked(_season.Id, track, payment));

            if (offer.IncludedTierSkips > 0) ApplyTierSkips(offer.IncludedTierSkips);

            // The point of buying mid-season: every tier already passed on the new track pays
            // out immediately. Without this the player pays for rewards they cannot reach.
            // Scoped to the tracks just unlocked — a free reward the player chose not to
            // collect yet is theirs to collect, not something a purchase should spend for them.
            await GrantBacklogAsync(cToken, previouslyOwned + 1);
            await PersistAsync(true, cToken);

            RaiseChanged();

            return TrackUnlockResult.Unlocked;
        }

        /// <inheritdoc />
        public async UniTask<int> BuyTierSkipsAsync(int count,
            SeasonPassPayment payment = SeasonPassPayment.Currency,
            CancellationToken cToken = default)
        {
            if (!IsReady || _season == null || count <= 0 || !CanEarn) return 0;

            if (_season.MaxTierSkipPurchases > 0)
            {
                var remaining = _season.MaxTierSkipPurchases - _saved.PurchasedTierSkips;

                count = Math.Min(count, Math.Max(0, remaining));

                if (count == 0) return 0;
            }

            if (payment == SeasonPassPayment.Currency)
            {
                if (!_season.SellsTierSkipsForCurrency) return 0;

                if (!_wallet.TrySpend(_season.TierSkipCurrencyId, _season.TierSkipCurrencyCost * count))
                {
                    return 0;
                }
            }

            _saved.RecordTierSkipPurchase(count);

            ApplyTierSkips(count);

            await GrantBacklogIfAutoClaimAsync(cToken);
            await PersistAsync(true, cToken);

            RaiseChanged();

            return count;
        }

        /// <inheritdoc />
        public async UniTask<QuestProgressResult> ReportQuestProgressAsync(string questId,
            int amount = 1, CancellationToken cToken = default)
        {
            if (!IsReady || _season == null || amount <= 0) return QuestProgressResult.Rejected;
            if (!_season.TryGetQuest(questId, out var quest)) return QuestProgressResult.UnknownQuest;
            if (!CanEarn) return QuestProgressResult.Rejected;

            if (!quest.IsAvailableAt(UtcNow)) return QuestProgressResult.Unavailable;

            if (quest.RequiresPaidTrack && !_saved.Owns(SeasonTrack.Premium))
            {
                return QuestProgressResult.Unavailable;
            }

            var progress = _saved.GetOrCreateQuest(questId);

            if (progress.IsComplete) return QuestProgressResult.AlreadyComplete;

            progress.Advance(amount);

            if (progress.Amount < quest.Goal)
            {
                await PersistAsync(false, cToken);
                RaiseChanged();

                return QuestProgressResult.Advanced;
            }

            progress.Complete();

            // Quest XP bypasses the source whitelist and daily caps: the quest definition is
            // itself the ceiling, and it can only ever pay out once.
            await ApplyXpAsync(quest.XpReward, QuestXpSource, $"quest:{_season.Id}:{questId}", cToken);

            Raise(new SeasonQuestCompleted(_season.Id, questId, quest.XpReward));

            await PersistAsync(true, cToken);

            return QuestProgressResult.Completed;
        }

        /// <inheritdoc />
        public async UniTask RefreshAsync(CancellationToken cToken = default)
        {
            if (!IsReady) return;

            _saved.AdvanceSeen(_clock.UnixTimestampNow);

            var selected = SelectSeason();

            if (selected != null &&
                !string.Equals(selected.Id, _saved.SeasonId, StringComparison.Ordinal) &&
                UtcNow >= selected.StartUtc)
            {
                await RollOverAsync(selected, cToken);
            }
            else
            {
                // A season that has been announced but has not begun is shown, never rolled
                // into. Wiping the save the moment a teaser appears costs the player their
                // standing early and buys nothing — the rollover happens when it starts.
                _season = selected;
            }

            RollDailyWindow();
            RollQuestWindows();

            if (_config.SyncOnRefresh && _backend.IsOnline) await SyncAsync(cToken);

            UpdatePhase();

            // An ended season under the forgiving policy pays out whatever the player earned
            // and never collected, so a forgotten tap does not cost them the season.
            if (_phase == SeasonPhase.Ended && _config.ExpiryPolicy == SeasonExpiryPolicy.AutoGrant)
            {
                await GrantBacklogAsync(cToken);
            }

            await RetryFailedClaimsAsync(cToken);

            if (_config.AutoClaim) await GrantBacklogAsync(cToken);

            await PersistAsync(false, cToken);

            RaiseChanged();
        }

        private DateTime UtcNow => SeasonPassTime.FromUnix(EffectiveUnixNow);

        private long EffectiveUnixNow => _saved == null
            ? _clock.UnixTimestampNow
            : _saved.AdvanceSeen(_clock.UnixTimestampNow);

        /// <summary>
        /// Indicates whether the loaded save actually belongs to the selected season.
        /// </summary>
        /// <remarks>
        /// False in the window where a not-yet-started season is being displayed while the
        /// save still holds the previous one. Reading the old XP against the new ladder would
        /// show a tier the player has not earned on a season that has not begun.
        /// </remarks>
        private bool IsSaveForSelectedSeason => _season != null && _saved != null &&
                                                string.Equals(_saved.SeasonId, _season.Id,
                                                    StringComparison.Ordinal);

        private int SeasonXp => IsSaveForSelectedSeason ? _saved.TotalXp : 0;

        private int CurrentTier => _season == null
            ? 0
            : SeasonPassCalculator.GetTier(_season, SeasonXp);

        private bool CanEarn => _phase is SeasonPhase.Active or SeasonPhase.EndingSoon;

        private bool CanClaim => _phase is SeasonPhase.Active or SeasonPhase.EndingSoon or SeasonPhase.Grace;

        private SeasonPassData SelectSeason()
        {
            var forcedId = _config.ForcedSeasonId;

            if (!string.IsNullOrWhiteSpace(forcedId))
            {
                return _content.TryGetData<SeasonPassData>(forcedId, out var forced) ? forced : null;
            }

            var now = UtcNow;
            SeasonPassData active = null;
            SeasonPassData mostRecentPast = null;
            SeasonPassData nextUpcoming = null;

            foreach (var candidate in _content.GetAllData<SeasonPassData>())
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.Id)) continue;

                if (now < candidate.StartUtc)
                {
                    // Earliest future season, so a pass announced ahead of time can be shown.
                    if (nextUpcoming == null || candidate.StartUtc < nextUpcoming.StartUtc)
                    {
                        nextUpcoming = candidate;
                    }

                    continue;
                }

                if (now < candidate.GraceEndUtc)
                {
                    // Overlapping definitions are a content bug; the later start wins so a
                    // corrected season replaces the one it supersedes rather than fighting it.
                    if (active == null || candidate.StartUtc > active.StartUtc) active = candidate;

                    continue;
                }

                if (mostRecentPast == null || candidate.StartUtc > mostRecentPast.StartUtc)
                {
                    mostRecentPast = candidate;
                }
            }

            // A finished season is still selected when nothing has replaced it, so the screen
            // can show a final state instead of blanking out.
            return active ?? mostRecentPast ?? nextUpcoming;
        }

        private async UniTask RollOverAsync(SeasonPassData incoming, CancellationToken cToken)
        {
            var previousId = _saved.SeasonId;
            var forfeited = 0;

            if (!string.IsNullOrEmpty(previousId))
            {
                // Resolve the outgoing definition so expiry can pay out against the ladder the
                // player actually earned on, not the incoming one.
                if (_content.TryGetData<SeasonPassData>(previousId, out var previous))
                {
                    _season = previous;
                    _phase = SeasonPhase.Grace;

                    if (_config.ExpiryPolicy == SeasonExpiryPolicy.AutoGrant)
                    {
                        await GrantBacklogAsync(cToken);
                    }

                    forfeited = CountUnclaimed();
                }
                else
                {
                    UniStatics.LogWarning(
                        $"Season '{previousId}' rolled over but its definition is no longer " +
                        "loaded, so unclaimed rewards could not be paid out.", this);
                }
            }

            var archive = string.IsNullOrEmpty(previousId)
                ? null
                : new SeasonArchiveEntry(previousId, CurrentTier, _saved.TotalXp,
                    _saved.HighestOwnedTrack, _saved.ClaimedKeys.Count, forfeited, EffectiveUnixNow);

            _saved.BeginSeason(incoming.Id, archive, _config.MaxArchiveEntries);

            _season = incoming;
            _hasRaisedEndingSoon = false;

            var problems = incoming.DescribeProblems();

            if (!string.IsNullOrEmpty(problems))
            {
                UniStatics.LogWarning($"Season '{incoming.Id}' has content problems: {problems}.", this);
            }

            // Skips bought past the end of the last ladder were paid for, so they carry over.
            var banked = _saved.TakeBankedTierSkips();

            if (banked > 0) ApplyTierSkips(banked);

            UpdatePhase();

            Raise(new SeasonChanged(previousId, incoming.Id, forfeited));

            await PersistAsync(true, cToken);
        }

        private void UpdatePhase()
        {
            if (_season == null)
            {
                _phase = SeasonPhase.None;
                return;
            }

            var now = UtcNow;

            if (now < _season.StartUtc)
            {
                _phase = SeasonPhase.NotStarted;
                return;
            }

            if (now < _season.EndingSoonUtc)
            {
                _phase = SeasonPhase.Active;
                return;
            }

            if (now < _season.EndUtc)
            {
                _phase = SeasonPhase.EndingSoon;

                if (!_hasRaisedEndingSoon)
                {
                    _hasRaisedEndingSoon = true;

                    Raise(new SeasonEndingSoon(_season.Id, (_season.EndUtc - now).TotalHours,
                        CountUnclaimed()));
                }

                return;
            }

            var inGrace = _config.ExpiryPolicy == SeasonExpiryPolicy.GraceWindow &&
                          now < _season.GraceEndUtc;

            _phase = inGrace ? SeasonPhase.Grace : SeasonPhase.Ended;
        }

        private void RollDailyWindow()
        {
            if (_saved == null) return;

            var dayStart = SeasonPassTime.StartOfUtcDay(EffectiveUnixNow);

            // Strictly greater, never merely different: the clock is a high-water mark, so a
            // day boundary can only ever move forward.
            if (dayStart > _saved.DailyWindowStartUnix) _saved.ResetDailyXp(dayStart);
        }

        private void RollQuestWindows()
        {
            if (_saved == null || _season == null) return;

            var now = EffectiveUnixNow;
            var dayStart = SeasonPassTime.StartOfUtcDay(now);
            var weekStart = SeasonPassTime.StartOfUtcWeek(now);

            if (dayStart > _saved.DailyQuestWindowStartUnix)
            {
                _saved.ResetQuests(CollectQuestIds(SeasonQuestScope.Daily), SeasonQuestScope.Daily, dayStart);
            }

            if (weekStart > _saved.WeeklyQuestWindowStartUnix)
            {
                _saved.ResetQuests(CollectQuestIds(SeasonQuestScope.Weekly), SeasonQuestScope.Weekly, weekStart);
            }
        }

        private IReadOnlyList<string> CollectQuestIds(SeasonQuestScope scope)
        {
            _questIdBuffer.Clear();

            foreach (var quest in _season.Quests)
            {
                if (quest != null && quest.Scope == scope) _questIdBuffer.Add(quest.QuestId);
            }

            return _questIdBuffer;
        }

        private async UniTask SyncAsync(CancellationToken cToken)
        {
            var remote = await _backend.SyncAsync(_saved, cToken);

            if (remote != null) SeasonPassReconciler.Reconcile(_saved, remote);

            // Cleared whether or not the backend returned a record: it was handed the queue and
            // is the only thing that can act on it. Keeping them would replay forever.
            _saved.ClearPendingGrants();
        }

        private async UniTask ApplyXpAsync(int amount, string sourceId, string grantId,
            CancellationToken cToken)
        {
            if (amount <= 0) return;

            // Checked at the mutation point rather than only where an await happens: a backend
            // that completes synchronously would otherwise let a cancelled call change state.
            cToken.ThrowIfCancellationRequested();

            var tierBefore = CurrentTier;

            _saved.AddXp(amount);
            _saved.RecordGrantId(grantId);

            // Only queue when there is something to replay to. A local backend is always
            // reachable, so a single-player game never pays for a queue it will never drain.
            if (!_backend.IsOnline)
            {
                _saved.QueuePendingGrant(new SeasonPendingGrant(
                    grantId ?? $"{sourceId}:{EffectiveUnixNow}:{_saved.TotalXp}",
                    sourceId, amount, EffectiveUnixNow));
            }

            Raise(new SeasonXpGranted(_season.Id, sourceId, amount, _saved.TotalXp));

            RaiseTierUnlocks(tierBefore, CurrentTier);

            if (_config.AutoClaim) await GrantBacklogAsync(cToken);

            await PersistAsync(false, cToken);

            RaiseChanged();
        }

        private void ApplyTierSkips(int count)
        {
            var tierBefore = CurrentTier;

            for (var index = 0; index < count; index++)
            {
                var needed = SeasonPassCalculator.GetXpToNextTier(_season, _saved.TotalXp);

                if (needed <= 0)
                {
                    // The ladder is finished and bonus tiers are off, so the remaining skips
                    // have nothing to buy this season. Banking them keeps what was paid for.
                    _saved.BankTierSkips(count - index);
                    break;
                }

                _saved.AddXp(needed);
            }

            RaiseTierUnlocks(tierBefore, CurrentTier);
        }

        private void RaiseTierUnlocks(int from, int to)
        {
            if (to <= from) return;

            var maxTier = _season.MaxTier;

            for (var tier = from + 1; tier <= to; tier++)
            {
                Raise(new SeasonTierUnlocked(_season.Id, tier, tier > maxTier));
            }
        }

        private async UniTask GrantBacklogIfAutoClaimAsync(CancellationToken cToken)
        {
            if (_config.AutoClaim) await GrantBacklogAsync(cToken);
        }

        private async UniTask GrantBacklogAsync(CancellationToken cToken,
            SeasonTrack? minTrack = null)
        {
            if (_season == null) return;

            SeasonPassCalculator.CollectUnlockedRewards(_season, CurrentTier, _workBuffer);

            var pending = _workBuffer.ToArray();

            foreach (var reward in pending)
            {
                if (minTrack.HasValue && reward.Track < minTrack.Value) continue;
                if (!_saved.Owns(reward.Track)) continue;
                if (_saved.HasClaimed(reward.ToClaimKey())) continue;

                var data = FindReward(reward);

                if (data == null) continue;

                await DeliverAsync(data, reward, true, cToken);
            }
        }

        private async UniTask<int> RetryFailedClaimsAsync(CancellationToken cToken)
        {
            if (_saved.PendingClaimKeys.Count == 0) return 0;

            var keys = new string[_saved.PendingClaimKeys.Count];

            for (var index = 0; index < keys.Length; index++)
            {
                keys[index] = _saved.PendingClaimKeys[index];
            }

            var delivered = 0;

            foreach (var key in keys)
            {
                if (!SeasonRewardRef.TryParseClaimKey(_season?.Id, key, out var reference)) continue;

                var data = FindReward(reference);

                if (data == null) continue;

                if (await DeliverAsync(data, reference, true, cToken)) delivered++;
            }

            return delivered;
        }

        private async UniTask<bool> DeliverAsync(SeasonRewardData data, SeasonRewardRef reference,
            bool isAutomatic, CancellationToken cToken)
        {
            bool granted;

            try
            {
                granted = await _granter.GrantAsync(data, reference, cToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // A granter that throws is a bug in the game's economy code, not a reason to
                // mark the reward collected. Queue it and let the next refresh try again.
                UniStatics.LogException(exception, this);
                granted = false;
            }

            if (!granted)
            {
                _saved.QueueFailedClaim(reference.ToClaimKey());
                Raise(new SeasonRewardGrantFailed(reference));

                return false;
            }

            _saved.RecordClaim(reference.ToClaimKey());
            Raise(new SeasonRewardClaimed(reference, isAutomatic));

            return true;
        }

        private SeasonRewardData FindReward(SeasonRewardRef reference)
        {
            if (_season == null) return null;

            foreach (var reward in _season.GetRewards(reference.Tier))
            {
                if (reward == null || !reward.IsValid) continue;

                if (reward.Track == reference.Track &&
                    string.Equals(reward.RewardId, reference.RewardId, StringComparison.Ordinal))
                {
                    return reward;
                }
            }

            return null;
        }

        private int CountUnclaimed()
        {
            if (_season == null) return 0;

            SeasonPassCalculator.CollectUnlockedRewards(_season, CurrentTier, _workBuffer);

            var count = 0;

            foreach (var reward in _workBuffer)
            {
                if (!_saved.Owns(reward.Track)) continue;
                if (_saved.HasClaimed(reward.ToClaimKey())) continue;

                count++;
            }

            return count;
        }

        private UniTask PersistAsync(bool isCheckpoint, CancellationToken cToken) =>
            _backend.SaveAsync(_saved, isCheckpoint && _config.FlushOnCheckpoint, cToken);

        private SeasonPassSnapshot BuildSnapshot()
        {
            if (!IsReady || _season == null)
            {
                return new SeasonPassSnapshot(null, SeasonPhase.None, default, SeasonTrack.Free, 0,
                    TimeSpan.Zero, _saved?.BankedTierSkips ?? 0);
            }

            var remaining = _season.EndUtc - UtcNow;

            return new SeasonPassSnapshot(_season.Id, _phase,
                SeasonPassCalculator.GetProgress(_season, SeasonXp),
                IsSaveForSelectedSeason ? _saved.HighestOwnedTrack : SeasonTrack.Free,
                CountUnclaimed(), remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
                _saved.BankedTierSkips);
        }

        private void RaiseChanged() => OnChanged.SafeInvoke(BuildSnapshot());

        private static void Raise<TEvent>(TEvent @event)
            where TEvent : struct, IEvent
        {
            // The bus is optional: a game that never bootstrapped UniEvents still gets a
            // working pass through OnChanged and the awaited results.
            if (UniEvents.IsInitialized) UniEvents.Raise(@event);
        }
    }
}
