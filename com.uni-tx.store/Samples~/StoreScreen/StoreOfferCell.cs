using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Iap;
using UniTx.SpriteLoader;
using UnityEngine;
using UnityEngine.UI;

namespace UniTx.Store.Samples
{
    /// <summary>
    /// One offer on the shop: the offer icon, name, rewards, price, and an action button
    /// that reads the offer kind — Buy (IAP, price from the store), Claim (free), or
    /// Watch (rewarded ad).
    /// </summary>
    /// <remarks>
    /// The price of an IAP offer is read from <see cref="UniIap.GetPrice"/> — the store
    /// returns the player's own currency and formatting, and showing a price that differs
    /// from the payment sheet is a store-review rejection. Free and rewarded offers fall
    /// back to their content price hint.
    /// </remarks>
    public sealed class StoreOfferCell : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private ImageSpriteLoader _iconLoader;
        [SerializeField] private Text _nameLabel;
        [SerializeField] private Text _sectionLabel;
        [SerializeField] private Text _badgeLabel;
        [SerializeField] private Text _rewardsLabel;
        [SerializeField] private Button _actionButton;
        [SerializeField] private Text _actionLabel;
        [SerializeField] private GameObject _claimedOverlay;

        /// <summary>Gets the offer icon image.</summary>
        public Image Icon => _icon;

        /// <summary>Gets the offer icon loader.</summary>
        public ImageSpriteLoader IconLoader => _iconLoader;

        /// <summary>Gets the offer name label.</summary>
        public Text NameLabel => _nameLabel;

        /// <summary>Gets the section header label.</summary>
        public Text SectionLabel => _sectionLabel;

        /// <summary>Gets the badge label.</summary>
        public Text BadgeLabel => _badgeLabel;

        /// <summary>Gets the rewards label.</summary>
        public Text RewardsLabel => _rewardsLabel;

        /// <summary>Gets the action button.</summary>
        public Button ActionButton => _actionButton;

        /// <summary>Gets the action button label.</summary>
        public Text ActionLabel => _actionLabel;

        /// <summary>Gets the claimed overlay.</summary>
        public GameObject ClaimedOverlay => _claimedOverlay;

        private IStoreService _service;
        private string _offerId;

        /// <summary>
        /// Gets the offer id this row renders.
        /// </summary>
        public string OfferId => _offerId;

        /// <summary>
        /// Binds the row to an offer and refreshes it.
        /// </summary>
        /// <param name="service">The store service to read and claim through.</param>
        /// <param name="offerId">The offer id.</param>
        /// <param name="cToken">Token to cancel icon loading.</param>
        public async UniTask BindAsync(IStoreService service, string offerId,
            CancellationToken cToken = default)
        {
            _service = service;
            _offerId = offerId;

            var offer = service.Store?.GetOffer(offerId);

            if (_nameLabel != null && offer != null) _nameLabel.text = offer.DisplayName;
            if (_sectionLabel != null && offer != null) _sectionLabel.text = offer.Section;

            if (_badgeLabel != null && offer != null)
            {
                _badgeLabel.gameObject.SetActive(offer.Badge != StoreBadge.None);
                _badgeLabel.text = FormatBadge(offer.Badge);
            }

            if (_rewardsLabel != null && offer != null) _rewardsLabel.text = FormatRewards(offer);

            if (_actionLabel != null && offer != null)
            {
                _actionLabel.text = offer.Kind switch
                {
                    StoreOfferKind.Iap => UniIap.GetPrice(offer.ProductId,
                        offer.PriceHint ?? "Buy"),
                    StoreOfferKind.Rewarded => "Watch",
                    _ => "Claim",
                };
            }

            _actionButton.onClick.RemoveAllListeners();
            _actionButton.onClick.AddListener(() => ClaimAsync().Forget());

            await LoadIconAsync(offer, cToken);

            Refresh();
        }

        /// <summary>
        /// Re-reads offer state and repaints.
        /// </summary>
        public void Refresh()
        {
            var snapshot = _service?.Snapshot ?? default;

            // The store is not loaded yet; nothing to render.
            if (_service == null || snapshot.StoreId == null) return;

            var offer = FindOffer(snapshot, _offerId);

            if (offer.OfferId == null) return;

            var ready = offer.State == StoreOfferState.Ready;

            _actionButton.gameObject.SetActive(ready);
            _actionButton.interactable = ready;

            if (_actionLabel != null && !ready)
            {
                _actionLabel.text = offer.State switch
                {
                    StoreOfferState.OnCooldown =>
                        $"Ready in {Mathf.CeilToInt(offer.CooldownRemainingSeconds)}s",
                    StoreOfferState.LimitReached => "Limit reached",
                    _ => "Claimed",
                };
            }

            if (_icon != null)
            {
                _icon.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }

            if (_claimedOverlay != null)
            {
                _claimedOverlay.SetActive(offer.State == StoreOfferState.LimitReached &&
                                          offer.ClaimCount > 0);
            }
        }

        private static StoreOfferSnapshot FindOffer(StoreSnapshot snapshot, string offerId)
        {
            foreach (var offer in snapshot.Offers)
            {
                if (string.Equals(offer.OfferId, offerId, StringComparison.Ordinal)) return offer;
            }

            return default;
        }

        private static string FormatBadge(StoreBadge badge) => badge switch
        {
            StoreBadge.BestValue => "Best Value",
            StoreBadge.New => "New",
            StoreBadge.Sale => "Sale",
            _ => string.Empty,
        };

        private static string FormatRewards(StoreOfferData offer)
        {
            var parts = new List<string>(offer.Rewards.Count);

            foreach (var reward in offer.Rewards)
            {
                if (reward == null) continue;

                parts.Add($"{reward.Amount}x {reward.ItemId}");
            }

            return string.Join(", ", parts);
        }

        private async UniTask LoadIconAsync(StoreOfferData offer, CancellationToken cToken)
        {
            if (_iconLoader == null || offer == null || string.IsNullOrEmpty(offer.IconAddress)) return;

            await _iconLoader.LoadKeyAsync(offer.IconAddress, cToken);
        }

        private async UniTaskVoid ClaimAsync()
        {
            try
            {
                var result = await _service.ClaimAsync(_offerId, this.GetCancellationTokenOnDestroy());

                if (result is StoreClaimResult.AdNotCompleted or StoreClaimResult.AdNotReady)
                {
                    Debug.Log($"[Store] Ad did not complete for '{_offerId}': {result}.");
                }
                else if (result is not (StoreClaimResult.Claimed or StoreClaimResult.Rewarded
                    or StoreClaimResult.Purchased))
                {
                    // GrantFailed is the interesting one: the reward is still owed and will
                    // be retried on the next refresh — a very different message from
                    // OnCooldown.
                    Debug.Log($"[Store] '{_offerId}' refused: {result}.");
                }

                Refresh();
            }
            catch (Exception exception)
            {
                // Fire-and-forget logs its own failures (async-unity.md) — a granter that
                // throws is a bug in the game's economy code, not a reason to drop the error.
                Debug.LogError($"[Store] Claim failed: {exception}");
            }
        }
    }
}
