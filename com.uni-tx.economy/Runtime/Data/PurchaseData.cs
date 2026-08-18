using System;
using System.Collections.Generic;
using UniTx.Rewards;
using UnityEngine;

namespace UniTx.Economy
{
    /// <summary>
    /// One virtual purchase: costs one or more currencies, grants rewards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The economy's shop: a player pays <see cref="Costs"/> and receives
    /// <see cref="Rewards"/>, both content-defined. This is the local, offline cousin of
    /// the IAP-backed store — a currency sink with a reward tap, which is what makes an
    /// economy feel worth engaging with.
    /// </para>
    /// <para>
    /// Rewards reuse the kit's <see cref="RewardData"/> so they flow through the same
    /// <c>IRewardService</c> as quests, ladders and stores — one grant path, one
    /// idempotency ledger.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class PurchaseData
    {
        [Tooltip("Unique purchase id, referenced by the UI and telemetry.")]
        [SerializeField] private string _id;

        [Tooltip("Player-facing name, or a localization key.")]
        [SerializeField] private string _displayName;

        [Tooltip("What the player pays. All lines are charged.")]
        [SerializeField] private List<CostData> _costs = new();

        [Tooltip("What the player receives, delivered through the kit's reward service.")]
        [SerializeField] private List<RewardData> _rewards = new();

        /// <summary>
        /// Gets the purchase id.
        /// </summary>
        public string Id => _id;

        /// <summary>
        /// Gets the player-facing name.
        /// </summary>
        public string DisplayName => _displayName;

        /// <summary>
        /// Gets the costs charged on purchase.
        /// </summary>
        public IReadOnlyList<CostData> Costs => _costs;

        /// <summary>
        /// Gets the rewards granted on purchase.
        /// </summary>
        public IReadOnlyList<RewardData> Rewards => _rewards;
    }
}
