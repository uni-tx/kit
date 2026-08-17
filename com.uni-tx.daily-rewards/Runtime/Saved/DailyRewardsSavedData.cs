using System;
using System.Collections.Generic;
using UniTx.Serialization;
using UnityEngine;

namespace UniTx.DailyRewards
{
    /// <summary>
    /// Everything the daily rewards system persists for one player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stored under a single save id that never changes, while the calendar id inside it
    /// does. That is deliberate: a save keyed by calendar version would multiply forever and
    /// lose the history a replacement calendar should carry over.
    /// </para>
    /// <para>
    /// The position (<see cref="NextSlotIndex"/>, <see cref="Streak"/>,
    /// <see cref="LastClaimDayStartUnix"/>) is reset when the calendar id changes; the
    /// archive (<see cref="History"/>) survives, because it is the record of what the
    /// player collected, not where they are.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DailyRewardsSavedData : ISavedData
    {
        /// <summary>
        /// The save id the service reads and writes under.
        /// </summary>
        public const string DefaultSaveId = "daily_rewards";

        /// <summary>
        /// Bump when the shape of this type changes, then handle it in <see cref="Migrate"/>.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// How many recent grant ids are remembered for duplicate detection.
        /// </summary>
        /// <remarks>
        /// Bounded on purpose. There is one grant id per claimed day, so a few dozen entries
        /// covers months of replays; older ids fall off the front.
        /// </remarks>
        public const int MaxTrackedGrantIds = 64;

        /// <summary>
        /// How many collected claims are kept in the history archive.
        /// </summary>
        public const int MaxHistoryEntries = 128;

        [SerializeField] private string _id;
        [SerializeField] private long _modifiedTimestamp;
        [SerializeField] private int _version = CurrentVersion;

        [SerializeField] private string _calendarId;
        [SerializeField] private int _nextSlotIndex;
        [SerializeField] private int _streak;
        [SerializeField] private long _lastClaimDayStartUnix;
        [SerializeField] private long _lastClaimUnix;
        [SerializeField] private long _failedClaimDayStartUnix;
        [SerializeField] private long _lastSeenUnix;
        [SerializeField] private List<string> _appliedGrantIds = new();
        [SerializeField] private List<DailyClaimRecord> _history = new();

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
        /// Gets the calendar this progress belongs to.
        /// </summary>
        public string CalendarId => _calendarId;

        /// <summary>
        /// Gets the 0-based index of the slot the next claim will deliver.
        /// </summary>
        public int NextSlotIndex => _nextSlotIndex;

        /// <summary>
        /// Gets the streak as of the last claim.
        /// </summary>
        public int Streak => _streak;

        /// <summary>
        /// Gets the day boundary of the last successful claim, or zero when none exists.
        /// </summary>
        public long LastClaimDayStartUnix => _lastClaimDayStartUnix;

        /// <summary>
        /// Gets the exact moment of the last successful claim.
        /// </summary>
        public long LastClaimUnix => _lastClaimUnix;

        /// <summary>
        /// Gets the day boundary whose delivery failed and is queued for retry, or zero.
        /// </summary>
        public long FailedClaimDayStartUnix => _failedClaimDayStartUnix;

        /// <summary>
        /// Gets the furthest point in time this save has ever seen.
        /// </summary>
        /// <remarks>
        /// A high-water mark, not a last-write time. Time is only ever allowed to move
        /// forward from here, so winding the device clock back cannot reopen a claimed day
        /// or refill the calendar. Winding it <i>forward</i> is the other half of the
        /// problem and cannot be solved on the device — bind <c>ServerClock</c> for that.
        /// </remarks>
        public long LastSeenUnix => _lastSeenUnix;

        /// <summary>
        /// Gets the grant ids already applied, oldest first.
        /// </summary>
        public IReadOnlyList<string> AppliedGrantIds => _appliedGrantIds;

        /// <summary>
        /// Gets collected claims, oldest first.
        /// </summary>
        public IReadOnlyList<DailyClaimRecord> History => _history;

        /// <summary>
        /// Indicates whether a grant id has already been applied.
        /// </summary>
        /// <param name="grantId">The idempotency id.</param>
        public bool HasAppliedGrant(string grantId) =>
            !string.IsNullOrEmpty(grantId) && _appliedGrantIds.Contains(grantId);

        /// <summary>
        /// Records a successful claim and advances the calendar.
        /// </summary>
        /// <param name="calendarId">The calendar the claim belongs to.</param>
        /// <param name="day">The 1-based day number claimed.</param>
        /// <param name="slotIndex">The 0-based slot index claimed.</param>
        /// <param name="nextSlotIndex">The slot the next claim will deliver.</param>
        /// <param name="streak">The streak after this claim.</param>
        /// <param name="dayStartUnix">The day boundary the claim belonged to.</param>
        /// <param name="claimUnix">The exact moment of the claim.</param>
        /// <param name="grantId">The idempotency id.</param>
        public void RecordClaim(string calendarId, int day, int slotIndex, int nextSlotIndex,
            int streak, long dayStartUnix, long claimUnix, string grantId)
        {
            _calendarId = calendarId;
            _nextSlotIndex = nextSlotIndex;
            _streak = streak;
            _lastClaimDayStartUnix = dayStartUnix;
            _lastClaimUnix = claimUnix;
            _failedClaimDayStartUnix = 0;

            RecordGrantId(grantId);

            _history.Add(new DailyClaimRecord(calendarId, day, slotIndex, dayStartUnix, streak));

            if (_history.Count > MaxHistoryEntries)
            {
                _history.RemoveRange(0, _history.Count - MaxHistoryEntries);
            }
        }

        /// <summary>
        /// Marks the current day's delivery as failed, so the same slot is retried rather
        /// than skipped.
        /// </summary>
        /// <param name="dayStartUnix">The day boundary the failed claim belongs to.</param>
        public void MarkClaimFailed(long dayStartUnix) => _failedClaimDayStartUnix = dayStartUnix;

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
        /// Starts over under a new calendar, keeping the collected-claims archive.
        /// </summary>
        /// <param name="calendarId">The new calendar id.</param>
        /// <remarks>
        /// The position is meaningless across calendars — slot 4 of the old ladder is not
        /// slot 4 of the new one — so it resets while the history survives as a record of
        /// what the player collected. The high-water clock is deliberately untouched.
        /// </remarks>
        public void BeginCalendar(string calendarId)
        {
            _calendarId = calendarId;
            _nextSlotIndex = 0;
            _streak = 0;
            _lastClaimDayStartUnix = 0;
            _lastClaimUnix = 0;
            _failedClaimDayStartUnix = 0;
            _appliedGrantIds.Clear();
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
            _appliedGrantIds ??= new List<string>();
            _history ??= new List<DailyClaimRecord>();

            if (_version >= CurrentVersion) return;

            _version = CurrentVersion;
        }
    }
}
