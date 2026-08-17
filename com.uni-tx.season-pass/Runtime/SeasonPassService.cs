using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Core;
using UniTx.Currency;
using UniTx.Entity;
using UniTx.Events;
using UniTx.IoC;
using UniTx.Rewards;

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
    /// Static and saved data live in a <see cref="SeasonPassEntity"/> — the same entity
    /// foundation every kit system builds on. The entity's save key is stable while its
    /// content key (the season id) changes on rollover, and persistence routes through
    /// <see cref="ISeasonPassBackend"/> so a server can take authority later.
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

        private SeasonPassEntity _entity;
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
        public SeasonPassData Season => _entity?.Data;

        /// <inheritdoc />
        public SeasonPhase Phase => _phase;

        /// <inheritdoc />
        public SeasonPassSnapshot Snapshot => BuildSnapshot();

        /// <inheritdoc />
        public event Action<SeasonPassSnapshot> OnChanged;

        /// <summary>
        /// Gets the player's persisted progress, or null before initialization.
        /// </summary>
        public SeasonPassSavedData SavedData => _entity?.SavedData;

        /// <summary>
        /// Gets the player's persisted progress, or null before initialization.
        /// </summary>
        private SeasonPassSavedData Saved => _entity?.SavedData;

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
            if (_granter == null && resolver.TryResolve<ISeasonPassRewardGranter>(out var granter))
            {
                _granter = granter;
            }

            // Defaults on top of the entity foundation: rewards route through the kit's
            // reward service, currency purchases through the kit's currency service.
            if (_granter == null && resolver.TryResolve<IRewardService>(out var rewards))
            {
                _granter = new SeasonPassRewardGranter(rewards);
            }

            if (_wallet == null && resolver.TryResolve<ISeasonPassWallet>(out var wallet))
            {
                _wallet = wallet;
            }

            if (_wallet == null && resolver.TryResolve<ICurrencyService>(out var currency))
            {
                _wallet = new SeasonPassCurrencyWallet(currency);
            }
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
            _phase = SeasonPhase.None;
            _hasRaisedEndingSoon = false;

            _claimBuffer.Clear();
            _workBuffer.Clear();
            _questIdBuffer.Clear();

            _entity?.Reset();
            _entity = null;
        }

        /// <inheritdoc />
        public void SetRewardGranter(ISeasonPassRewardGranter granter) =>
            _granter = granter ?? throw new ArgumentNullException(nameof(granter));

        /// <inheritdoc />
        public void SetWallet(ISeasonPassWallet wallet) =>
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));

        /// <inheritdoc />
        public bool OwnsTrack(SeasonTrack track) => Saved != null && Saved.Owns(track);

        /// <inheritdoc />
        public bool IsClaimable(SeasonRewardRef reward)
        {
            if (!IsReady || Season == null || !CanClaim) return false;
            if (Saved.HasClaimed(reward.ToClaimKey())) return false;
            if (!Saved.Owns(reward.Track)) return false;
            if (reward.Tier > CurrentTier) return false;

            return FindReward(reward) != null;
        }

        /// <inheritdoc />
        public int GetClaimable(List<SeasonRewardRef> buffer)
        {
            if (buffer == null) return 0;

            buffer.Clear();

            if (!IsReady || Season == null || !CanClaim) return 0;

            SeasonPassCalculator.CollectUnlockedRewards(Season, CurrentTier, _workBuffer);

            foreach (var reward in _workBuffer)
            {
                if (!Saved.Owns(reward.Track)) continue;
                if (Saved.HasClaimed(reward.ToClaimKey())) continue;

                buffer.Add(reward);
            }

            return buffer.Count;
        }

        /// <inheritdoc />
        public async UniTask<XpGrantResult> GrantXpAsync(string sourceId, int amount = 0,
            string grantId = null, CancellationToken cToken = default)
        {
            if (!IsReady || Season == null) return XpGrantResult.Rejected;

            if (!Season.TryGetXpSource(sourceId, out var source)) return XpGrantResult.UnknownSource;

            if (!CanEarn) return XpGrantResult.SeasonInactive;

            if (source.RequiresPaidTrack && !Saved.Owns(SeasonTrack.Premium))
            {
                return XpGrantResult.TrackNotOwned;
            }

            var requested = amount > 0 ? amount : source.XpPerEvent;

            if (requested <= 0) return XpGrantResult.Rejected;

            if (!string.IsNullOrEmpty(grantId) && Saved.HasAppliedGrant(grantId))
            {
                return XpGrantResult.Duplicate;
            }

            if (!_config.AllowOfflineGrants && !_backend.IsOnline) return XpGrantResult.Offline;

            RollDailyWindow();

            var granted = requested;
            SeasonSourceDailyXp daily = null;

            if (source.DailyCap > 0)
            {
                daily = Saved.GetOrCreateDailyXp(source.SourceId);
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
            if (!IsReady || Season == null) return ClaimResult.NoSeason;
            if (!CanClaim) return ClaimResult.SeasonExpired;

            var data = FindReward(reward);

            if (data == null) return ClaimResult.NothingToClaim;
            if (Saved.HasClaimed(reward.ToClaimKey())) return ClaimResult.AlreadyClaimed;
            if (reward.Tier > CurrentTier) return ClaimResult.TierNotReached;
            if (!Saved.Owns(reward.Track)) return ClaimResult.TrackNotOwned;

            var delivered = await DeliverAsync(data, reward, false, cToken);

            if (delivered) await PersistAsync(true, cToken);

            RaiseChanged();

            return delivered ? ClaimResult.Claimed : ClaimResult.GrantFailed;
        }

        /// <inheritdoc />
        public async UniTask<int> ClaimTierAsync(int tier, SeasonTrack track,
            CancellationToken cToken = default)
        {
            if (!IsReady || Season == null || !CanClaim) return 0;
            if (tier > CurrentTier || !Saved.Owns(track)) return 0;

            var claimed = 0;

            foreach (var reward in Season.GetRewards(tier))
            {
                if (reward == null || !reward.IsValid || reward.Track != track) continue;

                var reference = new SeasonRewardRef(Season.Id, tier, track, reward.RewardId);

                if (Saved.HasClaimed(reference.ToClaimKey())) continue;

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
            if (!IsReady || Season == null || !CanClaim) return 0;

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
            if (!IsReady || Season == null || track == SeasonTrack.Free)
            {
                return TrackUnlockResult.Rejected;
            }

            if (Saved.Owns(track)) return TrackUnlockResult.AlreadyOwned;
            if (!CanEarn) return TrackUnlockResult.SeasonInactive;

            var offer = Season.GetOffer(track);

            if (offer == null) return TrackUnlockResult.NotPurchasable;

            if (payment == SeasonPassPayment.Currency)
            {
                if (!offer.SellsForCurrency) return TrackUnlockResult.NotPurchasable;

                if (!_wallet.TrySpend(offer.CurrencyId, offer.CurrencyCost))
                {
                    return TrackUnlockResult.InsufficientFunds;
                }
            }

            var previouslyOwned = Saved.HighestOwnedTrack;

            Saved.GrantTrack(track);

            Raise(new SeasonTrackUnlocked(Season.Id, track, payment));

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
            if (!IsReady || Season == null || count <= 0 || !CanEarn) return 0;

            if (Season.MaxTierSkipPurchases > 0)
            {
                var remaining = Season.MaxTierSkipPurchases - Saved.PurchasedTierSkips;

                count = Math.Min(count, Math.Max(0, remaining));

                if (count == 0) return 0;
            }

            if (payment == SeasonPassPayment.Currency)
            {
                if (!Season.SellsTierSkipsForCurrency) return 0;

                if (!_wallet.TrySpend(Season.TierSkipCurrencyId, Season.TierSkipCurrencyCost * count))
                {
                    return 0;
                }
            }

            Saved.RecordTierSkipPurchase(count);

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
            if (!IsReady || Season == null || amount <= 0) return QuestProgressResult.Rejected;
            if (!Season.TryGetQuest(questId, out var quest)) return QuestProgressResult.UnknownQuest;
            if (!CanEarn) return QuestProgressResult.Rejected;

            if (!quest.IsAvailableAt(UtcNow)) return QuestProgressResult.Unavailable;

            if (quest.RequiresPaidTrack && !Saved.Owns(SeasonTrack.Premium))
            {
                return QuestProgressResult.Unavailable;
            }

            var progress = Saved.GetOrCreateQuest(questId);

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
            await ApplyXpAsync(quest.XpReward, QuestXpSource, $"quest:{Season.Id}:{questId}", cToken);

            Raise(new SeasonQuestCompleted(Season.Id, questId, quest.XpReward));

            await PersistAsync(true, cToken);

            return QuestProgressResult.Completed;
        }

        /// <inheritdoc />
        public async UniTask RefreshAsync(CancellationToken cToken = default)
        {
            if (!IsReady) return;

            Saved.AdvanceSeen(_clock.UnixTimestampNow);

            var selected = SelectSeason();

            if (selected != null &&
                !string.Equals(selected.Id, Saved.SeasonId, StringComparison.Ordinal) &&
                UtcNow >= selected.StartUtc)
            {
                await RollOverAsync(selected, cToken);
            }
            else
            {
                // A season that has been announced but has not begun is shown, never rolled
                // into. Wiping the save the moment a teaser appears costs the player their
                // standing early and buys nothing — the rollover happens when it starts.
                SetSeason(selected);
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

        private long EffectiveUnixNow => Saved == null
            ? _clock.UnixTimestampNow
            : Saved.AdvanceSeen(_clock.UnixTimestampNow);

        /// <summary>
        /// Indicates whether the loaded save actually belongs to the selected season.
        /// </summary>
        /// <remarks>
        /// False in the window where a not-yet-started season is being displayed while the
        /// save still holds the previous one. Reading the old XP against the new ladder would
        /// show a tier the player has not earned on a season that has not begun.
        /// </remarks>
        private bool IsSaveForSelectedSeason => Season != null && Saved != null &&
                                                string.Equals(Saved.SeasonId, Season.Id,
                                                    StringComparison.Ordinal);

        private int SeasonXp => IsSaveForSelectedSeason ? Saved.TotalXp : 0;

        private int CurrentTier => Season == null
            ? 0
            : SeasonPassCalculator.GetTier(Season, SeasonXp);

        private bool CanEarn => _phase is SeasonPhase.Active or SeasonPhase.EndingSoon;

        private bool CanClaim => _phase is SeasonPhase.Active or SeasonPhase.EndingSoon or SeasonPhase.Grace;

        /// <summary>
        /// Points the entity's content key at a season and reloads its static data.
        /// </summary>
        /// <param name="season">The season to show, or null for none.</param>
        private void SetSeason(SeasonPassData season)
        {
            _entity.SetDataId(season?.Id);
            _entity.ReloadData();
        }

        private void EnsureEntity()
        {
            if (_entity != null) return;

            _entity = new SeasonPassEntity(
                _config?.SaveId ?? SeasonPassSavedData.DefaultSaveId, _backend, _content);
        }

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
            var previousId = Saved.SeasonId;
            var forfeited = 0;

            if (!string.IsNullOrEmpty(previousId))
            {
                // Resolve the outgoing definition so expiry can pay out against the ladder the
                // player actually earned on, not the incoming one.
                if (_content.TryGetData<SeasonPassData>(previousId, out var previous))
                {
                    SetSeason(previous);
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
                : new SeasonArchiveEntry(previousId, CurrentTier, Saved.TotalXp,
                    Saved.HighestOwnedTrack, Saved.ClaimedKeys.Count, forfeited, EffectiveUnixNow);

            Saved.BeginSeason(incoming.Id, archive, _config.MaxArchiveEntries);

            SetSeason(incoming);
            _hasRaisedEndingSoon = false;

            var problems = incoming.DescribeProblems();

            if (!string.IsNullOrEmpty(problems))
            {
                UniStatics.LogWarning($"Season '{incoming.Id}' has content problems: {problems}.", this);
            }

            // Skips bought past the end of the last ladder were paid for, so they carry over.
            var banked = Saved.TakeBankedTierSkips();

            if (banked > 0) ApplyTierSkips(banked);

            UpdatePhase();

            Raise(new SeasonChanged(previousId, incoming.Id, forfeited));

            await PersistAsync(true, cToken);
        }

        private void UpdatePhase()
        {
            if (Season == null)
            {
                _phase = SeasonPhase.None;
                return;
            }

            var now = UtcNow;

            if (now < Season.StartUtc)
            {
                _phase = SeasonPhase.NotStarted;
                return;
            }

            if (now < Season.EndingSoonUtc)
            {
                _phase = SeasonPhase.Active;
                return;
            }

            if (now < Season.EndUtc)
            {
                _phase = SeasonPhase.EndingSoon;

                if (!_hasRaisedEndingSoon)
                {
                    _hasRaisedEndingSoon = true;

                    Raise(new SeasonEndingSoon(Season.Id, (Season.EndUtc - now).TotalHours,
                        CountUnclaimed()));
                }

                return;
            }

            var inGrace = _config.ExpiryPolicy == SeasonExpiryPolicy.GraceWindow &&
                          now < Season.GraceEndUtc;

            _phase = inGrace ? SeasonPhase.Grace : SeasonPhase.Ended;
        }

        private void RollDailyWindow()
        {
            if (Saved == null) return;

            var dayStart = SeasonPassTime.StartOfUtcDay(EffectiveUnixNow);

            // Strictly greater, never merely different: the clock is a high-water mark, so a
            // day boundary can only ever move forward.
            if (dayStart > Saved.DailyWindowStartUnix) Saved.ResetDailyXp(dayStart);
        }

        private void RollQuestWindows()
        {
            if (Saved == null || Season == null) return;

            var now = EffectiveUnixNow;
            var dayStart = SeasonPassTime.StartOfUtcDay(now);
            var weekStart = SeasonPassTime.StartOfUtcWeek(now);

            if (dayStart > Saved.DailyQuestWindowStartUnix)
            {
                Saved.ResetQuests(CollectQuestIds(SeasonQuestScope.Daily), SeasonQuestScope.Daily, dayStart);
            }

            if (weekStart > Saved.WeeklyQuestWindowStartUnix)
            {
                Saved.ResetQuests(CollectQuestIds(SeasonQuestScope.Weekly), SeasonQuestScope.Weekly, weekStart);
            }
        }

        private IReadOnlyList<string> CollectQuestIds(SeasonQuestScope scope)
        {
            _questIdBuffer.Clear();

            foreach (var quest in Season.Quests)
            {
                if (quest != null && quest.Scope == scope) _questIdBuffer.Add(quest.QuestId);
            }

            return _questIdBuffer;
        }

        private async UniTask SyncAsync(CancellationToken cToken)
        {
            var remote = await _backend.SyncAsync(Saved, cToken);

            if (remote != null) SeasonPassReconciler.Reconcile(Saved, remote);

            // Cleared whether or not the backend returned a record: it was handed the queue and
            // is the only thing that can act on it. Keeping them would replay forever.
            Saved.ClearPendingGrants();
        }

        private async UniTask ApplyXpAsync(int amount, string sourceId, string grantId,
            CancellationToken cToken)
        {
            if (amount <= 0) return;

            // Checked at the mutation point rather than only where an await happens: a backend
            // that completes synchronously would otherwise let a cancelled call change state.
            cToken.ThrowIfCancellationRequested();

            var tierBefore = CurrentTier;

            Saved.AddXp(amount);
            Saved.RecordGrantId(grantId);

            // Only queue when there is something to replay to. A local backend is always
            // reachable, so a single-player game never pays for a queue it will never drain.
            if (!_backend.IsOnline)
            {
                Saved.QueuePendingGrant(new SeasonPendingGrant(
                    grantId ?? $"{sourceId}:{EffectiveUnixNow}:{Saved.TotalXp}",
                    sourceId, amount, EffectiveUnixNow));
            }

            Raise(new SeasonXpGranted(Season.Id, sourceId, amount, Saved.TotalXp));

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
                var needed = SeasonPassCalculator.GetXpToNextTier(Season, Saved.TotalXp);

                if (needed <= 0)
                {
                    // The ladder is finished and bonus tiers are off, so the remaining skips
                    // have nothing to buy this season. Banking them keeps what was paid for.
                    Saved.BankTierSkips(count - index);
                    break;
                }

                Saved.AddXp(needed);
            }

            RaiseTierUnlocks(tierBefore, CurrentTier);
        }

        private void RaiseTierUnlocks(int from, int to)
        {
            if (to <= from) return;

            var maxTier = Season.MaxTier;

            for (var tier = from + 1; tier <= to; tier++)
            {
                Raise(new SeasonTierUnlocked(Season.Id, tier, tier > maxTier));
            }
        }

        private async UniTask GrantBacklogIfAutoClaimAsync(CancellationToken cToken)
        {
            if (_config.AutoClaim) await GrantBacklogAsync(cToken);
        }

        private async UniTask GrantBacklogAsync(CancellationToken cToken,
            SeasonTrack? minTrack = null)
        {
            if (Season == null) return;

            SeasonPassCalculator.CollectUnlockedRewards(Season, CurrentTier, _workBuffer);

            var pending = _workBuffer.ToArray();

            foreach (var reward in pending)
            {
                if (minTrack.HasValue && reward.Track < minTrack.Value) continue;
                if (!Saved.Owns(reward.Track)) continue;
                if (Saved.HasClaimed(reward.ToClaimKey())) continue;

                var data = FindReward(reward);

                if (data == null) continue;

                await DeliverAsync(data, reward, true, cToken);
            }
        }

        private async UniTask<int> RetryFailedClaimsAsync(CancellationToken cToken)
        {
            if (Saved.PendingClaimKeys.Count == 0) return 0;

            var keys = new string[Saved.PendingClaimKeys.Count];

            for (var index = 0; index < keys.Length; index++)
            {
                keys[index] = Saved.PendingClaimKeys[index];
            }

            var delivered = 0;

            foreach (var key in keys)
            {
                if (!SeasonRewardRef.TryParseClaimKey(Season?.Id, key, out var reference)) continue;

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
                Saved.QueueFailedClaim(reference.ToClaimKey());
                Raise(new SeasonRewardGrantFailed(reference));

                return false;
            }

            Saved.RecordClaim(reference.ToClaimKey());
            Raise(new SeasonRewardClaimed(reference, isAutomatic));

            return true;
        }

        private SeasonRewardData FindReward(SeasonRewardRef reference)
        {
            if (Season == null) return null;

            foreach (var reward in Season.GetRewards(reference.Tier))
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
            if (Season == null) return 0;

            SeasonPassCalculator.CollectUnlockedRewards(Season, CurrentTier, _workBuffer);

            var count = 0;

            foreach (var reward in _workBuffer)
            {
                if (!Saved.Owns(reward.Track)) continue;
                if (Saved.HasClaimed(reward.ToClaimKey())) continue;

                count++;
            }

            return count;
        }

        private UniTask PersistAsync(bool isCheckpoint, CancellationToken cToken) =>
            _entity.SaveAsync(isCheckpoint && _config.FlushOnCheckpoint, cToken);

        private SeasonPassSnapshot BuildSnapshot()
        {
            if (!IsReady || Season == null)
            {
                return new SeasonPassSnapshot(null, SeasonPhase.None, default, SeasonTrack.Free, 0,
                    TimeSpan.Zero, Saved?.BankedTierSkips ?? 0);
            }

            var remaining = Season.EndUtc - UtcNow;

            return new SeasonPassSnapshot(Season.Id, _phase,
                SeasonPassCalculator.GetProgress(Season, SeasonXp),
                IsSaveForSelectedSeason ? Saved.HighestOwnedTrack : SeasonTrack.Free,
                CountUnclaimed(), remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
                Saved.BankedTierSkips);
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
