using System;
using System.Collections.Generic;

namespace UniTx.Ladder
{
    /// <summary>
    /// What a ladder screen needs to know about one rung right now.
    /// </summary>
    public enum LadderState
    {
        /// <summary>
        /// No ladder is loaded.
        /// </summary>
        None = 0,

        /// <summary>
        /// The cumulative step total is below this rung's threshold.
        /// </summary>
        Locked = 1,

        /// <summary>
        /// The threshold is met and the rewards can be claimed.
        /// </summary>
        Reached = 2,

        /// <summary>
        /// The rewards have been delivered.
        /// </summary>
        Claimed = 3,
    }

    /// <summary>
    /// Outcome of a claim attempt.
    /// </summary>
    public enum LadderClaimResult
    {
        /// <summary>
        /// The rewards reached the player.
        /// </summary>
        Claimed = 0,

        /// <summary>
        /// The rung is reached and its rewards can be claimed.
        /// </summary>
        /// <remarks>
        /// The calculator's plan outcome; the service never returns it to callers — a
        /// successful claim reports <see cref="Claimed"/> instead.
        /// </remarks>
        Claimable = 1,

        /// <summary>
        /// The rung was already claimed.
        /// </summary>
        AlreadyClaimed = 2,

        /// <summary>
        /// The cumulative step total has not reached this rung yet.
        /// </summary>
        NotReached = 3,

        /// <summary>
        /// A granter refused or failed; nothing was recorded, so the rung stays claimable.
        /// </summary>
        GrantFailed = 4,

        /// <summary>
        /// No ladder is loaded.
        /// </summary>
        NoLadder = 5,

        /// <summary>
        /// The rung id does not exist in the loaded ladder.
        /// </summary>
        NoRung = 6,

        /// <summary>
        /// The rung is missing the fields a granter needs.
        /// </summary>
        Rejected = 7,
    }

    /// <summary>
    /// A rung reference carried through the granter, for logging and telemetry.
    /// </summary>
    public readonly struct LadderRungRef : IEquatable<LadderRungRef>
    {
        /// <summary>
        /// The ladder the rung belongs to.
        /// </summary>
        public readonly string LadderId;

        /// <summary>
        /// The rung id within the ladder.
        /// </summary>
        public readonly string RungId;

        /// <summary>
        /// Creates a reference to one rung.
        /// </summary>
        /// <param name="ladderId">The owning ladder id.</param>
        /// <param name="rungId">The rung id within the ladder.</param>
        public LadderRungRef(string ladderId, string rungId)
        {
            LadderId = ladderId;
            RungId = rungId;
        }

        /// <summary>
        /// Builds the idempotent grant id for one reward of this rung.
        /// </summary>
        /// <param name="ladderId">The owning ladder id.</param>
        /// <param name="rungId">The rung id.</param>
        /// <param name="rewardId">The reward id within the rung.</param>
        public static string GrantId(string ladderId, string rungId, string rewardId) =>
            $"ladder:{ladderId}:{rungId}:{rewardId}";

        /// <inheritdoc />
        public bool Equals(LadderRungRef other) =>
            string.Equals(LadderId, other.LadderId, StringComparison.Ordinal) &&
            string.Equals(RungId, other.RungId, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is LadderRungRef other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = LadderId != null ? StringComparer.Ordinal.GetHashCode(LadderId) : 0;
                hash = (hash * 397) ^
                       (RungId != null ? StringComparer.Ordinal.GetHashCode(RungId) : 0);
                return hash;
            }
        }

