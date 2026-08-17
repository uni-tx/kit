using System.Collections.Generic;
using UniTx.Events;
using UniTx.Rewards;

namespace UniTx.Quests
{
    /// <summary>
    /// Raised the first time progress is reported on a quest.
    /// </summary>
    /// <remarks>
    /// The adoption signal: a player opened the board and started working. Listen here for
    /// a tutorial nudge or a welcome quest highlight.
    /// </remarks>
    public readonly struct QuestStarted : IEvent
    {
        /// <summary>
        /// The set the quest belongs to.
        /// </summary>
        public readonly string SetId;

        /// <summary>
        /// The quest id.
        /// </summary>
        public readonly string QuestId;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="setId">The set id.</param>
        /// <param name="questId">The quest id.</param>
        public QuestStarted(string setId, string questId)
        {
            SetId = setId;
            QuestId = questId;
        }
    }

    /// <summary>
    /// Raised when progress is reported on a quest.
    /// </summary>
    public readonly struct QuestProgressed : IEvent
    {
        /// <summary>
        /// The set the quest belongs to.
        /// </summary>
        public readonly string SetId;

        /// <summary>
        /// The quest id.
        /// </summary>
        public readonly string QuestId;

        /// <summary>
        /// The objective key that advanced.
        /// </summary>
        public readonly string ObjectiveKey;

        /// <summary>
        /// The objective's progress after the report.
        /// </summary>
        public readonly int Current;

        /// <summary>
        /// The objective's target.
        /// </summary>
        public readonly int Target;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="setId">The set id.</param>
        /// <param name="questId">The quest id.</param>
        /// <param name="objectiveKey">The objective key.</param>
        /// <param name="current">Progress after the report.</param>
        /// <param name="target">The objective target.</param>
        public QuestProgressed(string setId, string questId, string objectiveKey,
            int current, int target)
        {
            SetId = setId;
            QuestId = questId;
            ObjectiveKey = objectiveKey;
            Current = current;
            Target = target;
        }
    }

    /// <summary>
    /// Raised when every objective of a quest is met.
    /// </summary>
    /// <remarks>
    /// The completion signal — the moment the claim button should appear. Listen here for
    /// a toast, a badge and the analytics funnel.
    /// </remarks>
    public readonly struct QuestCompleted : IEvent
    {
        /// <summary>
        /// The set the quest belongs to.
        /// </summary>
        public readonly string SetId;

        /// <summary>
        /// The quest id.
        /// </summary>
        public readonly string QuestId;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="setId">The set id.</param>
        /// <param name="questId">The quest id.</param>
        public QuestCompleted(string setId, string questId)
        {
            SetId = setId;
            QuestId = questId;
        }
    }

    /// <summary>
    /// Raised after a quest's rewards reach the player.
    /// </summary>
    public readonly struct QuestClaimed : IEvent
    {
        /// <summary>
        /// The set the quest belongs to.
        /// </summary>
        public readonly string SetId;

        /// <summary>
        /// The quest id.
        /// </summary>
        public readonly string QuestId;

        /// <summary>
        /// The rewards that were granted.
        /// </summary>
        public readonly IReadOnlyList<QuestRewardData> Rewards;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="setId">The set id.</param>
        /// <param name="questId">The quest id.</param>
        /// <param name="rewards">The granted rewards.</param>
        public QuestClaimed(string setId, string questId, IReadOnlyList<QuestRewardData> rewards)
        {
            SetId = setId;
            QuestId = questId;
            Rewards = rewards;
        }
    }

    /// <summary>
    /// Raised when a reward delivery fails, so the quest is queued for retry.
    /// </summary>
    public readonly struct QuestGrantFailed : IEvent
    {
        /// <summary>
        /// The set the quest belongs to.
        /// </summary>
        public readonly string SetId;

        /// <summary>
        /// The quest id.
        /// </summary>
        public readonly string QuestId;

        /// <summary>
        /// The reward id that could not be delivered.
        /// </summary>
        public readonly string RewardId;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="setId">The set id.</param>
        /// <param name="questId">The quest id.</param>
        /// <param name="rewardId">The reward id.</param>
        public QuestGrantFailed(string setId, string questId, string rewardId)
        {
            SetId = setId;
            QuestId = questId;
            RewardId = rewardId;
        }
    }

    /// <summary>
    /// Raised when a period rollover wipes a quest's progress.
    /// </summary>
    /// <remarks>
    /// The cadence signal: a daily quest has reset, so the board is fresh. Listen here to
    /// clear a stale "claimed" toast or to log that a player is coming back across resets.
    /// </remarks>
    public readonly struct QuestPeriodReset : IEvent
    {
        /// <summary>
        /// The set the quest belongs to.
        /// </summary>
        public readonly string SetId;

        /// <summary>
        /// The quest id.
        /// </summary>
        public readonly string QuestId;

        /// <summary>
        /// The quest's cadence.
        /// </summary>
        public readonly QuestReset Reset;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="setId">The set id.</param>
        /// <param name="questId">The quest id.</param>
        /// <param name="reset">The quest's cadence.</param>
        public QuestPeriodReset(string setId, string questId, QuestReset reset)
        {
            SetId = setId;
            QuestId = questId;
            Reset = reset;
        }
    }
}
