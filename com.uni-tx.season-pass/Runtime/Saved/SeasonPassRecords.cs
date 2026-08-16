using System;
using UnityEngine;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// Progress against one quest.
    /// </summary>
    [Serializable]
    public sealed class SeasonQuestProgress
    {
        [SerializeField] private string _questId;
        [SerializeField] private int _amount;
        [SerializeField] private bool _isComplete;

        /// <summary>
        /// Creates an empty record for a quest.
        /// </summary>
        /// <param name="questId">The quest this tracks.</param>
        public SeasonQuestProgress(string questId) => _questId = questId;

        /// <summary>
        /// Parameterless constructor required by <c>JsonUtility</c>.
        /// </summary>
        public SeasonQuestProgress()
        {
        }

        /// <summary>
        /// Gets the quest id.
        /// </summary>
        public string QuestId => _questId;

        /// <summary>
        /// Gets how much progress has been made.
        /// </summary>
        public int Amount => _amount;

        /// <summary>
        /// Indicates whether the quest has been completed and paid out.
        /// </summary>
        public bool IsComplete => _isComplete;

        /// <summary>
        /// Adds progress, clamped at zero.
        /// </summary>
        /// <param name="amount">How much progress to add.</param>
        public void Advance(int amount) => _amount = Mathf.Max(0, _amount + amount);

        /// <summary>
        /// Marks the quest complete.
        /// </summary>
        public void Complete() => _isComplete = true;

        /// <summary>
        /// Clears progress for a new reset window.
        /// </summary>
        public void Reset()
        {
            _amount = 0;
            _isComplete = false;
        }
    }

    /// <summary>
    /// How much XP one source has contributed inside the current daily window.
    /// </summary>
    [Serializable]
    public sealed class SeasonSourceDailyXp
    {
        [SerializeField] private string _sourceId;
        [SerializeField] private int _xp;

        /// <summary>
        /// Creates a counter for a source.
        /// </summary>
        /// <param name="sourceId">The XP source this counts.</param>
        public SeasonSourceDailyXp(string sourceId) => _sourceId = sourceId;

        /// <summary>
        /// Parameterless constructor required by <c>JsonUtility</c>.
        /// </summary>
        public SeasonSourceDailyXp()
        {
        }

        /// <summary>
        /// Gets the source id.
        /// </summary>
        public string SourceId => _sourceId;

        /// <summary>
        /// Gets the XP granted by this source today.
        /// </summary>
        public int Xp => _xp;

        /// <summary>
        /// Adds to today's total.
        /// </summary>
        /// <param name="amount">How much XP to add.</param>
        public void Add(int amount) => _xp = Mathf.Max(0, _xp + amount);

        /// <summary>
        /// Clears the counter for a new day.
        /// </summary>
        public void Clear() => _xp = 0;
    }

    /// <summary>
    /// An XP grant that has been applied locally but not yet acknowledged by a backend.
    /// </summary>
    /// <remarks>
    /// The grant is applied to the local total immediately — a player who earned XP on a
    /// train sees it straight away — and replayed on the next sync. The id is what makes the
    /// replay safe: a backend that already recorded it ignores the duplicate instead of
    /// paying twice.
    /// </remarks>
    [Serializable]
    public sealed class SeasonPendingGrant
    {
        [SerializeField] private string _grantId;
        [SerializeField] private string _sourceId;
        [SerializeField] private int _amount;
        [SerializeField] private long _timestampUnix;

        /// <summary>
        /// Records a grant awaiting acknowledgement.
        /// </summary>
        /// <param name="grantId">The idempotency id.</param>
        /// <param name="sourceId">The whitelisted source that produced it.</param>
        /// <param name="amount">How much XP was applied.</param>
        /// <param name="timestampUnix">When it was applied.</param>
        public SeasonPendingGrant(string grantId, string sourceId, int amount, long timestampUnix)
        {
            _grantId = grantId;
            _sourceId = sourceId;
            _amount = amount;
            _timestampUnix = timestampUnix;
        }

        /// <summary>
        /// Parameterless constructor required by <c>JsonUtility</c>.
        /// </summary>
        public SeasonPendingGrant()
        {
        }

        /// <summary>
        /// Gets the idempotency id.
        /// </summary>
        public string GrantId => _grantId;

        /// <summary>
        /// Gets the source that produced the grant.
        /// </summary>
        public string SourceId => _sourceId;

        /// <summary>
        /// Gets how much XP was applied.
        /// </summary>
        public int Amount => _amount;

        /// <summary>
        /// Gets when the grant was applied.
        /// </summary>
        public long TimestampUnix => _timestampUnix;
    }

    /// <summary>
    /// What a finished season left behind.
    /// </summary>
    /// <remarks>
    /// Archived rather than deleted. It is small, it answers "what did I get last season"
    /// without a server round trip, and it is the evidence that a rollover happened when a
    /// player reports lost progress.
    /// </remarks>
    [Serializable]
    public sealed class SeasonArchiveEntry
    {
        [SerializeField] private string _seasonId;
        [SerializeField] private int _finalTier;
        [SerializeField] private int _finalXp;
        [SerializeField] private int _highestTrack;
        [SerializeField] private int _claimedCount;
        [SerializeField] private int _forfeitedCount;
        [SerializeField] private long _archivedAtUnix;

        /// <summary>
        /// Records the outcome of a finished season.
        /// </summary>
        /// <param name="seasonId">The season that ended.</param>
        /// <param name="finalTier">The tier reached.</param>
        /// <param name="finalXp">Total XP earned.</param>
        /// <param name="highestTrack">The highest track owned.</param>
        /// <param name="claimedCount">How many rewards were claimed.</param>
        /// <param name="forfeitedCount">How many unlocked rewards were never claimed.</param>
        /// <param name="archivedAtUnix">When the rollover happened.</param>
        public SeasonArchiveEntry(string seasonId, int finalTier, int finalXp, SeasonTrack highestTrack,
            int claimedCount, int forfeitedCount, long archivedAtUnix)
        {
            _seasonId = seasonId;
            _finalTier = finalTier;
            _finalXp = finalXp;
            _highestTrack = (int)highestTrack;
            _claimedCount = claimedCount;
            _forfeitedCount = forfeitedCount;
            _archivedAtUnix = archivedAtUnix;
        }

        /// <summary>
        /// Parameterless constructor required by <c>JsonUtility</c>.
        /// </summary>
        public SeasonArchiveEntry()
        {
        }

        /// <summary>
        /// Gets the archived season id.
        /// </summary>
        public string SeasonId => _seasonId;

        /// <summary>
        /// Gets the tier the player finished on.
        /// </summary>
        public int FinalTier => _finalTier;

        /// <summary>
        /// Gets the total XP earned that season.
        /// </summary>
        public int FinalXp => _finalXp;

        /// <summary>
        /// Gets the highest track owned that season.
        /// </summary>
        public SeasonTrack HighestTrack => (SeasonTrack)_highestTrack;

        /// <summary>
        /// Gets how many rewards were claimed.
        /// </summary>
        public int ClaimedCount => _claimedCount;

        /// <summary>
        /// Gets how many unlocked rewards expired unclaimed.
        /// </summary>
        public int ForfeitedCount => _forfeitedCount;

        /// <summary>
        /// Gets when the season was archived.
        /// </summary>
        public long ArchivedAtUnix => _archivedAtUnix;
    }
}
