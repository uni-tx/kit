using System;
using System.Collections.Generic;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.Store
{
    /// <summary>
    /// Everything the store persists for one player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stored under a single save id that never changes, while the store id inside it
    /// does. That is deliberate: a save keyed by sale version would multiply forever and
    /// lose the history a replacement store should carry over.
    /// </para>
    /// <para>
    /// Claims live in <see cref="Records"/>, keyed by offer id. A store replacement keeps
    /// the records of offers that still exist (a recurring free offer survives a sale),
    /// while the applied-grant ledger (<see cref="AppliedGrantIds"/>) survives everything
    /// so a replay of an old claim cannot double-deliver into the economy.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class StoreSavedData : ISavedData
    {
        /// <summary>
        /// The save id the service reads and writes under.
        /// </summary>
        public const string DefaultSaveId = "store";

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

        [SerializeField] private string _storeId;
        [SerializeField] private List<StoreOfferRecord> _records = new();
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
        /// Gets the store this progress belongs to.
        /// </summary>
        public string StoreId => _storeId;

        /// <summary>
        /// Gets the per-offer records.
        /// </summary>
        public IReadOnlyList<StoreOfferRecord> Records => _records;

        /// <summary>
        /// Gets the recent applied grant ids, for duplicate detection.
        /// </summary>
        public IReadOnlyList<string> AppliedGrantIds => _appliedGrantIds;

        /// <summary>
        /// Sets the store this progress belongs to.
        /// </summary>
        /// <param name="storeId">The store id.</param>
        public void SetStoreId(string storeId) => _storeId = storeId;

        /// <summary>
        /// Returns the record for an offer, creating it on first access.
        /// </summary>
        /// <param name="offerId">The offer id.</param>
        public StoreOfferRecord GetOrCreateRecord(string offerId)
        {
            foreach (var record in _records)
            {
                if (record != null &&
                    string.Equals(record.OfferId, offerId, StringComparison.Ordinal))
                {
                    return record;
                }
            }

            var created = new StoreOfferRecord(offerId);

            _records.Add(created);

            return created;
        }

        /// <summary>
        /// Indicates whether a grant id was already applied.
        /// </summary>
        /// <param name="grantId">The grant id to check.</param>
        public bool HasAppliedGrant(string grantId)
        {
            foreach (var applied in _appliedGrantIds)
            {
                if (string.Equals(applied, grantId, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        /// <summary>
        /// Records a grant id as applied, trimming the ledger to its cap.
        /// </summary>
        /// <param name="grantId">The grant id that was applied.</param>
        public void RecordAppliedGrant(string grantId)
        {
            if (HasAppliedGrant(grantId)) return;

            _appliedGrantIds.Add(grantId);

            while (_appliedGrantIds.Count > MaxTrackedGrantIds)
            {
                _appliedGrantIds.RemoveAt(0);
            }
        }

        /// <summary>
        /// Migrates an older save to the current schema.
        /// </summary>
        /// <remarks>
        /// Idempotent, so it can run on every load without a version gate.
        /// </remarks>
        public void Migrate()
        {
            _version = CurrentVersion;
        }
    }
}
