using System;
using System.Collections.Generic;
using System.Text;
using UniTx.Content;
using UnityEngine;

namespace UniTx.Quests
{
    /// <summary>
    /// One quest set's static definition, loaded as JSON content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The content unit the service selects: a "quest board" holding a mix of one-time,
    /// daily and weekly quests. A set replacement (a new season's board) re-points the
    /// entity's content key without moving the save, and the per-quest progress resets
    /// because the quest ids inside the old board are gone.
    /// </para>
    /// <para>
    /// Quests of different cadences coexist in one set: <see cref="QuestReset"/> is decided
    /// per quest, so a board can hold "finish the tutorial" (one-time) next to "win three
    /// matches today" (daily) next to "collect 50 coins this week" (weekly), each rolling
    /// over on its own clock.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class QuestSetData : IData
    {
        [Tooltip("Unique set id. Part of the recorded claim key, so changing it on a live " +
                 "set restarts every player's progress.")]
        [SerializeField] private string _id;

        [Tooltip("Player-facing set name, or a localization key.")]
        [SerializeField] private string _displayName;

        [Tooltip("The quests of the set. Quests are sorted by their order field on load, " +
                 "so the authoring order in the file does not matter.")]
        [SerializeField] private QuestData[] _quests;

        [NonSerialized] private bool _isPrepared;
        [NonSerialized] private QuestData[] _sortedQuests;

        /// <inheritdoc />
        public string Id => _id;

        /// <summary>
        /// Gets the player-facing set name or localization key.
        /// </summary>
        public string DisplayName => _displayName;

        /// <summary>
        /// Gets the quests, ordered by their sort order.
        /// </summary>
        public IReadOnlyList<QuestData> Quests
        {
            get
            {
                Prepare();
                return _sortedQuests;
            }
        }

        /// <summary>
        /// Returns the quest with the given id, or null.
        /// </summary>
        /// <param name="questId">The quest id.</param>
        public QuestData GetQuest(string questId)
        {
            Prepare();

            foreach (var quest in _sortedQuests)
            {
                if (string.Equals(quest.Id, questId, StringComparison.Ordinal)) return quest;
            }

            return null;
        }

        /// <summary>
        /// Reports authoring mistakes that would misbehave at runtime rather than fail loudly.
        /// </summary>
        /// <returns>A human-readable summary, or an empty string when the set is sound.</returns>
        /// <remarks>
        /// Content arrives as JSON a designer edits, so it is validated rather than trusted.
        /// These are the failures that would otherwise show up as a quest nobody can finish
        /// or a prerequisite that never unlocks.
        /// </remarks>
        public string DescribeProblems()
        {
            Prepare();

            var problems = new StringBuilder();

            if (string.IsNullOrWhiteSpace(_id)) Append(problems, "set id is blank");
            if (_sortedQuests.Length == 0) Append(problems, "no quests are defined");

            var seenIds = new HashSet<string>();

            foreach (var quest in _sortedQuests)
            {
                if (quest == null) continue;

                if (!seenIds.Add(quest.Id))
                {
                    // Telemetry keys on the quest id, so a duplicate is indistinguishable
                    // from a re-claim of the first one.
                    Append(problems, $"quest id '{quest.Id}' appears more than once");
                }

                if (!quest.IsValid)
                {
                    Append(problems, $"quest '{quest.Id ?? "(blank)"}' is missing an id, " +
                                     "objectives or rewards");
                }

                if (!string.IsNullOrWhiteSpace(quest.RequiredQuestId) &&
                    !seenIds.Contains(quest.RequiredQuestId) &&
                    !ReferenceEquals(quest, GetQuest(quest.RequiredQuestId)))
                {
                    // A prerequisite that does not exist locks the quest forever.
                    Append(problems,
                        $"quest '{quest.Id}' requires unknown quest '{quest.RequiredQuestId}'");
                }

                var seenObjectives = new HashSet<string>();

                foreach (var objective in quest.Objectives)
                {
                    if (objective == null) continue;

                    if (!seenObjectives.Add(objective.Key))
                    {
                        Append(problems,
                            $"quest '{quest.Id}' defines objective key '{objective.Key}' " +
                            "more than once");
                    }

                    if (!objective.IsValid)
                    {
                        Append(problems,
                            $"quest '{quest.Id}' has an objective with a blank key or zero target");
                    }
                }

                var seenRewards = new HashSet<string>();

                foreach (var reward in quest.Rewards)
                {
                    if (reward == null) continue;

                    if (!seenRewards.Add(reward.RewardId))
                    {
                        Append(problems,
                            $"quest '{quest.Id}' defines reward id '{reward.RewardId}' more than once");
                    }

                    if (!reward.IsValid)
                    {
                        Append(problems,
                            $"quest '{quest.Id}' has an incomplete reward '{reward.RewardId}'");
                    }
                }
            }

            return problems.ToString();
        }

        private static void Append(StringBuilder builder, string problem)
        {
            if (builder.Length > 0) builder.Append("; ");

            builder.Append(problem);
        }

        private void Prepare()
        {
            if (_isPrepared) return;

            _isPrepared = true;

            // Sorted once here rather than assumed: JSON is hand-edited, and the UI renders
            // in the order a designer chose.
            _sortedQuests = _quests == null
                ? Array.Empty<QuestData>()
                : (QuestData[])_quests.Clone();

            Array.Sort(_sortedQuests, static (left, right) =>
            {
                var byOrder = left.Order.CompareTo(right.Order);
                return byOrder != 0 ? byOrder : string.CompareOrdinal(left.Id, right.Id);
            });
        }
    }
}
