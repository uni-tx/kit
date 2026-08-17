using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UniTx.Quests
{
    /// <summary>
    /// One quest's static definition: objectives to report against and rewards to claim.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Static because it is the same for every player: a balance patch replaces this file
    /// without touching a single save. Everything per-player lives in the saved record,
    /// keyed by quest id.
    /// </para>
    /// <para>
    /// The <see cref="Reset"/> cadence decides when progress wipes. A <see cref="QuestReset.Daily"/>
    /// quest rolls over at the configured UTC hour; a <see cref="QuestReset.Weekly"/> one at
    /// the configured hour on the configured week-start day; a <see cref="QuestReset.None"/>
    /// quest never rolls over and stays claimed once delivered. The optional
    /// <see cref="RequiredQuestId"/> chains quests: the quest stays locked until its
    /// prerequisite is claimed, which is how a tutorial hands over to a daily loop.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class QuestData
    {
        [Tooltip("Unique quest id within the set. Part of the recorded claim key, so " +
                 "changing it on a live quest restarts every player's progress.")]
        [SerializeField] private string _id;

        [Tooltip("Player-facing quest name, or a localization key.")]
        [SerializeField] private string _displayName;

        [Tooltip("Player-facing quest description, or a localization key.")]
        [SerializeField] private string _description;

        [Tooltip("Addressables address of the quest icon, loaded on demand by the UI.")]
        [SerializeField] private string _iconAddress;

        [Tooltip("How often progress resets: never (one-time), daily, or weekly.")]
        [SerializeField] private QuestReset _reset = QuestReset.None;

        [Tooltip("Quest id that must be claimed before this quest unlocks. Leave empty for " +
                 "a quest that is always available.")]
        [SerializeField] private string _requiredQuestId;

        [Tooltip("Sort order within the set. Quests are sorted on load, so the authoring " +
                 "order in the file does not matter.")]
        [SerializeField] private int _order;

        [Tooltip("The objectives that must be met for the quest to complete.")]
        [SerializeField] private QuestObjectiveData[] _objectives;

        [Tooltip("The rewards granted when the quest is claimed.")]
        [SerializeField] private QuestRewardData[] _rewards;

        /// <summary>
        /// Gets the unique quest id within the set.
        /// </summary>
        public string Id => _id;

        /// <summary>
        /// Gets the player-facing quest name or localization key.
        /// </summary>
        public string DisplayName => _displayName;

        /// <summary>
        /// Gets the player-facing quest description or localization key.
        /// </summary>
        public string Description => _description;

        /// <summary>
        /// Gets the Addressables address of the quest icon.
        /// </summary>
        public string IconAddress => _iconAddress;

        /// <summary>
        /// Gets how often progress resets.
        /// </summary>
        public QuestReset Reset => _reset;

        /// <summary>
        /// Gets the quest id that must be claimed first, or an empty string.
        /// </summary>
        public string RequiredQuestId => _requiredQuestId;

        /// <summary>
        /// Gets the sort order within the set.
        /// </summary>
        public int Order => _order;

        /// <summary>
        /// Gets the objectives that must be met for completion.
        /// </summary>
        public IReadOnlyList<QuestObjectiveData> Objectives => _objectives;

        /// <summary>
        /// Gets the rewards granted on claim.
        /// </summary>
        public IReadOnlyList<QuestRewardData> Rewards => _rewards;

        /// <summary>
        /// Indicates whether the quest is missing the fields it needs to work.
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(_id) &&
            _objectives is { Length: > 0 } &&
            _rewards is { Length: > 0 };
    }
}
