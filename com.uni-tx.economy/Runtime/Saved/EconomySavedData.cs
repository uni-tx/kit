using System;
using System.Collections.Generic;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.Economy
{
    /// <summary>
    /// Everything the economy persists for one economy for one player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One save file per economy, keyed by the entity's save id. The ledgers are bounded:
    /// an unbounded ledger grows with every exchange and purchase a player ever makes, and
    /// a save file that grows without limit eventually costs more to write than the
    /// idempotency is worth.
    /// </para>
    /// <para>
    /// A purchase whose rewards failed to deliver is remembered in <see cref="PendingKeys"/>
    /// so the next refresh can retry it without re-charging the costs.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class EconomySavedData : ISavedData
    {
        /// <summary>
        /// Bump when the shape of this type changes, then handle it in <see cref="Migrate"/>.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// How many recent exchange ids are remembered for duplicate detection.
        /// </summary>
        public const int MaxTrackedExchangeIds = 128;

        /// <summary>
        /// How many recent purchase keys are remembered for duplicate detection.
        /// </summary>
        public const int MaxTrackedPurchaseKeys = 128;

        [SerializeField] private string _id;
        [SerializeField] private long _modifiedTimestamp;
        [SerializeField] private int _version = CurrentVersion;
        [SerializeField] private List<string> _appliedExchangeIds = new();
        [SerializeField] private List<string> _appliedPurchaseKeys = new();
        [SerializeField] private List<string> _pendingPurchaseKeys = new();

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
        /// Gets applied exchange ids, for duplicate detection.
        /// </summary>
        public IReadOnlyList<string> AppliedExchangeIds => _appliedExchangeIds;

        /// <summary>
        /// Gets applied purchase keys, for duplicate detection.
        /// </summary>
        public IReadOnlyList<string> AppliedPurchaseKeys => _appliedPurchaseKeys;

        /// <summary>
        /// Gets purchases whose rewards are still owed, keyed for retry.
        /// </summary>
        public IReadOnlyList<string> PendingPurchaseKeys => _pendingPurchaseKeys;

        /// <summary>
        /// Indicates whether an exchange id was already applied.
        /// </summary>
        /// <param name="exchangeId">The exchange id to check.</param>
        public bool HasAppliedExchange(string exchangeId)
        {
            foreach (var applied in _appliedExchangeIds)
            {
                if (string.Equals(applied, exchangeId, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        /// <summary>
        /// Records an exchange id as applied, trimming the ledger to its cap.
        /// </summary>
        /// <param name="exchangeId">The exchange id that was applied.</param>
        public void RecordAppliedExchange(string exchangeId)
        {
            if (HasAppliedExchange(exchangeId)) return;

            _appliedExchangeIds.Add(exchangeId);

            while (_appliedExchangeIds.Count > MaxTrackedExchangeIds)
            {
                _appliedExchangeIds.RemoveAt(0);
            }
        }

        /// <summary>
        /// Indicates whether a purchase key was already applied.
        /// </summary>
        /// <param name="purchaseKey">The purchase key to check.</param>
        public bool HasAppliedPurchase(string purchaseKey)
        {
            foreach (var applied in _appliedPurchaseKeys)
            {
                if (string.Equals(applied, purchaseKey, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        /// <summary>
        /// Records a purchase key as applied, trimming the ledger to its cap.
        /// </summary>
        /// <param name="purchaseKey">The purchase key that was applied.</param>
        public void RecordAppliedPurchase(string purchaseKey)
        {
            if (HasAppliedPurchase(purchaseKey)) return;

            _appliedPurchaseKeys.Add(purchaseKey);

            while (_appliedPurchaseKeys.Count > MaxTrackedPurchaseKeys)
            {
                _appliedPurchaseKeys.RemoveAt(0);
            }
        }

        /// <summary>
        /// Adds a purchase key to the pending-retry list, unless already present.
        /// </summary>
        /// <param name="purchaseKey">The purchase key whose rewards are owed.</param>
        public void AddPendingPurchase(string purchaseKey)
        {
            if (_pendingPurchaseKeys.Contains(purchaseKey)) return;

            _pendingPurchaseKeys.Add(purchaseKey);
        }

        /// <summary>
        /// Removes a purchase key from the pending-retry list.
        /// </summary>
        /// <param name="purchaseKey">The purchase key whose rewards were delivered.</param>
        public void RemovePendingPurchase(string purchaseKey)
        {
            _pendingPurchaseKeys.Remove(purchaseKey);
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
            _appliedExchangeIds ??= new List<string>();
            _appliedPurchaseKeys ??= new List<string>();
            _pendingPurchaseKeys ??= new List<string>();

            if (_version >= CurrentVersion) return;

            _version = CurrentVersion;
        }
    }
}
