using System;
using System.Collections.Generic;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.Quests
{
    /// <summary>
    /// Everything the quest system persists for one player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stored under a single save id that never changes, while the set id inside it does.
    /// That is deliberate: a save keyed by set version would multiply forever and lose the
    /// history a replacement board should carry over.
    /// </para>
    /// <para>
    /// Per-quest progress lives in <see cref="Records"/>, keyed by quest id. A set
    /// replacement starts fresh records (the old quest ids no longer exist), while the
    /// applied-grant ledger (<see cref="AppliedGrantIds"/>) survives so a replay of an old
    /// claim cannot double-deliver into the economy.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class QuestsSavedData : ISavedData
    {
        /// <summary>
        /// The save id the service reads and writes under.
        /// </summary>
        public const string DefaultSaveId = "quests";

        /// <summary>
        /// Bump when the shape of this type changes, then handle it in <see cref="Migrate"/>.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// How many recent grant ids are remembered for duplicate detection.
        /// </summary>
        public const int MaxTrackedGrantIds = 128;

        [SerializeField] private string _id;
        [SerializeField] private long _modifiedTimestamp;
        [SerializeField] private int _version = CurrentVersion;

        [SerializeField] private string _setId;
        [SerializeField] private List<QuestRecord> _records = new();
        [SerializeField] private List<string> _appliedGrantIds = new();
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
        /// Gets the set this progress belongs to.
        /// </summary>
        public string SetId => _setId;

        /// <summary>
        /// Gets the per-quest records.
        /// </summary>
        public IReadOnlyList<QuestRecord> Records => _records;

        /// <summary>
        /// Gets the grant ids already applied, oldest first.
        /// </summary>
        public IReadOnlyList<string> AppliedGrantIds => _appliedGrantIds;

        /// <summary>
        /// Gets the furthest point in time this save has ever seen.
        /// </summary>
        /// <remarks>
        /// A high-water mark, not a last-write time. Time is only ever allowed to move
        /// forward from here, so winding the device clock back cannot reopen a claimed
        /// quest or refill a reset one. Winding it <i>forward</i> is the other half of the
        /// problem and cannot be solved on the device — bind <c>ServerClock</c> for that.
        /// </remarks>
        public long LastSeenUnix => _lastSeenUnix;

        /// <summary>
        /// Returns the record of a quest, or null.
        /// </summary>
        /// <param name="questId">The quest id.</param>
        public QuestRecord GetRecord(string questId)
        {
            foreach (var record in _records)
            {
                if (string.Equals(record.QuestId, questId, StringComparison.Ordinal))
                {
                    return record;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the record of a quest, creating one for the given period when missing.
        /// </summary>
        /// <param name="questId">The quest id.</param>
        /// <param name="periodStartUnix">The period boundary for a fresh record.</param>
        public QuestRecord GetOrCreateRecord(string questId, long periodStartUnix)
        {
            var existing = GetRecord(questId);

            if (existing != null) return existing;

            var created = new QuestRecord(questId, periodStartUnix);
            _records.Add(created);

            return created;
        }

        /// <summary>
        /// Starts over under a new set, keeping the applied-grant ledger.
        /// </summary>
        /// <param name="setId">The new set id.</param>
        /// <remarks>
        /// The records are meaningless across sets — "win 5 matches" on the old board is
        /// not the same quest on the new one — so they reset while the grant ledger
        /// survives as protection against a replayed delivery. The high-water clock is
        /// deliberately untouched.
        /// </remarks>
        public void BeginSet(string setId)
        {
            _setId = setId;
            _records.Clear();
        }

        /// <summary>
        /// Indicates whether a grant id has already been applied.
        /// </summary>
        /// <param name="grantId">The idempotency id.</param>
        public bool HasAppliedGrant(string grantId) =>
            !string.IsNullOrEmpty(grantId) && _appliedGrantIds.Contains(grantId);

        /// <summary>
        /// Records a grant id so a replay of the same grant is ignored.
        /// </summary>
        /// <param name="grantId">The idempotency id.</param>
        public void RecordGrantId(string grantId)
        {
            if (string.IsNullOrEmpty(grantId) || _appliedGrantIds.Contains(grantId)) return;

            _appliedGrantIds.Add(grantId);

            // Oldest ids fall off the front; a replay older than this window is
            // indistinguishable from a new grant, which is the accepted cost of a bounded save.
            if (_appliedGrantIds.Count > MaxTrackedGrantIds)
            {
                _appliedGrantIds.RemoveRange(0, _appliedGrantIds.Count - MaxTrackedGrantIds);
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
        /// Brings an older save up to the current schema.
        /// </summary>
        /// <remarks>
        /// Called by the entity straight after loading. A shipped game reads saves written
        /// by every version it ever released, so the upgrade path has to be explicit rather
        /// than assumed.
        /// </remarks>
        public void Migrate()
        {
            // Lists deserialized from a save written before a field existed come back null.
            _records ??= new List<QuestRecord>();
            _appliedGrantIds ??= new List<string>();

            foreach (var record in _records)
            {
                record?.Migrate();
            }

            if (_version >= CurrentVersion) return;

            _version = CurrentVersion;
        }
    }
}
