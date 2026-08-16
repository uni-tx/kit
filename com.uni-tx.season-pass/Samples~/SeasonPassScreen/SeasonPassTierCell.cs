using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.SpriteLoader;
using UnityEngine;
using UnityEngine.UI;

namespace UniTx.SeasonPass.Samples
{
    /// <summary>
    /// One tier column: the tier number, a free slot and a paid slot.
    /// </summary>
    /// <remarks>
    /// A locked paid reward is rendered dimmed rather than hidden. Showing what the paid track
    /// holds is the entire conversion mechanic of a season pass — a player cannot want
    /// something they have never seen.
    /// </remarks>
    public sealed class SeasonPassTierCell : MonoBehaviour
    {
        [SerializeField] private Text _tierLabel;
        [SerializeField] private Button _freeButton;
        [SerializeField] private Image _freeIcon;
        [SerializeField] private ImageSpriteLoader _freeIconLoader;
        [SerializeField] private Button _paidButton;
        [SerializeField] private Image _paidIcon;
        [SerializeField] private ImageSpriteLoader _paidIconLoader;
        [SerializeField] private GameObject _lockedOverlay;
        [SerializeField] private GameObject _claimedOverlay;

        private ISeasonPassService _service;
        private int _tier;

        /// <summary>
        /// Gets the tier this cell renders.
        /// </summary>
        public int Tier => _tier;

        /// <summary>
        /// Binds the cell to a tier and refreshes it.
        /// </summary>
        /// <param name="service">The season pass to read and claim through.</param>
        /// <param name="tier">The 1-based tier number.</param>
        /// <param name="cToken">Token to cancel icon loading.</param>
        public async UniTask BindAsync(ISeasonPassService service, int tier,
            CancellationToken cToken = default)
        {
            _service = service;
            _tier = tier;

            if (_tierLabel != null) _tierLabel.text = tier.ToString();

            _freeButton.onClick.RemoveAllListeners();
            _paidButton.onClick.RemoveAllListeners();
            _freeButton.onClick.AddListener(() => ClaimAsync(SeasonTrack.Free).Forget());
            _paidButton.onClick.AddListener(() => ClaimAsync(SeasonTrack.Premium).Forget());

            await LoadIconsAsync(cToken);

            Refresh();
        }

        /// <summary>
        /// Re-reads claim state and repaints.
        /// </summary>
        public void Refresh()
        {
            if (_service?.Season == null) return;

            var free = Find(SeasonTrack.Free);
            var paid = Find(SeasonTrack.Premium);

            SetSlot(_freeButton, _freeIcon, free, null);
            SetSlot(_paidButton, _paidIcon, paid, _lockedOverlay);

            if (_claimedOverlay != null)
            {
                _claimedOverlay.SetActive(free.HasValue && !_service.IsClaimable(free.Value) &&
                                          _tier <= _service.Snapshot.Progress.Tier);
            }
        }

        private void SetSlot(Button button, Image icon, SeasonRewardRef? reward, GameObject lockedOverlay)
        {
            var exists = reward.HasValue;
            var claimable = exists && _service.IsClaimable(reward.Value);

            button.gameObject.SetActive(exists);
            button.interactable = claimable;

            if (icon != null)
            {
                // Dimmed, not hidden: the point of the paid column is that it is visible.
                icon.color = claimable ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }

            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(exists && !_service.OwnsTrack(reward.Value.Track));
            }
        }

        private SeasonRewardRef? Find(SeasonTrack track)
        {
            foreach (var reward in _service.Season.GetRewards(_tier))
            {
                if (reward != null && reward.IsValid && reward.Track == track)
                {
                    return new SeasonRewardRef(_service.Season.Id, _tier, track, reward.RewardId);
                }
            }

            return null;
        }

        private async UniTask LoadIconsAsync(CancellationToken cToken)
        {
            await UniTask.WhenAll(LoadIconAsync(_freeIconLoader, SeasonTrack.Free, cToken),
                LoadIconAsync(_paidIconLoader, SeasonTrack.Premium, cToken));
        }

        private async UniTask LoadIconAsync(ImageSpriteLoader loader, SeasonTrack track,
            CancellationToken cToken)
        {
            if (loader == null) return;

            var address = AddressFor(track);

            if (string.IsNullOrEmpty(address)) return;

            await loader.LoadKeyAsync(address, cToken);
        }

        private string AddressFor(SeasonTrack track)
        {
            foreach (var reward in _service.Season.GetRewards(_tier))
            {
                if (reward != null && reward.Track == track) return reward.IconAddress;
            }

            return null;
        }

        private async UniTaskVoid ClaimAsync(SeasonTrack track)
        {
            var reward = Find(track);

            if (!reward.HasValue) return;

            // The result is worth surfacing rather than ignoring: GrantFailed means the reward
            // is still owed and will be retried, which is a very different message from
            // AlreadyClaimed.
            var result = await _service.ClaimAsync(reward.Value, this.GetCancellationTokenOnDestroy());

            if (result != ClaimResult.Claimed) Debug.Log($"[SeasonPass] Claim refused: {result}.");

            Refresh();
        }
    }
}
