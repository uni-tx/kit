using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.SpriteLoader;
using UnityEngine;
using UnityEngine.UI;

namespace UniTx.Ladder.Samples
{
    /// <summary>
    /// One rung on the ladder: the rung icon, name, step threshold, rewards, and a claim
    /// button that only appears once the rung is reached.
    /// </summary>
    /// <remarks>
    /// A locked rung is dimmed rather than hidden. Showing the road ahead — even greyed
    /// out — is what keeps a player climbing; an empty ladder leaves them nothing to aim
    /// for.
    /// </remarks>
    public sealed class LadderRungCell : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private ImageSpriteLoader _iconLoader;
        [SerializeField] private Text _nameLabel;
        [SerializeField] private Text _stepsLabel;
        [SerializeField] private Text _rewardsLabel;
        [SerializeField] private Button _claimButton;
        [SerializeField] private GameObject _claimedOverlay;

        /// <summary>Gets the rung icon image.</summary>
        public Image Icon => _icon;

        /// <summary>Gets the rung icon loader.</summary>
        public ImageSpriteLoader IconLoader => _iconLoader;

        /// <summary>Gets the rung name label.</summary>
        public Text NameLabel => _nameLabel;

        /// <summary>Gets the step threshold label.</summary>
        public Text StepsLabel => _stepsLabel;

        /// <summary>Gets the rewards label.</summary>
        public Text RewardsLabel => _rewardsLabel;

        /// <summary>Gets the claim button.</summary>
        public Button ClaimButton => _claimButton;

        /// <summary>Gets the claimed overlay.</summary>
        public GameObject ClaimedOverlay => _claimedOverlay;

        private ILadderService _service;
        private string _rungId;

        /// <summary>
        /// Gets the rung id this row renders.
        /// </summary>
        public string RungId => _rungId;

        /// <summary>
        /// Binds the row to a rung and refreshes it.
        /// </summary>
        /// <param name="service">The ladder service to read and claim through.</param>
        /// <param name="rungId">The rung id.</param>
        /// <param name="cToken">Token to cancel icon loading.</param>
        public async UniTask BindAsync(ILadderService service, string rungId,
            CancellationToken cToken = default)
        {
            _service = service;
            _rungId = rungId;

            var rung = service.Ladder?.GetRung(rungId);

            if (_nameLabel != null && rung != null) _nameLabel.text = rung.DisplayName;
            if (_stepsLabel != null && rung != null) _stepsLabel.text = $"{rung.Steps} steps";
            if (_rewardsLabel != null && rung != null) _rewardsLabel.text = FormatRewards(rung);

            _claimButton.onClick.RemoveAllListeners();
            _claimButton.onClick.AddListener(() => ClaimAsync().Forget());

            await LoadIconAsync(rung, cToken);

            Refresh();
        }

        /// <summary>
        /// Re-reads rung state and repaints.
        /// </summary>
        public void Refresh()
        {
            var snapshot = _service?.Snapshot ?? default;

            // The ladder is not loaded yet; nothing to render.
            if (_service == null || snapshot.LadderId == null) return;

            var rung = FindRung(snapshot, _rungId);

            if (rung.RungId == null) return;

            var claimable = rung.State == LadderState.Reached;

            _claimButton.gameObject.SetActive(claimable);
            _claimButton.interactable = claimable;

            if (_icon != null)
            {
                // Dimmed, not hidden: the point of a locked rung is that it is visible.
                _icon.color = rung.State == LadderState.Locked
                    ? new Color(1f, 1f, 1f, 0.45f)
                    : Color.white;
            }

            if (_claimedOverlay != null) _claimedOverlay.SetActive(rung.State == LadderState.Claimed);
        }

        private static LadderRungSnapshot FindRung(LadderSnapshot snapshot, string rungId)
        {
            foreach (var rung in snapshot.Rungs)
            {
                if (string.Equals(rung.RungId, rungId, StringComparison.Ordinal)) return rung;
            }

            return default;
        }

        private static string FormatRewards(LadderRungData rung)
        {
            var parts = new System.Collections.Generic.List<string>(rung.Rewards.Count);

            foreach (var reward in rung.Rewards)
            {
                if (reward == null) continue;

                parts.Add($"{reward.Amount}x {reward.ItemId}");
            }

            return string.Join(", ", parts);
        }

        private async UniTask LoadIconAsync(LadderRungData rung, CancellationToken cToken)
        {
            if (_iconLoader == null || rung == null || string.IsNullOrEmpty(rung.IconAddress)) return;

            await _iconLoader.LoadKeyAsync(rung.IconAddress, cToken);
        }

        private async UniTaskVoid ClaimAsync()
        {
            try
            {
                var result = await _service.ClaimAsync(_rungId, this.GetCancellationTokenOnDestroy());

                if (result != LadderClaimResult.Claimed)
                {
                    // GrantFailed is the interesting one: the reward is still owed and will
                    // be retried on the next refresh — a very different message from
                    // AlreadyClaimed.
                    Debug.Log($"[Ladder] Claim refused: {result}.");
                }

                Refresh();
            }
            catch (Exception exception)
            {
                // Fire-and-forget logs its own failures (async-unity.md) — a granter that
                // throws is a bug in the game's economy code, not a reason to drop the error.
                Debug.LogError($"[Ladder] Claim failed: {exception}");
            }
        }
    }
}
