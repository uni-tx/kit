using System;
using System.Collections.Generic;

namespace UniTx.Ladder
{
    /// <summary>
    /// Pure ladder math: what state a rung is in, and what a claim would do.
    /// </summary>
    /// <remarks>
    /// No I/O, no state of its own — everything is derived from the rung definition, the
    /// saved record and the cumulative step total, so the rules can be unit-tested without
    /// the Unity engine.
    /// </remarks>
    public static class LadderCalculator
    {
        /// <summary>
        /// Evaluates a rung's current state against its record and the climb.
        /// </summary>
        /// <param name="rung">The rung definition.</param>
        /// <param name="record">The player's record, or null when none exists.</param>
        /// <param name="totalSteps">The cumulative steps climbed.</param>
        /// <returns>The state the rung is in.</returns>
        public static LadderState EvaluateState(LadderRungData rung, LadderRungRecord record,
            int totalSteps)
        {
            if (rung == null || !rung.IsValid) return LadderState.Locked;

            // A claimed rung is a terminal fact — once the rewards are delivered they stay
            // delivered, even if a data edit later drags the recorded total below the
            // threshold. Claimed wins over the threshold check.
            if (record is { IsClaimed: true }) return LadderState.Claimed;

            return totalSteps < rung.Steps ? LadderState.Locked : LadderState.Reached;
        }

        /// <summary>
        /// Indicates whether a claim would be granted right now.
        /// </summary>
        /// <param name="rung">The rung definition.</param>
        /// <param name="record">The player's record, or null when none exists.</param>
        /// <param name="totalSteps">The cumulative steps climbed.</param>
        public static bool IsClaimable(LadderRungData rung, LadderRungRecord record,
            int totalSteps) =>
            EvaluateState(rung, record, totalSteps) == LadderState.Reached;

        /// <summary>
        /// Plans a claim against the current state.
        /// </summary>
        /// <param name="rung">The rung definition.</param>
        /// <param name="record">The player's record, or null when none exists.</param>
        /// <param name="totalSteps">The cumulative steps climbed.</param>
        /// <returns>The outcome a claim would produce.</returns>
        public static LadderClaimPlan PlanClaim(LadderRungData rung, LadderRungRecord record,
            int totalSteps)
        {
            if (rung == null || !rung.IsValid)
            {
                return new LadderClaimPlan(LadderClaimResult.Rejected);
            }

            if (totalSteps < rung.Steps)
            {
                return new LadderClaimPlan(LadderClaimResult.NotReached);
            }

            if (record is { IsClaimed: true })
            {
                return new LadderClaimPlan(LadderClaimResult.AlreadyClaimed);
            }

            return new LadderClaimPlan(LadderClaimResult.Claimable);
        }

        /// <summary>
        /// Computes what a ladder screen needs to render the climb: the next unclaimed
        /// rung's threshold and the progress toward it.
        /// </summary>
        /// <param name="ladder">The ladder definition.</param>
        /// <param name="saved">The player's saved progress.</param>
        /// <returns>The next threshold and normalized progress, or a complete state.</returns>
        public static LadderProgressInfo GetProgress(LadderData ladder, LadderSavedData saved)
        {
            var rungs = ladder?.Rungs;

            if (ladder == null || rungs == null || rungs.Count == 0 || saved == null)
            {
                return new LadderProgressInfo(0, 0f, false);
            }

            var totalSteps = Math.Max(0, saved.TotalSteps);

            // The bar points at the next rung the climb has NOT yet reached — the first
            // one whose threshold is still above the total. A reached-but-unclaimed rung
            // has its own claim button; the bar is about the road ahead.
            LadderRungData previous = null;
            LadderRungData next = null;

            foreach (var rung in rungs)
            {
                if (rung == null || !rung.IsValid) continue;

                if (rung.Steps <= totalSteps)
                {
                    previous = rung;
                    continue;
                }

                next = rung;
                break;
            }

            if (next == null)
            {
                // Every threshold is reached. Complete means the grand prize is claimed;
                // reached-but-unclaimed still reads as a full bar, not a partial one.
                var topClaimed = saved.GetRecord(ladder.TopRung?.Id) is { IsClaimed: true };

                return new LadderProgressInfo(0, 1f, topClaimed);
            }

            var previousSteps = previous?.Steps ?? 0;
            var span = Math.Max(1, next.Steps - previousSteps);
            var progress = MathfClamp01((float)(totalSteps - previousSteps) / span);

            return new LadderProgressInfo(next.Steps, progress, false);
        }

        private static float MathfClamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    /// <summary>
    /// What a claim would do.
    /// </summary>
    public readonly struct LadderClaimPlan
    {
        /// <summary>
        /// The outcome a claim would produce.
        /// </summary>
        public readonly LadderClaimResult Outcome;

        /// <summary>
        /// Creates a plan.
        /// </summary>
        /// <param name="outcome">The outcome.</param>
        public LadderClaimPlan(LadderClaimResult outcome)
        {
            Outcome = outcome;
        }
    }

    /// <summary>
    /// The next step on the ladder, as a screen renders it.
    /// </summary>
    public readonly struct LadderProgressInfo
    {
        /// <summary>
        /// The cumulative step total of the next unclaimed rung, or zero when complete.
        /// </summary>
        public readonly int NextRungSteps;

        /// <summary>
        /// Progress toward that rung, 0..1. 1 when complete.
        /// </summary>
        public readonly float Progress;

        /// <summary>
        /// Indicates whether the ladder is complete — every rung claimed.
        /// </summary>
        public readonly bool IsComplete;

        /// <summary>
        /// Creates the progress info.
        /// </summary>
        /// <param name="nextRungSteps">The next threshold, or zero.</param>
        /// <param name="progress">Progress toward it, 0..1.</param>
        /// <param name="isComplete">Whether the ladder is complete.</param>
        public LadderProgressInfo(int nextRungSteps, float progress, bool isComplete)
        {
            NextRungSteps = nextRungSteps;
            Progress = progress;
            IsComplete = isComplete;
        }
    }
}
