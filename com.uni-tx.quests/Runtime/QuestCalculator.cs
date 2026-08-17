using System;
using System.Collections.Generic;

namespace UniTx.Quests
{
    /// <summary>
    /// Pure quest math: what state a quest is in, and what a report or a claim would do.
    /// </summary>
    /// <remarks>
    /// No I/O, no state of its own — everything is derived from the quest definition, the
    /// saved record and the current time, so the rules can be unit-tested without the Unity
    /// engine.
    /// </remarks>
    public static class QuestCalculator
    {
        /// <summary>
        /// Evaluates a quest's current state against its record.
        /// </summary>
        /// <param name="quest">The quest definition.</param>
        /// <param name="record">The player's record, or null when none exists.</param>
        /// <param name="prerequisiteClaimed">Whether the quest's prerequisite is claimed.</param>
        /// <returns>The state the quest is in.</returns>
        public static QuestState EvaluateState(QuestData quest, QuestRecord record,
            bool prerequisiteClaimed)
        {
            if (!string.IsNullOrWhiteSpace(quest.RequiredQuestId) && !prerequisiteClaimed)
            {
                return QuestState.Locked;
            }

            // A missing record and a wiped record are the same thing: nothing reported yet.
            // A fresh record exists after a period rollover, so the quest reads as available
            // again, not as mysteriously half-done.
            if (record == null || !HasProgress(record)) return QuestState.Available;

            if (record.IsClaimed) return QuestState.Claimed;

            return IsComplete(quest, record) ? QuestState.Completed : QuestState.InProgress;
        }

        /// <summary>
        /// Indicates whether any objective has reported progress.
        /// </summary>
        /// <param name="record">The player's record.</param>
        public static bool HasProgress(QuestRecord record)
        {
            foreach (var entry in record.Objectives)
            {
                if (entry != null && entry.Current > 0) return true;
            }

            return false;
        }

        /// <summary>
        /// Indicates whether every objective of the quest is met.
        /// </summary>
        /// <param name="quest">The quest definition.</param>
        /// <param name="record">The player's record, or null when none exists.</param>
        public static bool IsComplete(QuestData quest, QuestRecord record)
        {
            foreach (var objective in quest.Objectives)
            {
                if (objective == null || !objective.IsValid) continue;

                if ((record?.GetCurrent(objective.Key) ?? 0) < objective.Target) return false;
            }

            return true;
        }

        /// <summary>
        /// Plans a progress report against the current state.
        /// </summary>
        /// <param name="quest">The quest definition.</param>
        /// <param name="record">The player's record, or null when none exists.</param>
        /// <param name="prerequisiteClaimed">Whether the quest's prerequisite is claimed.</param>
        /// <param name="objectiveKey">The key gameplay reported.</param>
        /// <param name="amount">How much progress was reported.</param>
        /// <param name="periodStartUnix">The period boundary the progress belongs to.</param>
        /// <returns>What the report would do, or null when nothing changes.</returns>
        public static QuestReportPlan? PlanReport(QuestData quest, QuestRecord record,
            bool prerequisiteClaimed, string objectiveKey, int amount, long periodStartUnix)
        {
            // Locked and claimed quests ignore gameplay events. A locked quest's prerequisite
            // is a gate, not a race; a claimed quest is terminal until its period resets.
            var state = EvaluateState(quest, record, prerequisiteClaimed);

            if (state is QuestState.Locked or QuestState.Claimed) return null;

            // A stale record belongs to a previous period — the service wipes it before
            // planning, so a record here is always current. Guard anyway for direct callers.
            if (record != null && periodStartUnix != 0 &&
                record.PeriodStartUnix != 0 && record.PeriodStartUnix != periodStartUnix)
            {
                record = null;
            }

            if (amount <= 0) return null;

            foreach (var objective in quest.Objectives)
            {
                if (objective == null || !objective.IsValid) continue;

                if (!string.Equals(objective.Key, objectiveKey, StringComparison.Ordinal))
                {
                    continue;
                }

                var current = record?.GetCurrent(objectiveKey) ?? 0;

                if (current >= objective.Target) continue;

                var added = Math.Min(amount, objective.Target - current);
                var next = current + added;

                // The quest completes when this objective reaches its target and every
                // other objective is already met.
                var completesQuest = next >= objective.Target &&
                                     AreOtherObjectivesComplete(quest, record, objectiveKey);

                return new QuestReportPlan(objectiveKey, added, next, objective.Target,
                    completesQuest);
            }

            return null;
        }

