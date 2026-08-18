using System;
using UnityEngine;

namespace UniTx.Store
{
    /// <summary>
    /// Everything the store persists about one offer for one player.
    /// </summary>
    /// <remarks>
    /// Free and rewarded offers are governed by a cooldown and a claim limit, both derived
    /// from <see cref="ClaimCount"/> and <see cref="LastClaimUnix"/>. The failed flag
    /// records a delivery that did not land, so the same offer is retried rather than
    /// skipped.
    /// </remarks>
    [Serializable]
    public sealed class StoreOfferRecord
    {
        [SerializeField] private string _offerId;
        [SerializeField] private int _claimCount;
        [SerializeField] private long _lastClaimUnix;
        [SerializeField] private bool _isFailed;

        /// <summary>
        /// Gets the offer id this record belongs to.
        /// </summary>
        public string OfferId => _offerId;

        /// <summary>
        /// Gets how many times the offer has been claimed.
        /// </summary>
        public int ClaimCount => _claimCount;

        /// <summary>
        /// Gets the unix time of the last successful claim, or 0 when never claimed.
        /// </summary>
        public long LastClaimUnix => _lastClaimUnix;

        /// <summary>
        /// Indicates whether the last delivery attempt failed and is queued for retry.
        /// </summary>
        public bool IsFailed => _isFailed;

        /// <summary>
        /// Creates a record for an offer.
        /// </summary>
        /// <param name="offerId">The offer id.</param>
        public StoreOfferRecord(string offerId)
        {
            _offerId = offerId;
        }

        /// <summary>
        /// Parameterless constructor required by <c>JsonUtility</c>.
        /// </summary>
        public StoreOfferRecord()
        {
        }

        /// <summary>
        /// Marks the last delivery attempt as failed, so the offer is retried rather than
        /// skipped.
        /// </summary>
        public void MarkClaimFailed() => _isFailed = true;

        /// <summary>
        /// Records a successful claim at the given time.
        /// </summary>
        /// <param name="nowUnix">The unix time of the claim.</param>
        public void RecordClaim(long nowUnix)
        {
            _claimCount++;
            _lastClaimUnix = nowUnix;
            _isFailed = false;
        }
    }
}
