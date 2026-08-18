using System;
using System.Collections.Generic;
using System.Text;
using UniTx.Content;
using UnityEngine;

namespace UniTx.Ladder
{
    /// <summary>
    /// One ladder's static definition, loaded as JSON content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The content unit the service selects: a "ladder event" holding rungs from the first
    /// small reward to the grand prize. A replacement ladder (a new event) re-points the
    /// entity's content key without moving the save, and the climb restarts because the
    /// rung ids inside the old ladder are gone.
    /// </para>
    /// <para>
    /// Rungs are sorted by their cumulative <see cref="LadderRungData.Steps"/> on load, so
    /// the authoring order in the file does not matter — the top rung is whichever has the
    /// highest threshold, and the <see cref="IsTop"/> flag follows automatically.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class LadderData : IData
    {
        [Tooltip("Unique ladder id. Part of the recorded claim key, so changing it on a " +
                 "live ladder restarts every player's climb.")]
        [SerializeField] private string _id;

        [Tooltip("Player-facing ladder name, or a localization key.")]
        [SerializeField] private string _displayName;

        [Tooltip("The rungs of the ladder. Sorted by cumulative steps on load, so the " +
                 "authoring order in the file does not matter.")]
        [SerializeField] private LadderRungData[] _rungs;

        [NonSerialized] private bool _isPrepared;
        [NonSerialized] private LadderRungData[] _sortedRungs;

        /// <inheritdoc />
        public string Id => _id;

        /// <summary>
        /// Gets the player-facing ladder name or localization key.
        /// </summary>
        public string DisplayName => _displayName;

        /// <summary>
        /// Gets the rungs, ordered by their cumulative step threshold.
        /// </summary>
        public IReadOnlyList<LadderRungData> Rungs
        {
            get
            {
                Prepare();
                return _sortedRungs;
            }
        }

        /// <summary>
        /// Gets the grand prize rung — the one with the highest step threshold — or null
        /// when the ladder has no rungs.
        /// </summary>
        public LadderRungData TopRung
        {
            get
            {
                Prepare();
                return _sortedRungs.Length == 0 ? null : _sortedRungs[_sortedRungs.Length - 1];
            }
        }

        /// <summary>
        /// Returns the rung with the given id, or null.
        /// </summary>
        /// <param name="rungId">The rung id.</param>
        public LadderRungData GetRung(string rungId)
        {
            Prepare();

            foreach (var rung in _sortedRungs)
            {
                if (string.Equals(rung.Id, rungId, StringComparison.Ordinal)) return rung;
            }

            return null;
        }

        /// <summary>
        /// Indicates whether a rung is the grand prize — the last in the sorted order.
        /// </summary>
        /// <param name="rung">The rung to test.</param>
        public bool IsTop(LadderRungData rung)
        {
            Prepare();

            return rung != null &&
                   _sortedRungs.Length > 0 &&
                   ReferenceEquals(rung, _sortedRungs[_sortedRungs.Length - 1]);
        }

        /// <summary>
        /// Reports authoring mistakes that would misbehave at runtime rather than fail loudly.
        /// </summary>
        /// <returns>A human-readable summary, or an empty string when the ladder is sound.</returns>
        /// <remarks>
        /// Content arrives as JSON a designer edits, so it is validated rather than trusted.
        /// These are the failures that would otherwise show up as a rung nobody can reach
        /// or a duplicate threshold that hides the intended one.
        /// </remarks>
        public string DescribeProblems()
        {
            Prepare();

            var problems = new StringBuilder();

            if (string.IsNullOrWhiteSpace(_id)) Append(problems, "ladder id is blank");
            if (_sortedRungs.Length == 0) Append(problems, "no rungs are defined");

            var seenIds = new HashSet<string>();

            for (var index = 0; index < _sortedRungs.Length; index++)
            {
                var rung = _sortedRungs[index];

                if (rung == null) continue;

                if (!seenIds.Add(rung.Id))
                {
                    Append(problems, $"rung id '{rung.Id}' appears more than once");
                }

                if (!rung.IsValid)
                {
                    Append(problems,
                        $"rung '{rung.Id ?? "(blank)"}' is missing an id, a positive step " +
                        "threshold or rewards");
                }

                // A duplicate threshold makes one of the two rungs unreachable — the climb
                // skips whichever sorts second. That is almost never intended.
                if (index > 0 && _sortedRungs[index - 1].Steps == rung.Steps)
                {
                    Append(problems,
                        $"rungs '{_sortedRungs[index - 1].Id}' and '{rung.Id}' share the " +
                        "same step threshold");
                }

                var seenRewards = new HashSet<string>();

                foreach (var reward in rung.Rewards)
                {
                    if (reward == null) continue;

                    if (!seenRewards.Add(reward.RewardId))
                    {
                        Append(problems,
                            $"rung '{rung.Id}' defines reward id '{reward.RewardId}' more than once");
                    }

                    if (!reward.IsValid)
                    {
                        Append(problems,
                            $"rung '{rung.Id}' has an incomplete reward '{reward.RewardId}'");
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

            _sortedRungs = _rungs == null
                ? Array.Empty<LadderRungData>()
                : (LadderRungData[])_rungs.Clone();

            Array.Sort(_sortedRungs, static (left, right) =>
            {
                var bySteps = left.Steps.CompareTo(right.Steps);
                return bySteps != 0 ? bySteps : string.CompareOrdinal(left.Id, right.Id);
            });
        }
    }
}
