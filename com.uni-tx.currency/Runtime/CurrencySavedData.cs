using System;
using System.Collections.Generic;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.Currency
{
    /// <summary>
    /// Everything the currency system persists for one currency for one player.
    /// </summary>
    /// <remarks>
    /// Stored under the currency's id, which is also the entity id — one save file per
    /// currency, batched by the serialisation service. The grant-id ledger is bounded: an
    /// unbounded ledger grows with every reward a player ever receives, and a save file
    /// that grows without limit eventually costs more to write than the idempotency is
    /// worth. A few hundred entries covers any plausible retry or replay window.
    /// </remarks>
    [Serializable]
    public sealed class CurrencySavedData : ISavedData
    {
        /// <summary>
        /// Bump when the shape of this type changes, then handle it in <see cref="Migrate"/>.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// How many recent grant ids are remembered for duplicate detection.
        /// </summary>
        public const int MaxTrackedGrantIds = 256;

        [SerializeField] private string _id;
        [SerializeField] private long _modifiedTimestamp;
        [SerializeField] private int _version = CurrentVersion;
        [SerializeField] private int _balance;
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
        /// Gets the player's balance.
        /// </summary>
        public int Balance => _balance;

        /// <summary>
        /// Gets grant ids already applied, for idempotent deliveries.
        /// </summary>
        public IReadOnlyList<string> AppliedGrantIds => _appliedGrantIds;

        /// <summary>
        /// Sets the balance, never below zero.
        /// </summary>
        /// <param name="balance">The new balance.</param>
        public void SetBalance(int balance) => _balance = Mathf.Max(0, balance);

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
        /// Called by the entity after loading. A shipped game reads saves written by every
        /// version it ever released, so the upgrade path has to be explicit rather than assumed.
        /// </remarks>
        public void Migrate()
        {
            // Lists deserialized from a save written before a field existed come back null.
            _appliedGrantIds ??= new List<string>();

            if (_version >= CurrentVersion) return;

            _version = CurrentVersion;
        }
    }
}
