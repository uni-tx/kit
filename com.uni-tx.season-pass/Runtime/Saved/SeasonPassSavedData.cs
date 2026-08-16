using System;
using System.Collections.Generic;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// Everything the season pass persists for one player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stored under a single save id that never changes, while the season id inside it does.
    /// That is deliberate: a save keyed by season would multiply forever and lose the one
    /// thing rollover has to carry across, which is what the player already owns.
    /// </para>
    /// <para>
    /// Two lifecycles live here and must not be confused. <b>Ephemeral</b> — season id, XP,
    /// claimed keys, owned tracks, quest progress — is wiped by <see cref="BeginSeason"/>.
    /// <b>Durable</b> — banked tier skips and the archive — survives it. Granted rewards
    /// themselves are not stored here at all; they belong to the game's own inventory, which
    /// is what stops a rollover from deleting them.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class SeasonPassSavedData : ISavedData
    {
        /// <summary>
        /// The save id the service reads and writes under.
        /// </summary>
        public const string DefaultSaveId = "season_pass";

        /// <summary>
        /// Bump when the shape of this type changes, then handle it in <see cref="Migrate"/>.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// How many recent grant ids are remembered for duplicate detection.
        /// </summary>
        /// <remarks>
        /// Bounded on purpose. An unbounded ledger grows with every match a player ever
        /// plays, and a save file that grows without limit eventually costs more to write
        /// than the feature is worth. A few hundred entries covers any plausible retry or
        /// replay window; older ids fall off the front.
        /// </remarks>
        public const int MaxTrackedGrantIds = 256;

        [SerializeField] private string _id;
        [SerializeField] private long _modifiedTimestamp;
        [SerializeField] private int _version = CurrentVersion;

        [SerializeField] private string _seasonId;
        [SerializeField] private int _totalXp;
        [SerializeField] private int _highestOwnedTrack;
        [SerializeField] private int _purchasedTierSkips;
        [SerializeField] private int _bankedTierSkips;

        [SerializeField] private List<string> _claimedKeys = new();
        [SerializeField] private List<string> _pendingClaimKeys = new();
        [SerializeField] private List<string> _appliedGrantIds = new();
        [SerializeField] private List<SeasonPendingGrant> _pendingGrants = new();
        [SerializeField] private List<SeasonQuestProgress> _questProgress = new();
        [SerializeField] private List<SeasonSourceDailyXp> _dailyXp = new();
        [SerializeField] private List<SeasonArchiveEntry> _archive = new();

        [SerializeField] private long _dailyWindowStartUnix;
        [SerializeField] private long _dailyQuestWindowStartUnix;
        [SerializeField] private long _weeklyQuestWindowStartUnix;
        [SerializeField] private long _lastSeenUnix;

        /// <inheritdoc />
        public string Id
        {
            get => _id;
            set => _id = value;
        }

        /// <inheritdoc />
        public long ModifiedTimestamp
        {
            get => _modifiedTimestamp;
            set => _modifiedTimestamp = value;
        }

        /// <summary>
        /// Gets the schema version this instance was written with.
        /// </summary>
        public int Version => _version;

        /// <summary>
        /// Gets the season this progress belongs to.
        /// </summary>
        public string SeasonId => _seasonId;

        /// <summary>
        /// Gets total season XP earned. Only ever increases within a season.
        /// </summary>
        public int TotalXp => _totalXp;

        /// <summary>
        /// Gets the highest track the player owns this season.
        /// </summary>
        public SeasonTrack HighestOwnedTrack => (SeasonTrack)_highestOwnedTrack;

        /// <summary>
        /// Gets how many tier skips were bought this season.
        /// </summary>
        public int PurchasedTierSkips => _purchasedTierSkips;

        /// <summary>
        /// Gets tier skips bought past the final tier, waiting for the next season.
        /// </summary>
        public int BankedTierSkips => _bankedTierSkips;

        /// <summary>
        /// Gets the claim keys already collected this season.
        /// </summary>
        public IReadOnlyList<string> ClaimedKeys => _claimedKeys;

        /// <summary>
        /// Gets claims whose delivery failed and which are retried on the next refresh.
        /// </summary>
        public IReadOnlyList<string> PendingClaimKeys => _pendingClaimKeys;

        /// <summary>
        /// Gets XP grants applied locally but not yet acknowledged by a backend.
        /// </summary>
        public IReadOnlyList<SeasonPendingGrant> PendingGrants => _pendingGrants;

        /// <summary>
        /// Gets per-quest progress for the current windows.
        /// </summary>
        public IReadOnlyList<SeasonQuestProgress> QuestProgress => _questProgress;

        /// <summary>
        /// Gets finished seasons, oldest first.
        /// </summary>
        public IReadOnlyList<SeasonArchiveEntry> Archive => _archive;

        /// <summary>
        /// Gets the Unix timestamp the daily XP window started at.
        /// </summary>
        public long DailyWindowStartUnix => _dailyWindowStartUnix;

        /// <summary>
        /// Gets the Unix timestamp the daily quest window started at.
        /// </summary>
        public long DailyQuestWindowStartUnix => _dailyQuestWindowStartUnix;

        /// <summary>
        /// Gets the Unix timestamp the weekly quest window started at.
        /// </summary>
        public long WeeklyQuestWindowStartUnix => _weeklyQuestWindowStartUnix;

        /// <summary>
        /// Gets the furthest point in time this save has ever seen.
        /// </summary>
        /// <remarks>
        /// A high-water mark, not a last-write time. Time is only ever allowed to move
        /// forward from here, so winding the device clock back cannot reopen a season that
        /// already ended or refill a daily cap. Winding it <i>forward</i> is the other half
        /// of the problem and cannot be solved on the device — bind
        /// <c>ServerClock</c> for that.
        /// </remarks>
        public long LastSeenUnix => _lastSeenUnix;

        /// <summary>
        /// Indicates whether the player owns the given track.
        /// </summary>
        /// <param name="track">The track to test.</param>
        /// <remarks>
        /// Tracks are ordered, so owning a higher one implies every track below it.
        /// </remarks>
        public bool Owns(SeasonTrack track) => _highestOwnedTrack >= (int)track;

        /// <summary>
        /// Indicates whether a reward has already been collected.
        /// </summary>
        /// <param name="claimKey">The key from <see cref="SeasonRewardRef.ToClaimKey"/>.</param>
        public bool HasClaimed(string claimKey) => _claimedKeys.Contains(claimKey);

        /// <summary>
        /// Indicates whether a grant id has already been applied.
        /// </summary>
        /// <param name="grantId">The idempotency id.</param>
        public bool HasAppliedGrant(string grantId) =>
            !string.IsNullOrEmpty(grantId) && _appliedGrantIds.Contains(grantId);

        /// <summary>
        /// Adds XP, keeping the total monotonic.
        /// </summary>
        /// <param name="amount">How much XP to add. Non-positive values are ignored.</param>
        public void AddXp(int amount)
        {
            if (amount <= 0) return;

            _totalXp += amount;
        }

        /// <summary>
        /// Raises the XP total to at least the given value.
        /// </summary>
        /// <param name="totalXp">The candidate total, usually from a backend.</param>
        /// <remarks>
        /// Never lowers it. A reconnect that reads a stale server total would otherwise snap
        /// the player's tier backwards, which reads as lost progress even when the next sync
        /// repairs it.
        /// </remarks>
        public void RaiseXpTo(int totalXp) => _totalXp = Mathf.Max(_totalXp, totalXp);

        /// <summary>
        /// Records ownership of a track, keeping the highest.
        /// </summary>
        /// <param name="track">The newly owned track.</param>
        public void GrantTrack(SeasonTrack track) =>
            _highestOwnedTrack = Mathf.Max(_highestOwnedTrack, (int)track);

        /// <summary>
        /// Records a collected reward.
        /// </summary>
        /// <param name="claimKey">The key from <see cref="SeasonRewardRef.ToClaimKey"/>.</param>
        public void RecordClaim(string claimKey)
        {
            if (string.IsNullOrEmpty(claimKey) || _claimedKeys.Contains(claimKey)) return;

            _claimedKeys.Add(claimKey);
            _pendingClaimKeys.Remove(claimKey);
        }

        /// <summary>
        /// Queues a claim whose delivery failed, so the next refresh retries it.
        /// </summary>
        /// <param name="claimKey">The key from <see cref="SeasonRewardRef.ToClaimKey"/>.</param>
        public void QueueFailedClaim(string claimKey)
        {
            if (string.IsNullOrEmpty(claimKey) || _claimedKeys.Contains(claimKey)) return;

            if (!_pendingClaimKeys.Contains(claimKey)) _pendingClaimKeys.Add(claimKey);
        }

        /// <summary>
        /// Records a grant id so a replay of the same grant is ignored.
        /// </summary>
        /// <param name="grantId">The idempotency id.</param>
        public void RecordGrantId(string grantId)
        {
            if (string.IsNullOrEmpty(grantId) || _appliedGrantIds.Contains(grantId)) return;

            _appliedGrantIds.Add(grantId);

            // Oldest ids fall off the front; a replay older than this window is indistinguishable
            // from a new grant, which is the accepted cost of a bounded save.
            if (_appliedGrantIds.Count > MaxTrackedGrantIds)
            {
                _appliedGrantIds.RemoveRange(0, _appliedGrantIds.Count - MaxTrackedGrantIds);
            }
        }

        /// <summary>
        /// Queues a grant for replay to a backend.
        /// </summary>
        /// <param name="grant">The applied grant awaiting acknowledgement.</param>
        public void QueuePendingGrant(SeasonPendingGrant grant)
        {
            if (grant == null) return;

            _pendingGrants.Add(grant);

            if (_pendingGrants.Count > MaxTrackedGrantIds)
            {
                _pendingGrants.RemoveRange(0, _pendingGrants.Count - MaxTrackedGrantIds);
            }
        }

        /// <summary>
        /// Drops every queued grant after a backend acknowledged them.
        /// </summary>
        public void ClearPendingGrants() => _pendingGrants.Clear();

        /// <summary>
        /// Returns the progress record for a quest, creating it on first use.
        /// </summary>
        /// <param name="questId">The quest id.</param>
        public SeasonQuestProgress GetOrCreateQuest(string questId)
        {
            foreach (var progress in _questProgress)
            {
                if (string.Equals(progress.QuestId, questId, StringComparison.Ordinal)) return progress;
            }

            var created = new SeasonQuestProgress(questId);
            _questProgress.Add(created);

            return created;
        }

        /// <summary>
        /// Returns today's counter for an XP source, creating it on first use.
        /// </summary>
        /// <param name="sourceId">The XP source id.</param>
        public SeasonSourceDailyXp GetOrCreateDailyXp(string sourceId)
        {
            foreach (var daily in _dailyXp)
            {
                if (string.Equals(daily.SourceId, sourceId, StringComparison.Ordinal)) return daily;
            }

            var created = new SeasonSourceDailyXp(sourceId);
            _dailyXp.Add(created);

            return created;
        }

        /// <summary>
        /// Clears every daily XP counter and opens a new window.
        /// </summary>
        /// <param name="windowStartUnix">The UTC midnight the new window starts at.</param>
        public void ResetDailyXp(long windowStartUnix)
        {
            foreach (var daily in _dailyXp)
            {
                daily.Clear();
            }

            _dailyWindowStartUnix = windowStartUnix;
        }

        /// <summary>
        /// Clears progress for the given quests and opens a new window.
        /// </summary>
        /// <param name="questIds">The quests to reset.</param>
        /// <param name="scope">Which window is being reopened.</param>
        /// <param name="windowStartUnix">The UTC boundary the new window starts at.</param>
        public void ResetQuests(IReadOnlyList<string> questIds, SeasonQuestScope scope, long windowStartUnix)
        {
            if (questIds != null)
            {
                foreach (var progress in _questProgress)
                {
                    if (Contains(questIds, progress.QuestId)) progress.Reset();
                }
            }

            switch (scope)
            {
                case SeasonQuestScope.Daily:
                    _dailyQuestWindowStartUnix = windowStartUnix;
                    break;
                case SeasonQuestScope.Weekly:
                    _weeklyQuestWindowStartUnix = windowStartUnix;
                    break;
            }
        }

        /// <summary>
        /// Moves the high-water clock forward. Never backwards.
        /// </summary>
        /// <param name="unixSeconds">The observed time.</param>
        /// <returns>The effective time to reason with.</returns>
        public long AdvanceSeen(long unixSeconds)
        {
            _lastSeenUnix = Math.Max(_lastSeenUnix, unixSeconds);
            return _lastSeenUnix;
        }

        /// <summary>
        /// Adds tier skips that could not be spent because the ladder is finished.
        /// </summary>
        /// <param name="count">How many to bank.</param>
        public void BankTierSkips(int count) => _bankedTierSkips = Mathf.Max(0, _bankedTierSkips + count);

        /// <summary>
        /// Consumes every banked tier skip.
        /// </summary>
        /// <returns>How many were banked.</returns>
        public int TakeBankedTierSkips()
        {
            var banked = _bankedTierSkips;
            _bankedTierSkips = 0;

            return banked;
        }

        /// <summary>
        /// Records a bought tier skip against the season's purchase limit.
        /// </summary>
        /// <param name="count">How many were bought.</param>
        public void RecordTierSkipPurchase(int count) =>
            _purchasedTierSkips = Mathf.Max(0, _purchasedTierSkips + count);

        /// <summary>
        /// Files a finished season and starts a fresh one, keeping durable state.
        /// </summary>
        /// <param name="seasonId">The new season id.</param>
        /// <param name="archive">The outgoing season's summary, or null on a first run.</param>
        /// <param name="maxArchiveEntries">How many past seasons to keep.</param>
        public void BeginSeason(string seasonId, SeasonArchiveEntry archive, int maxArchiveEntries)
        {
            if (archive != null)
            {
                _archive.Add(archive);

                if (maxArchiveEntries > 0 && _archive.Count > maxArchiveEntries)
                {
                    _archive.RemoveRange(0, _archive.Count - maxArchiveEntries);
                }
            }

            _seasonId = seasonId;
            _totalXp = 0;
            _highestOwnedTrack = (int)SeasonTrack.Free;
            _purchasedTierSkips = 0;

            _claimedKeys.Clear();
            _pendingClaimKeys.Clear();
            _pendingGrants.Clear();
            _appliedGrantIds.Clear();
            _questProgress.Clear();
            _dailyXp.Clear();

            _dailyWindowStartUnix = 0;
            _dailyQuestWindowStartUnix = 0;
            _weeklyQuestWindowStartUnix = 0;

            // _bankedTierSkips and _archive deliberately survive: banked skips were paid for,
            // and the archive is the record that this rollover happened.
        }

        /// <summary>
        /// Brings an older save up to the current schema.
        /// </summary>
        /// <remarks>
        /// Called by the service straight after loading. A shipped game reads saves written
        /// by every version it ever released, so the upgrade path has to be explicit rather
        /// than assumed.
        /// </remarks>
        public void Migrate()
        {
            // Lists deserialized from a save written before a field existed come back null.
            _claimedKeys ??= new List<string>();
            _pendingClaimKeys ??= new List<string>();
            _appliedGrantIds ??= new List<string>();
            _pendingGrants ??= new List<SeasonPendingGrant>();
            _questProgress ??= new List<SeasonQuestProgress>();
            _dailyXp ??= new List<SeasonSourceDailyXp>();
            _archive ??= new List<SeasonArchiveEntry>();

            if (_version >= CurrentVersion) return;

            _version = CurrentVersion;
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (var index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index], value, StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }
}
