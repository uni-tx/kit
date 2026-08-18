using System;
using UnityEngine;

namespace UniTx.Ladder
{
    /// <summary>
    /// Everything the ladder persists about one rung for one player.
    /// </summary>
    /// <remarks>
    /// Climbing is cumulative and derived — a rung is reached when the saved total steps
    /// cross its threshold — so the record only has to answer one question: has this rung
    /// been claimed. The failed flag records a delivery that did not land, so the same
    /// rung is retried rather than skipped.
    /// </remarks>
    [Serializable]
    public sealed class LadderRungRecord
    {
        [SerializeField] private string _rungId;
        [SerializeField] private bool _isClaimed;
        [SerializeField] private bool _isFailed;

        /// <summary>
        /// Gets the rung id this record belongs to.
        /// </summary>
        public string RungId => _rungId;

        /// <summary>
        /// Indicates whether the rewards were delivered.
        /// </summary>
        public bool IsClaimed => _isClaimed;

        /// <summary>
        /// Indicates whether the last delivery attempt failed and is queued for retry.
        /// </summary>
        public bool IsFailed => _isFailed;

        /// <summary>
        /// Creates a record for a rung.
        /// </summary>
        /// <param name="rungId">The rung id.</param>
        public LadderRungRecord(string rungId)
        {
            _rungId = rungId;
        }

        /// <summary>
        /// Parameterless constructor required by <c>JsonUtility</c>.
        /// </summary>
        public LadderRungRecord()
        {
        }

        /// <summary>
        /// Marks the last delivery attempt as failed, so the rung is retried rather than
        /// skipped.
        /// </summary>
        public void MarkClaimFailed() => _isFailed = true;

        /// <summary>
        /// Records a successful claim.
        /// </summary>
        public void RecordClaim()
        {
            _isClaimed = true;
            _isFailed = false;
        }
    }
}
