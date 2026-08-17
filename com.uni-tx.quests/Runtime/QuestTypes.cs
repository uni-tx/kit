using System;
using System.Collections.Generic;

namespace UniTx.Quests
{
    /// <summary>
    /// How often a quest's progress is wiped and starts over.
    /// </summary>
    /// <remarks>
    /// Enum values are stable; they are stored in JSON content and in saves.
    /// </remarks>
    public enum QuestReset
    {
        /// <summary>
        /// A one-time quest. Progress never rolls over and a claimed quest stays claimed.
        /// </summary>
        None = 0,

        /// <summary>
        /// Progress resets at the configured UTC hour each day.
        /// </summary>
        Daily = 1,

        /// <summary>
        /// Progress resets once per week, at the configured UTC hour on the configured
        /// week-start day.
        /// </summary>
        Weekly = 2,
    }

    /// <summary>
    /// What a quests screen needs to know about one quest right now.
    /// </summary>
    public enum QuestState
    {
        /// <summary>
        /// No quest set is loaded.
        /// </summary>
        None = 0,

        /// <summary>
        /// The quest's prerequisite has not been claimed yet, so it cannot be worked on.
        /// </summary>
        Locked = 1,

        /// <summary>
        /// No progress has been reported yet; the quest is open for work.
        /// </summary>
        Available = 2,

        /// <summary>
        /// Progress has been reported but not all objectives are met.
        /// </summary>
        InProgress = 3,

        /// <summary>
        /// Every objective is met; the rewards can be claimed.
        /// </summary>
        Completed = 4,

        /// <summary>
        /// The rewards have been delivered.
        /// </summary>
        Claimed = 5,
    }

    /// <summary>
    /// Outcome of a claim attempt.
    /// </summary>
    public enum QuestClaimResult
    {
        /// <summary>
        /// The rewards reached the player.
        /// </summary>
        Claimed = 0,

        /// <summary>
        /// The quest is complete and its rewards can be claimed.
        /// </summary>
        /// <remarks>
        /// The calculator's plan outcome; the service never returns it to callers — a
        /// successful claim reports <see cref="Claimed"/> instead.
        /// </remarks>
        Claimable = 1,

        /// <summary>
        /// The quest was already claimed for this period.
        /// </summary>
        AlreadyClaimed = 2,

        /// <summary>
        /// Not every objective is met yet.
        /// </summary>
        NotCompleted = 3,

        /// <summary>
        /// A prerequisite quest has not been claimed.
        /// </summary>
        Locked = 4,

        /// <summary>
        /// A granter refused or failed; nothing was recorded, so the quest stays claimable.
        /// </summary>
        GrantFailed = 5,

        /// <summary>
        /// No quest set is loaded.
        /// </summary>
        NoSet = 6,

        /// <summary>
        /// The quest id does not exist in the loaded set.
        /// </summary>
        NoQuest = 7,

        /// <summary>
        /// The quest is missing the fields a granter needs.
        /// </summary>
        Rejected = 8,
    }

    /// <summary>
    /// A quest reference carried through the granter, for logging and telemetry.
    /// </summary>
    public readonly struct QuestRef : IEquatable<QuestRef>
    {
        /// <summary>
        /// The set the quest belongs to.
        /// </summary>
        public readonly string SetId;

        /// <summary>
        /// The quest id within the set.
        /// </summary>
        public readonly string QuestId;

        /// <summary>
        /// Creates a reference to one quest.
        /// </summary>
        /// <param name="setId">The owning set id.</param>
        /// <param name="questId">The quest id within the set.</param>
        public QuestRef(string setId, string questId)
        {
            SetId = setId;
            QuestId = questId;
        }

        /// <summary>
        /// Builds the idempotent grant id for one reward of this quest.
        /// </summary>
        /// <param name="setId">The owning set id.</param>
        /// <param name="questId">The quest id.</param>
        /// <param name="periodStartUnix">The period boundary the claim belongs to.</param>
        /// <param name="rewardId">The reward id within the quest.</param>
        public static string GrantId(string setId, string questId, long periodStartUnix,
            string rewardId) => $"quest:{setId}:{questId}:{periodStartUnix}:{rewardId}";

        /// <inheritdoc />
        public bool Equals(QuestRef other) =>
            string.Equals(SetId, other.SetId, StringComparison.Ordinal) &&
            string.Equals(QuestId, other.QuestId, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is QuestRef other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SetId != null ? StringComparer.Ordinal.GetHashCode(SetId) : 0;
                hash = (hash * 397) ^ (QuestId != null ? StringComparer.Ordinal.GetHashCode(QuestId) : 0);
                return hash;
            }
        }

