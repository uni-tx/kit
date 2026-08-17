using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.SpriteLoader;
using UnityEngine;
using UnityEngine.UI;

namespace UniTx.DailyRewards.Samples
{
    /// <summary>
    /// One day on the calendar: the day number, a reward icon, and a claim button that only
    /// appears while that day is claimable.
    /// </summary>
    /// <remarks>
    /// A future slot is rendered dimmed rather than hidden. Showing what the rest of the
    /// week holds — especially the milestone chest — is what brings a player back tomorrow;
    /// an empty calendar is no offer at all.
    /// </remarks>
    public sealed class DailyRewardsDayCell : MonoBehaviour
    {
        [SerializeField] private Text _dayLabel;
        [SerializeField] private Image _icon;
        [SerializeField] private ImageSpriteLoader _iconLoader;
        [SerializeField] private Button _claimButton;
        [SerializeField] private GameObject _milestoneBadge;
        [SerializeField] private GameObject _claimedOverlay;
        [SerializeField] private GameObject _lockedOverlay;

        /// <summary>Gets the day number label.</summary>
        public Text DayLabel => _dayLabel;

        /// <summary>Gets the reward icon image.</summary>
        public Image Icon => _icon;

        /// <summary>Gets the reward icon loader.</summary>
        public ImageSpriteLoader IconLoader => _iconLoader;

        /// <summary>Gets the claim button.</summary>
        public Button ClaimButton => _claimButton;

        /// <summary>Gets the milestone badge.</summary>
        public GameObject MilestoneBadge => _milestoneBadge;

        /// <summary>Gets the claimed overlay.</summary>
        public GameObject ClaimedOverlay => _claimedOverlay;

        /// <summary>Gets the locked overlay.</summary>
        public GameObject LockedOverlay => _lockedOverlay;

        private IDailyRewardsService _service;
        private int _slotIndex;

        /// <summary>
        /// Gets the 0-based slot this cell renders.
        /// </summary>
        public int SlotIndex => _slotIndex;

        /// <summary>
        /// Binds the cell to a slot and refreshes it.
        /// </summary>
        /// <param name="service">The daily rewards service to read and claim through.</param>
        /// <param name="slotIndex">The 0-based slot index.</param>
        /// <param name="cToken">Token to cancel icon loading.</param>
        public async UniTask BindAsync(IDailyRewardsService service, int slotIndex,
            CancellationToken cToken = default)
        {
            _service = service;
            _slotIndex = slotIndex;

            var slot = service.Calendar?.GetSlot(slotIndex);

            if (_dayLabel != null && slot != null) _dayLabel.text = slot.Day.ToString();

            if (_milestoneBadge != null) _milestoneBadge.SetActive(slot != null && slot.IsMilestone);

            _claimButton.onClick.RemoveAllListeners();
            _claimButton.onClick.AddListener(() => ClaimAsync().Forget());

            await LoadIconAsync(slot, cToken);

            Refresh();
        }

        /// <summary>
        /// Re-reads claim state and repaints.
        /// </summary>
        public void Refresh()
        {
            var snapshot = _service?.Snapshot ?? default;

            // The calendar is not loaded yet; nothing to render.
            if (_service == null || snapshot.State == DailyRewardsState.None) return;

            var isPast = _slotIndex < snapshot.CurrentSlotIndex;
            var isCurrent = _slotIndex == snapshot.CurrentSlotIndex;

            // Only today's slot is ever claimable; the rest of the week is claimed or locked.
            var claimable = isCurrent && snapshot.State == DailyRewardsState.Claimable;
            var claimed = isPast || isCurrent;

            _claimButton.gameObject.SetActive(claimable);
            _claimButton.interactable = claimable;

            if (_icon != null)
            {
                // Dimmed, not hidden: the point of the future slots is that they are visible.
                _icon.color = isCurrent ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }

            if (_claimedOverlay != null) _claimedOverlay.SetActive(claimed);
            if (_lockedOverlay != null) _lockedOverlay.SetActive(!isCurrent);
        }

        private async UniTask LoadIconAsync(DailyRewardSlotData slot, CancellationToken cToken)
        {
            if (_iconLoader == null || slot == null || string.IsNullOrEmpty(slot.IconAddress)) return;

            await _iconLoader.LoadKeyAsync(slot.IconAddress, cToken);
        }

        private async UniTaskVoid ClaimAsync()
        {
            try
            {
                // Only today's slot can ever be claimable, so claiming from the cell delivers
                // exactly the reward the player is looking at.
                var result = await _service.ClaimAsync(this.GetCancellationTokenOnDestroy());

                if (result != DailyClaimResult.Claimed)
                {
                    Debug.Log($"[DailyRewards] Claim refused: {result}.");
                }

                Refresh();
            }
            catch (Exception exception)
            {
                // Fire-and-forget logs its own failures (async-unity.md) — a granter that
                // throws is a bug in the game's economy code, not a reason to drop the error.
                Debug.LogError($"[DailyRewards] Claim failed: {exception}");
            }
        }
    }
}
