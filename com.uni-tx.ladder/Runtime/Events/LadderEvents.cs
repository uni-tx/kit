using System.Collections.Generic;
using UniTx.Events;
using UniTx.Rewards;

namespace UniTx.Ladder
{
    /// <summary>
    /// Raised every time steps are added to the climb.
    /// </summary>
    /// <remarks>
    /// The progress signal: how far the player has climbed, and by how much. Listen here
    /// for the analytics funnel and for anything that reacts to crossing a threshold.
    /// </remarks>
    public readonly struct LadderStepsAdded : IEvent
    {
        /// <summary>
        /// The ladder id.
        /// </summary>
        public readonly string LadderId;

        /// <summary>
        /// How many steps were added.
        /// </summary>
        public readonly int Steps;

        /// <summary>
        /// The cumulative total after the addition.
        /// </summary>
        public readonly int TotalSteps;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="ladderId">The ladder id.</param>
        /// <param name="steps">How many steps were added.</param>
        /// <param name="totalSteps">The cumulative total after.</param>
        public LadderStepsAdded(string ladderId, int steps, int totalSteps)
        {
            LadderId = ladderId;
            Steps = steps;
            TotalSteps = totalSteps;
        }
    }

    /// <summary>
    /// Raised the moment the climb crosses a rung's threshold.
    /// </summary>
    /// <remarks>
    /// The unlock signal — the moment a new claim button should appear. Listen here for a
    /// toast, a badge and the analytics funnel.
    /// </remarks>
    public readonly struct LadderRungReached : IEvent
    {
        /// <summary>
        /// The ladder id.
        /// </summary>
        public readonly string LadderId;

        /// <summary>
        /// The rung id that was reached.
        /// </summary>
        public readonly string RungId;

        /// <summary>
        /// The cumulative steps at the moment of reaching.
        /// </summary>
        public readonly int Steps;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="ladderId">The ladder id.</param>
        /// <param name="rungId">The rung id.</param>
        /// <param name="steps">The cumulative steps at the moment of reaching.</param>
        public LadderRungReached(string ladderId, string rungId, int steps)
        {
            LadderId = ladderId;
            RungId = rungId;
            Steps = steps;
        }
    }

    /// <summary>
    /// Raised after a rung's rewards reach the player.
    /// </summary>
    public readonly struct LadderRungClaimed : IEvent
    {
        /// <summary>
        /// The ladder id.
        /// </summary>
        public readonly string LadderId;

        /// <summary>
        /// The rung id.
        /// </summary>
        public readonly string RungId;

        /// <summary>
        /// The rewards that were granted.
        /// </summary>
        public readonly IReadOnlyList<LadderRewardData> Rewards;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="ladderId">The ladder id.</param>
        /// <param name="rungId">The rung id.</param>
        /// <param name="rewards">The granted rewards.</param>
        public LadderRungClaimed(string ladderId, string rungId,
            IReadOnlyList<LadderRewardData> rewards)
        {
            LadderId = ladderId;
            RungId = rungId;
            Rewards = rewards;
        }
    }

    /// <summary>
    /// Raised when a reward delivery fails, so the rung is queued for retry.
    /// </summary>
    public readonly struct LadderGrantFailed : IEvent
    {
        /// <summary>
        /// The ladder id.
        /// </summary>
        public readonly string LadderId;

        /// <summary>
        /// The rung id.
        /// </summary>
        public readonly string RungId;

        /// <summary>
        /// The reward id that could not be delivered.
        /// </summary>
        public readonly string RewardId;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="ladderId">The ladder id.</param>
        /// <param name="rungId">The rung id.</param>
        /// <param name="rewardId">The reward id.</param>
        public LadderGrantFailed(string ladderId, string rungId, string rewardId)
        {
            LadderId = ladderId;
            RungId = rungId;
            RewardId = rewardId;
        }
    }

    /// <summary>
    /// Raised when the grand prize rung is claimed — the ladder is complete.
    /// </summary>
    /// <remarks>
    /// The terminal signal. Listen here for the "event complete" celebration, the
    /// analytics conversion event and the hand-off to the next ladder.
    /// </remarks>
    public readonly struct LadderCompleted : IEvent
    {
        /// <summary>
        /// The ladder id.
        /// </summary>
        public readonly string LadderId;

        /// <summary>
        /// The grand prize rung id.
        /// </summary>
        public readonly string RungId;

        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="ladderId">The ladder id.</param>
        /// <param name="rungId">The grand prize rung id.</param>
        public LadderCompleted(string ladderId, string rungId)
        {
            LadderId = ladderId;
            RungId = rungId;
        }
    }
}