        /// <inheritdoc />
        public override string ToString() => $"quest:{SetId}:{QuestId}";
    }

    /// <summary>
    /// One objective's progress as a quests screen reads it.
    /// </summary>
    public readonly struct QuestObjectiveSnapshot
    {
        /// <summary>
        /// The objective definition.
        /// </summary>
        public readonly QuestObjectiveData Objective;

        /// <summary>
        /// The reported progress so far, never above the target.
        /// </summary>
        public readonly int Current;

        /// <summary>
        /// Whether the objective is fully met.
        /// </summary>
        public readonly bool IsComplete;

        /// <summary>
        /// Creates an objective snapshot.
        /// </summary>
        /// <param name="objective">The definition.</param>
        /// <param name="current">Progress so far.</param>
        /// <param name="isComplete">Whether the target is met.</param>
        public QuestObjectiveSnapshot(QuestObjectiveData objective, int current, bool isComplete)
        {
            Objective = objective;
            Current = current;
            IsComplete = isComplete;
        }
    }

    /// <summary>
    /// Everything a quests screen needs about one quest.
    /// </summary>
    /// <remarks>
    /// Immutable by design — the service builds it on demand, and UI binds to
    /// <c>OnChanged</c> rather than holding a stale copy across a period boundary.
    /// </remarks>
    public readonly struct QuestSnapshot
    {
        /// <summary>
        /// The quest id.
        /// </summary>
        public readonly string QuestId;

        /// <summary>
        /// The player-facing name or localization key.
        /// </summary>
        public readonly string DisplayName;

        /// <summary>
        /// The player-facing description or localization key.
        /// </summary>
        public readonly string Description;

        /// <summary>
        /// The Addressables address of the quest icon, or an empty string.
        /// </summary>
        public readonly string IconAddress;

        /// <summary>
        /// The current state.
        /// </summary>
        public readonly QuestState State;

        /// <summary>
        /// The sort order within the set.
        /// </summary>
        public readonly int Order;

        /// <summary>
        /// How many objectives are complete.
        /// </summary>
        public readonly int CompletedObjectives;

        /// <summary>
        /// How many objectives the quest has.
        /// </summary>
        public readonly int TotalObjectives;

        /// <summary>
        /// The objectives with their progress.
        /// </summary>
        public readonly IReadOnlyList<QuestObjectiveSnapshot> Objectives;

        /// <summary>
        /// The rewards granted on claim.
        /// </summary>
        public readonly IReadOnlyList<QuestRewardData> Rewards;

        /// <summary>
        /// Indicates whether the rewards can be claimed right now.
        /// </summary>
        public readonly bool IsClaimable;

        /// <summary>
        /// Creates a quest snapshot.
        /// </summary>
        /// <param name="questId">The quest id.</param>
        /// <param name="displayName">The player-facing name or localization key.</param>
        /// <param name="description">The player-facing description or localization key.</param>
        /// <param name="iconAddress">The icon address, or empty.</param>
        /// <param name="state">The current state.</param>
        /// <param name="order">The sort order.</param>
        /// <param name="completedObjectives">How many objectives are complete.</param>
        /// <param name="totalObjectives">How many objectives exist.</param>
        /// <param name="objectives">The objectives with progress.</param>
        /// <param name="rewards">The rewards granted on claim.</param>
        /// <param name="isClaimable">Whether rewards can be claimed now.</param>
        public QuestSnapshot(string questId, string displayName, string description,
            string iconAddress, QuestState state, int order, int completedObjectives,
            int totalObjectives, IReadOnlyList<QuestObjectiveSnapshot> objectives,
            IReadOnlyList<QuestRewardData> rewards, bool isClaimable)
        {
            QuestId = questId;
            DisplayName = displayName;
            Description = description;
            IconAddress = iconAddress;
            State = state;
            Order = order;
            CompletedObjectives = completedObjectives;
            TotalObjectives = totalObjectives;
            Objectives = objectives;
            Rewards = rewards;
            IsClaimable = isClaimable;
        }
    }

    /// <summary>
    /// Everything a quests screen needs in one value.
    /// </summary>
    public readonly struct QuestsSnapshot
    {
        /// <summary>
        /// The set id, or null when none is loaded.
        /// </summary>
        public readonly string SetId;

        /// <summary>
        /// The quests of the set, in authoring order.
        /// </summary>
        public readonly IReadOnlyList<QuestSnapshot> Quests;

        /// <summary>
        /// The Unix timestamp of the next period reset, or zero for a set with no
        /// repeating quests.
        /// </summary>
        public readonly long NextResetUnix;

        /// <summary>
        /// Seconds until the next period reset.
        /// </summary>
        public readonly long RemainingSeconds;

        /// <summary>
        /// Creates a snapshot.
        /// </summary>
        /// <param name="setId">The set id, or null.</param>
        /// <param name="quests">The quests in authoring order.</param>
        /// <param name="nextResetUnix">When the next reset happens, or zero.</param>
        /// <param name="remainingSeconds">Seconds until the next reset.</param>
        public QuestsSnapshot(string setId, IReadOnlyList<QuestSnapshot> quests,
            long nextResetUnix, long remainingSeconds)
        {
            SetId = setId;
            Quests = quests;
            NextResetUnix = nextResetUnix;
            RemainingSeconds = remainingSeconds;
        }
    }
}
