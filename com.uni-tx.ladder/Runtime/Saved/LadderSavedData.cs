using System;
using System.Collections.Generic;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.Ladder
{
    /// <summary>
    /// Everything the ladder persists for one player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stored under a single save id that never changes, while the ladder id inside it
    /// does. That is deliberate: a save keyed by event version would multiply forever and
    /// lose the history a replacement ladder should carry over.
    /// </para>
    /// <para>
    /// The climb is one number — <see cref="TotalSteps"/> — that only ever grows while a
    /// ladder is live. Rung claims live in <see cref="Records"/>, keyed by rung id. A
    /// ladder replacement starts the climb over (the old rung ids no longer exist), while
    /// the applied-grant ledger (<see cref="AppliedGrantIds"/>) survives so a replay of an
    /// old claim cannot double-deliver into the economy.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class LadderSavedData : ISavedData
    {
        /// <summary>
        /// The save id the service reads and writes under.
        /// </summary>
        public const string DefaultSaveId = "ladder";

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

        [SerializeField] private string _ladderId;
        [SerializeField] private int _totalSteps;
        [SerializeField] private List<LadderRungRecord> _records = new();
        [SerializeField] private List<string> _appliedGrantIds = new();

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
        /// Gets the ladder this progress belongs to.
        /// </summary>
        public string LadderId => _ladderId;

        /// <summary>
        /// Gets the cumulative steps climbed.
        /// </summary>
        public int TotalSteps => _totalSteps;

        /// <summary>
        /// Gets the per-rung records.
        /// </summary>
        public IReadOnlyList<LadderRungRecord> Records => _records;

        /// <summary>
        /// Gets the grant ids already applied, oldest first.
        /// </summary>
        public IReadOnlyList<string> AppliedGrantIds => _appliedGrantIds;

        /// <summary>
        /// Returns the record of a rung, or null.
        /// </summary>
        /// <param name="rungId">The rung id.</param>
        public LadderRungRecord GetRecord(string rungId)
        {
            foreach (var record in _records)
            {
                if (string.Equals(record.RungId, rungId, StringComparison.Ordinal))
                {
                    return record;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the record of a rung, creating one when missing.
        /// </summary>
        /// <param name="rungId">The rung id.</param>
        public LadderRungRecord GetOrCreateRecord(string rungId)
        {
            var existing = GetRecord(rungId);

            if (existing != null) return existing;

            var created = new LadderRungRecord(rungId);
            _records.Add(created);

            return created;
        }

        /// <summary>
        /// Starts a new climb under a new ladder, keeping the applied-grant ledger.
        /// </summary>
        /// <param name="ladderId">The new ladder id.</param>
        /// <remarks>
        /// The climb and the records are meaningless across ladders — "50 steps" on the
        /// old event is not the same ladder as the new one — so they reset while the
        /// grant ledger survives as protection against a replayed delivery.
        /// </remarks>
        public void BeginLadder(string ladderId)
        {
            _ladderId = ladderId;
            _totalSteps = 0;
            _records.Clear();
        }

        /// <summary>
        /// Adds steps to the climb. Never negative.
        /// </summary>
        /// <param name="steps">How many steps to add.</param>
        /// <returns>The new total.</returns>
        public int AddSteps(int steps) => _totalSteps = Math.Max(0, _totalSteps + steps);

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
            _records ??= new List<LadderRungRecord>();
            _appliedGrantIds ??= new List<string>();

            if (_version >= CurrentVersion) return;

            _version = CurrentVersion;
        }
    }
}
