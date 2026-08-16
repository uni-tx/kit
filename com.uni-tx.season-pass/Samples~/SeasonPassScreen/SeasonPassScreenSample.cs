using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace UniTx.SeasonPass.Samples
{
    /// <summary>
    /// A season pass screen: header, tier ladder, claim-all and buy buttons.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements <see cref="IWidget"/> so it can be pushed onto the kit's widget stack, and
    /// repaints from <see cref="ISeasonPassService.OnChanged"/> rather than polling in
    /// <c>Update</c> — the pass changes a handful of times per session, and a per-frame
    /// rebuild of a hundred tier cells is exactly the cost the mobile budget forbids.
    /// </para>
    /// <para>
    /// Restyle it, do not extend it. The value here is the wiring: which state each control
    /// reads, and what it does with a refusal.
    /// </para>
    /// </remarks>
    public sealed class SeasonPassScreenSample : MonoBehaviour, IWidget
    {
        [Header("Header")]
        [SerializeField] private Text _seasonNameLabel;
        [SerializeField] private Text _timeRemainingLabel;
        [SerializeField] private Text _tierLabel;
        [SerializeField] private Slider _tierProgressBar;

        [Header("Ladder")]
        [SerializeField] private SeasonPassTierCell _tierCellPrefab;
        [SerializeField] private RectTransform _ladderContent;

        [Header("Actions")]
        [SerializeField] private Button _claimAllButton;
        [SerializeField] private Text _claimAllBadge;
        [SerializeField] private Button _buyPassButton;
        [SerializeField] private Text _buyPassLabel;

        private readonly List<SeasonPassTierCell> _cells = new();

        private ISeasonPassService _service;
        private CancellationTokenSource _cts;

        /// <inheritdoc />
        public GameObject GameObject => gameObject;

        /// <inheritdoc />
        public Transform Transform => transform;

        /// <summary>
        /// Binds the screen to a service and builds the ladder.
        /// </summary>
        /// <param name="service">The season pass to render.</param>
        /// <param name="cToken">Token to cancel building.</param>
        public async UniTask BindAsync(ISeasonPassService service, CancellationToken cToken = default)
        {
            _service = service;

            await BuildLadderAsync(cToken);

            _service.OnChanged += OnSnapshotChanged;

            Repaint(_service.Snapshot);
        }

        /// <inheritdoc />
        public void Initialize()
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            _claimAllButton.onClick.AddListener(() => ClaimAllAsync().Forget());
            _buyPassButton.onClick.AddListener(() => BuyPassAsync().Forget());

            // Resolved through the facade so the screen works whether the game installed the
            // service into the container, the facade, or both.
            if (UniSeasonPass.Service != null) BindAsync(UniSeasonPass.Service, _cts.Token).Forget();
        }

        /// <inheritdoc />
        public void Reset()
        {
            if (_service != null) _service.OnChanged -= OnSnapshotChanged;

            _claimAllButton.onClick.RemoveAllListeners();
            _buyPassButton.onClick.RemoveAllListeners();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void OnDestroy() => Reset();

        private async UniTask BuildLadderAsync(CancellationToken cToken)
        {
            foreach (var cell in _cells)
            {
                Destroy(cell.gameObject);
            }

            _cells.Clear();

            var season = _service?.Season;

            if (season == null || _tierCellPrefab == null || _ladderContent == null) return;

            foreach (var tier in season.Tiers)
            {
                var cell = Instantiate(_tierCellPrefab, _ladderContent);

                await cell.BindAsync(_service, tier.Tier, cToken);

                _cells.Add(cell);
            }
        }

        private void OnSnapshotChanged(SeasonPassSnapshot snapshot) => Repaint(snapshot);

        private void Repaint(SeasonPassSnapshot snapshot)
        {
            var season = _service?.Season;

            if (_seasonNameLabel != null)
            {
                _seasonNameLabel.text = season != null ? season.DisplayName : "No active season";
            }

            if (_timeRemainingLabel != null) _timeRemainingLabel.text = FormatRemaining(snapshot);

            if (_tierLabel != null) _tierLabel.text = $"Tier {snapshot.Progress.Tier}";

            if (_tierProgressBar != null) _tierProgressBar.value = snapshot.Progress.Normalized;

            if (_claimAllBadge != null) _claimAllBadge.text = snapshot.ClaimableCount.ToString();

            _claimAllButton.gameObject.SetActive(snapshot.ClaimableCount > 0);

            var owned = snapshot.HighestOwnedTrack >= SeasonTrack.Premium;

            _buyPassButton.gameObject.SetActive(!owned && snapshot.IsEarning);

            if (_buyPassLabel != null && season != null)
            {
                var offer = season.GetOffer(SeasonTrack.Premium);

                _buyPassLabel.text = offer == null ? "Unavailable" : $"Unlock — {offer.CurrencyCost}";
            }

            foreach (var cell in _cells)
            {
                cell.Refresh();
            }
        }

        private static string FormatRemaining(SeasonPassSnapshot snapshot) => snapshot.Phase switch
        {
            SeasonPhase.NotStarted => "Starts soon",
            SeasonPhase.Grace => "Last chance to collect",
            SeasonPhase.Ended => "Season over",
            _ when snapshot.TimeRemaining.TotalDays >= 1 =>
                $"{(int)snapshot.TimeRemaining.TotalDays}d left",
            _ => $"{(int)snapshot.TimeRemaining.TotalHours}h left",
        };

        private async UniTaskVoid ClaimAllAsync()
        {
            _claimAllButton.interactable = false;

            try
            {
                var claimed = await _service.ClaimAllAsync(_cts.Token);

                Debug.Log($"[SeasonPass] Collected {claimed} rewards.");
            }
            finally
            {
                // Re-enabled in a finally so a granter that refuses does not leave the player
                // staring at a permanently dead button.
                _claimAllButton.interactable = true;
            }
        }

        private async UniTaskVoid BuyPassAsync()
        {
            _buyPassButton.interactable = false;

            try
            {
                var result = await _service.UnlockTrackAsync(SeasonTrack.Premium,
                    SeasonPassPayment.Currency, _cts.Token);

                // InsufficientFunds is the interesting one: it is where a real game opens the
                // currency store rather than showing an error.
                Debug.Log($"[SeasonPass] Unlock result: {result}.");
            }
            finally
            {
                _buyPassButton.interactable = true;
            }
        }
    }
}
