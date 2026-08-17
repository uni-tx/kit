using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.SpriteLoader;
using UnityEngine;
using UnityEngine.UI;

namespace UniTx.Quests.Samples
{
    /// <summary>
    /// One quest on the board: the quest icon, name and description, one progress line per
    /// objective, and a claim button that only appears while the quest is completed.
    /// </summary>
    /// <remarks>
    /// A locked quest is dimmed rather than hidden. Showing what is still to come — even a
    /// greyed-out prerequisite — is what points a player at the next step; an empty list
    /// leaves them nothing to do.
    /// </remarks>
    public sealed class QuestRowCell : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private ImageSpriteLoader _iconLoader;
        [SerializeField] private Text _nameLabel;
        [SerializeField] private Text _descriptionLabel;
        [SerializeField] private Text _objectivesLabel;
        [SerializeField] private Button _claimButton;
        [SerializeField] private GameObject _lockedOverlay;
        [SerializeField] private GameObject _claimedOverlay;

        /// <summary>Gets the quest icon image.</summary>
        public Image Icon => _icon;

        /// <summary>Gets the quest icon loader.</summary>
        public ImageSpriteLoader IconLoader => _iconLoader;

        /// <summary>Gets the quest name label.</summary>
        public Text NameLabel => _nameLabel;

        /// <summary>Gets the quest description label.</summary>
        public Text DescriptionLabel => _descriptionLabel;

        /// <summary>Gets the objectives progress label.</summary>
        public Text ObjectivesLabel => _objectivesLabel;

        /// <summary>Gets the claim button.</summary>
        public Button ClaimButton => _claimButton;

        /// <summary>Gets the locked overlay.</summary>
        public GameObject LockedOverlay => _lockedOverlay;

        /// <summary>Gets the claimed overlay.</summary>
        public GameObject ClaimedOverlay => _claimedOverlay;

        private IQuestsService _service;
        private string _questId;

        /// <summary>
        /// Gets the quest id this row renders.
        /// </summary>
        public string QuestId => _questId;

        /// <summary>
        /// Binds the row to a quest and refreshes it.
        /// </summary>
        /// <param name="service">The quests service to read and claim through.</param>
        /// <param name="questId">The quest id.</param>
        /// <param name="cToken">Token to cancel icon loading.</param>
        public async UniTask BindAsync(IQuestsService service, string questId,
            CancellationToken cToken = default)
        {
            _service = service;
            _questId = questId;

            var quest = service.Set?.GetQuest(questId);

            if (_nameLabel != null && quest != null) _nameLabel.text = quest.DisplayName;
            if (_descriptionLabel != null && quest != null) _descriptionLabel.text = quest.Description;

            _claimButton.onClick.RemoveAllListeners();
            _claimButton.onClick.AddListener(() => ClaimAsync().Forget());

            await LoadIconAsync(quest, cToken);

            Refresh();
        }

        /// <summary>
        /// Re-reads quest state and repaints.
        /// </summary>
        public void Refresh()
        {
            var snapshot = _service?.Snapshot ?? default;

            // The board is not loaded yet; nothing to render.
            if (_service == null || snapshot.SetId == null) return;

            var quest = FindQuest(snapshot, _questId);

            if (quest.QuestId == null) return;

            var claimable = quest.State == QuestState.Completed;

            _claimButton.gameObject.SetActive(claimable);
            _claimButton.interactable = claimable;

            if (_objectivesLabel != null) _objectivesLabel.text = FormatObjectives(quest);

            if (_icon != null)
            {
                // Dimmed, not hidden: the point of a locked quest is that it is visible.
                _icon.color = quest.State == QuestState.Locked
                    ? new Color(1f, 1f, 1f, 0.45f)
                    : Color.white;
            }

            if (_lockedOverlay != null) _lockedOverlay.SetActive(quest.State == QuestState.Locked);
            if (_claimedOverlay != null) _claimedOverlay.SetActive(quest.State == QuestState.Claimed);
        }

        private static QuestSnapshot FindQuest(QuestsSnapshot snapshot, string questId)
        {
            foreach (var quest in snapshot.Quests)
            {
                if (string.Equals(quest.QuestId, questId, StringComparison.Ordinal)) return quest;
            }

            return default;
        }

        private static string FormatObjectives(QuestSnapshot quest)
        {
            var lines = new List<string>(quest.Objectives.Count);

            foreach (var objective in quest.Objectives)
            {
                var name = objective.Objective?.DisplayName ?? objective.Objective?.Key ?? "?";

                lines.Add($"{name}: {objective.Current}/{objective.Objective.Target}");
            }

            return string.Join("\n", lines);
        }

        private async UniTask LoadIconAsync(QuestData quest, CancellationToken cToken)
        {
            if (_iconLoader == null || quest == null || string.IsNullOrEmpty(quest.IconAddress)) return;

            await _iconLoader.LoadKeyAsync(quest.IconAddress, cToken);
        }

        private async UniTaskVoid ClaimAsync()
        {
            try
            {
                var result = await _service.ClaimAsync(_questId, this.GetCancellationTokenOnDestroy());

                if (result != QuestClaimResult.Claimed)
                {
                    // GrantFailed is the interesting one: the reward is still owed and will
                    // be retried on the next refresh — a very different message from
                    // AlreadyClaimed.
                    Debug.Log($"[Quests] Claim refused: {result}.");
                }

                Refresh();
            }
            catch (Exception exception)
            {
                // Fire-and-forget logs its own failures (async-unity.md) — a granter that
                // throws is a bug in the game's economy code, not a reason to drop the error.
                Debug.LogError($"[Quests] Claim failed: {exception}");
            }
        }
    }
}
