using System;
using UnityEngine;

namespace UniTx.Quests
{
    /// <summary>
    /// One objective inside a quest: a counter the game reports progress against.
    /// </summary>
    /// <remarks>
    /// <c>JsonUtility</c> maps fields, not properties, so every value that must survive a
    /// round trip is a serialized field exposed through a read-only property.
    /// <para>
    /// The <see cref="Key"/> is the contract between gameplay code and content: gameplay
    /// reports "win_match" and every objective whose key is "win_match" advances. The game
    /// decides what an event means — the quest system never needs to know how a match is
    /// won, only that one was.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class QuestObjectiveData
    {
        [Tooltip("The key gameplay reports progress against, e.g. \"win_match\". Objectives " +
                 "with the same key advance together when gameplay reports that key.")]
        [SerializeField] private string _key;

        [Tooltip("Player-facing objective text, or a localization key, e.g. \"Win 5 matches\".")]
        [SerializeField] private string _displayName;

        [Tooltip("How much progress the objective needs to be complete.")]
        [SerializeField] private int _target = 1;

        [Tooltip("Addressables address of the objective icon, loaded on demand by the UI.")]
        [SerializeField] private string _iconAddress;

        /// <summary>
        /// Gets the key gameplay reports progress against.
        /// </summary>
        public string Key => _key;

        /// <summary>
        /// Gets the player-facing text or localization key.
        /// </summary>
        public string DisplayName => _displayName;

        /// <summary>
        /// Gets how much progress completes the objective.
        /// </summary>
        public int Target => _target;

        /// <summary>
        /// Gets the Addressables address of the objective icon.
        /// </summary>
        public string IconAddress => _iconAddress;

        /// <summary>
        /// Indicates whether the objective is missing the fields it needs to work.
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(_key) && _target > 0;
    }
}