        /// <summary>
        /// Plans a claim against the current state.
        /// </summary>
        /// <param name="quest">The quest definition.</param>
        /// <param name="record">The player's record, or null when none exists.</param>
        /// <param name="prerequisiteClaimed">Whether the quest's prerequisite is claimed.</param>
        /// <param name="periodStartUnix">The period boundary the claim belongs to.</param>
        /// <returns>The outcome a claim would produce.</returns>
        public static QuestClaimPlan PlanClaim(QuestData quest, QuestRecord record,
            bool prerequisiteClaimed, long periodStartUnix)
        {
            if (quest == null || !quest.IsValid) return new QuestClaimPlan(QuestClaimResult.Rejected, 0);

            if (!string.IsNullOrWhiteSpace(quest.RequiredQuestId) && !prerequisiteClaimed)
            {
                return new QuestClaimPlan(QuestClaimResult.Locked, 0);
            }

            if (record == null) return new QuestClaimPlan(QuestClaimResult.NotCompleted, 0);

            if (record.IsClaimed) return new QuestClaimPlan(QuestClaimResult.AlreadyClaimed, 0);

            if (record.PeriodStartUnix != periodStartUnix && periodStartUnix != 0)
            {
                return new QuestClaimPlan(QuestClaimResult.NotCompleted, 0);
            }

            return IsComplete(quest, record)
                ? new QuestClaimPlan(QuestClaimResult.Claimable, periodStartUnix)
                : new QuestClaimPlan(QuestClaimResult.NotCompleted, 0);
        }

        private static bool AreOtherObjectivesComplete(QuestData quest, QuestRecord record,
            string completedKey)
        {
            foreach (var objective in quest.Objectives)
            {
                if (objective == null || !objective.IsValid) continue;

                if (string.Equals(objective.Key, completedKey, StringComparison.Ordinal)) continue;

                if ((record?.GetCurrent(objective.Key) ?? 0) < objective.Target) return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the earliest moment any quest in the set next resets, or zero.
        /// </summary>
        /// <param name="quests">The quests of the set.</param>
        /// <param name="nowUnix">The observed time.</param>
        /// <param name="resetHourUtc">The reset hour, 0-23.</param>
        /// <param name="weekStartDay">The week-start day, 0-6.</param>
        public static long GetNextResetUnix(IEnumerable<QuestData> quests, long nowUnix,
            int resetHourUtc, int weekStartDay)
        {
            long next = 0;

            foreach (var quest in quests)
            {
                if (quest == null || quest.Reset == QuestReset.None) continue;

                var period = QuestTime.GetPeriodStart(quest.Reset, nowUnix, resetHourUtc,
                    weekStartDay);

                var boundary = quest.Reset == QuestReset.Daily
                    ? period + QuestTime.SecondsPerDay
                    : period + QuestTime.SecondsPerWeek;

                if (next == 0 || boundary < next) next = boundary;
            }

            return next;
        }
    }

    /// <summary>
    /// What a progress report would do.
    /// </summary>
    public readonly struct QuestReportPlan
    {
        /// <summary>
        /// The objective key that advances.
        /// </summary>
        public readonly string ObjectiveKey;

        /// <summary>
        /// How much progress is actually applied (capped at the target).
        /// </summary>
        public readonly int Added;

        /// <summary>
        /// The objective's progress after the report.
        /// </summary>
        public readonly int Current;

        /// <summary>
        /// The objective's target.
        /// </summary>
        public readonly int Target;

        /// <summary>
        /// Indicates whether the report completes the whole quest.
        /// </summary>
        public readonly bool CompletesQuest;

        /// <summary>
        /// Creates a plan.
        /// </summary>
        /// <param name="objectiveKey">The objective key.</param>
        /// <param name="added">Progress applied.</param>
        /// <param name="current">Progress after the report.</param>
        /// <param name="target">The objective target.</param>
        /// <param name="completesQuest">Whether the quest completes.</param>
        public QuestReportPlan(string objectiveKey, int added, int current, int target,
            bool completesQuest)
        {
            ObjectiveKey = objectiveKey;
            Added = added;
            Current = current;
            Target = target;
            CompletesQuest = completesQuest;
        }
    }

    /// <summary>
    /// What a claim would do.
    /// </summary>
    public readonly struct QuestClaimPlan
    {
        /// <summary>
        /// The outcome a claim would produce.
        /// </summary>
        public readonly QuestClaimResult Outcome;

        /// <summary>
        /// The period boundary the claim belongs to, when claimable.
        /// </summary>
        public readonly long PeriodStartUnix;

        /// <summary>
        /// Creates a plan.
        /// </summary>
        /// <param name="outcome">The outcome.</param>
        /// <param name="periodStartUnix">The period boundary.</param>
        public QuestClaimPlan(QuestClaimResult outcome, long periodStartUnix)
        {
            Outcome = outcome;
            PeriodStartUnix = periodStartUnix;
        }
    }
}
