using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniTx.Quests
{
    /// <summary>
    /// Everything the quest system persists about one quest for one player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="PeriodStartUnix"/> is the period boundary this progress belongs to. A
    /// daily quest's record whose boundary is yesterday is stale: the service wipes it when
    /// the period rolls over. One-time quests always have a zero boundary and never roll
    /// over.
    /// </para>
    /// <para>
    /// Completion is derived from progress — a quest is complete when every objective
    /// reaches its target — while <see cref="IsClaimed"/> is the terminal fact that stops a
    /// repeat claim. The failed boundary records a delivery that did not land, so the same
    /// quest is retried rather than skipped.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class QuestRecord
    {
        [SerializeField] private string _questId;
        [SerializeField] private long _periodStartUnix;
        [SerializeField] private List<QuestObjectiveProgress> _objectives = new();
        [SerializeField] private bool _isClaimed;
        [SerializeField] private long _failedPeriodStartUnix;

        /// <summary>
        /// Gets the quest id this record belongs to.
        /// </summary>
        public string QuestId => _questId;

        /// <summary>
        /// Gets the period boundary the progress belongs to, or zero for one-time quests.
        /// </summary>
        public long PeriodStartUnix => _periodStartUnix;

        /// <summary>
        /// Gets the per-objective progress.
        /// </summary>
        public IReadOnlyList<QuestObjectiveProgress> Objectives => _objectives;

        /// <summary>
        /// Indicates whether the rewards were delivered for the current period.
        /// </summary>
        public bool IsClaimed => _isClaimed;

        /// <summary>
        /// Gets the period boundary whose delivery failed and is queued for retry, or zero.
        /// </summary>
        public long FailedPeriodStartUnix => _failedPeriodStartUnix;

        /// <summary>
        /// Creates a record for a quest in the given period.
        /// </summary>
        /// <param name="questId">The quest id.</param>
        /// <param name="periodStartUnix">The period boundary, or zero for one-time quests.</param>
        public QuestRecord(string questId, long periodStartUnix)
        {
            _questId = questId;
            _periodStartUnix = periodStartUnix;
        }

        /// <summary>
        /// Parameterless constructor required by <c>JsonUtility</c>.
        /// </summary>
        public QuestRecord()
        {
        }

        /// <summary>
        /// Returns the progress of an objective, or zero.
        /// </summary>
        /// <param name="key">The objective key.</param>
        public int GetCurrent(string key)
        {
            foreach (var entry in _objectives)
            {
                if (string.Equals(entry.Key, key, StringComparison.Ordinal)) return entry.Current;
            }

            return 0;
        }

        /// <summary>
        /// Adds progress to an objective, creating its record when missing.
        /// </summary>
        /// <param name="key">The objective key.</param>
        /// <param name="amount">How much to add, clamped at zero.</param>
        /// <returns>The new value.</returns>
        public int AddProgress(string key, int amount)
        {
            foreach (var entry in _objectives)
            {
                if (string.Equals(entry.Key, key, StringComparison.Ordinal)) return entry.Add(amount);
            }

            var created = new QuestObjectiveProgress(key, Math.Max(0, amount));
            _objectives.Add(created);

            return created.Current;
        }

        /// <summary>
        /// Starts a fresh period: progress wiped, claim cleared, boundary updated.
        /// </summary>
        /// <param name="periodStartUnix">The new period boundary.</param>
        public void BeginPeriod(long periodStartUnix)
        {
            _periodStartUnix = periodStartUnix;
            _objectives.Clear();
            _isClaimed = false;
            _failedPeriodStartUnix = 0;
        }

        /// <summary>
        /// Marks the current period's delivery as failed, so the quest is retried rather
        /// than skipped.
        /// </summary>
        /// <param name="periodStartUnix">The period boundary the failed claim belongs to.</param>
        public void MarkClaimFailed(long periodStartUnix) => _failedPeriodStartUnix = periodStartUnix;

        /// <summary>
        /// Records a successful claim for the period.
        /// </summary>
        /// <param name="periodStartUnix">The period boundary the claim belongs to.</param>
        public void RecordClaim(long periodStartUnix)
        {
            _periodStartUnix = periodStartUnix;
            _isClaimed = true;
            _failedPeriodStartUnix = 0;
        }

        /// <summary>
        /// Brings an older record up to the current shape.
        /// </summary>
        public void Migrate()
        {
            _objectives ??= new List<QuestObjectiveProgress>();
        }
    }
}