        /// <inheritdoc />
        public override string ToString() => $"ladder:{LadderId}:{RungId}";
    }

    /// <summary>
    /// Everything a ladder screen needs about one rung.
    /// </summary>
    /// <remarks>
    /// Immutable by design — the service builds it on demand, and UI binds to
    /// <c>OnChanged</c> rather than holding a stale copy across a step report.
    /// </remarks>
    public readonly struct LadderRungSnapshot
    {
        /// <summary>
        /// The rung id.
        /// </summary>
        public readonly string RungId;

        /// <summary>
        /// The player-facing name or localization key.
        /// </summary>
        public readonly string DisplayName;

        /// <summary>
        /// The Addressables address of the rung icon, or an empty string.
        /// </summary>
        public readonly string IconAddress;

        /// <summary>
        /// The cumulative step total that reaches this rung.
        /// </summary>
        public readonly int Steps;

        /// <summary>
        /// The current state.
        /// </summary>
        public readonly LadderState State;

        /// <summary>
        /// The rewards granted on claim.
        /// </summary>
        public readonly IReadOnlyList<LadderRewardData> Rewards;

        /// <summary>
        /// Indicates whether the rewards can be claimed right now.
        /// </summary>
        public readonly bool IsClaimable;

        /// <summary>
        /// Creates a rung snapshot.
        /// </summary>
        /// <param name="rungId">The rung id.</param>
        /// <param name="displayName">The player-facing name or localization key.</param>
        /// <param name="iconAddress">The icon address, or empty.</param>
        /// <param name="steps">The cumulative threshold.</param>
        /// <param name="state">The current state.</param>
        /// <param name="rewards">The rewards granted on claim.</param>
        /// <param name="isClaimable">Whether rewards can be claimed now.</param>
        public LadderRungSnapshot(string rungId, string displayName, string iconAddress,
            int steps, LadderState state, IReadOnlyList<LadderRewardData> rewards,
            bool isClaimable)
        {
            RungId = rungId;
            DisplayName = displayName;
            IconAddress = iconAddress;
            Steps = steps;
            State = state;
            Rewards = rewards;
            IsClaimable = isClaimable;
        }
    }

    /// <summary>
    /// Everything a ladder screen needs in one value.
    /// </summary>
    public readonly struct LadderSnapshot
    {
        /// <summary>
        /// The ladder id, or null when none is loaded.
        /// </summary>
        public readonly string LadderId;

        /// <summary>
        /// The player-facing ladder name or localization key.
        /// </summary>
        public readonly string DisplayName;

        /// <summary>
        /// The cumulative steps climbed so far.
        /// </summary>
        public readonly int TotalSteps;

        /// <summary>
        /// The rungs, in authoring order.
        /// </summary>
        public readonly IReadOnlyList<LadderRungSnapshot> Rungs;

        /// <summary>
        /// The cumulative step total of the next unclaimed rung, or zero when every rung is
        /// claimed or the ladder has none.
        /// </summary>
        public readonly int NextRungSteps;

        /// <summary>
        /// Progress toward the next unclaimed rung, 0..1. Reaches 1 when the ladder is
        /// complete (the top rung is claimed).
        /// </summary>
        public readonly float Progress;

        /// <summary>
        /// Indicates whether the top rung has been claimed — the ladder is complete.
        /// </summary>
        public readonly bool IsComplete;

        /// <summary>
        /// Creates a snapshot.
        /// </summary>
        /// <param name="ladderId">The ladder id, or null.</param>
        /// <param name="displayName">The player-facing name or localization key.</param>
        /// <param name="totalSteps">The cumulative steps climbed.</param>
        /// <param name="rungs">The rungs in authoring order.</param>
        /// <param name="nextRungSteps">The next unclaimed rung's threshold, or zero.</param>
        /// <param name="progress">Progress toward the next rung, 0..1.</param>
        /// <param name="isComplete">Whether the top rung is claimed.</param>
        public LadderSnapshot(string ladderId, string displayName, int totalSteps,
            IReadOnlyList<LadderRungSnapshot> rungs, int nextRungSteps, float progress,
            bool isComplete)
        {
            LadderId = ladderId;
            DisplayName = displayName;
            TotalSteps = totalSteps;
            Rungs = rungs;
            NextRungSteps = nextRungSteps;
            Progress = progress;
            IsComplete = isComplete;
        }
    }
}
