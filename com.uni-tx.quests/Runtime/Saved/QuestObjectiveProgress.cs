using System;
using UnityEngine;

namespace UniTx.Quests
{
    /// <summary>
    /// One objective's persisted progress inside a quest record.
    /// </summary>
    /// <remarks>
    /// Keyed by the objective's <see cref="QuestObjectiveData.Key"/> rather than array index,
    /// so a designer reordering objectives in content does not scramble the progress.
    /// </remarks>
    [Serializable]
    public sealed class QuestObjectiveProgress
    {
        [SerializeField] private string _key;
        [SerializeField] private int _current;

        /// <summary>
        /// Gets the objective key this progress belongs to.
        /// </summary>
        public string Key => _key;

        /// <summary>
        /// Gets the reported progress so far.
        /// </summary>
        public int Current => _current;

        /// <summary>
        /// Creates a progress record.
        /// </summary>
        /// <param name="key">The objective key.</param>
        /// <param name="current">The reported progress.</param>
        public QuestObjectiveProgress(string key, int current)
        {
            _key = key;
            _current = current;
        }

        /// <summary>
        /// Parameterless constructor required by <c>JsonUtility</c>.
        /// </summary>
        public QuestObjectiveProgress()
        {
        }

        /// <summary>
        /// Adds progress, never below zero.
        /// </summary>
        /// <param name="amount">How much to add.</param>
        /// <returns>The new value.</returns>
        public int Add(int amount) => _current = Math.Max(0, _current + amount);
    }
}
