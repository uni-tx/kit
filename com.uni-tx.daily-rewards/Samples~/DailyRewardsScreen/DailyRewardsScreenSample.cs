using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace UniTx.DailyRewards.Samples
{
    /// <summary>
    /// A daily rewards screen: header, one cell per day, and a claim button that only
    /// appears on today's slot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements <see cref="IWidget"/> so it can be pushed onto the kit's widget stack, and
    /// repaints from <see cref="UniDailyRewards.OnChanged"/> rather than polling in
    /// <c>Update</c> — the calendar changes a handful of times per session, and a per-frame
    /// rebuild of a week of cells is exactly the cost the mobile budget forbids.
    /// </para>
    /// <para>
    /// Restyle it, do not extend it. The value here is the wiring: which state each control
    /// reads, and what it does with a refusal.
    /// </para>
    /// </remarks>
    public sealed class DailyRewardsScreenSample : MonoBehaviour, IWidget
    {
        [Header("Header")]
        [SerializeField] private Text _calendarNameLabel;
        [SerializeField] private Text _streakLabel;
        [SerializeField] private Text _countdownLabel;

        [Header("Calendar")]
        [SerializeField] private DailyRewardsDayCell _dayCellPrefab;
        [SerializeField] private RectTransform _calendarContent;

        [Header("Actions")]
        [SerializeField] private Button _claimButton;
        [SerializeField] private Text _claimBadge;

        /// <summary>Gets the calendar name label.</summary>
        public Text CalendarNameLabel => _calendarNameLabel;

        /// <summary>Gets the streak label.</summary>
        public Text StreakLabel => _streakLabel;

        /// <summary>Gets the countdown label.</summary>
        public Text CountdownLabel => _countdownLabel;

        /// <summary>Gets the day cell prefab.</summary>
        public DailyRewardsDayCell DayCellPrefab => _dayCellPrefab;

        /// <summary>Gets the calendar content transform.</summary>
        public RectTransform CalendarContent => _calendarContent;

        /// <summary>Gets the claim button.</summary>
        public Button ClaimButton => _claimButton;

        /// <summary>Gets the claim badge label.</summary>
        public Text ClaimBadge => _claimBadge;

        private readonly List<DailyRewardsDayCell> _cells = new();

        private IDailyRewardsService _service;
        private CancellationTokenSource _cts;

        /// <inheritdoc />
        public GameObject GameObject => gameObject;

        /// <inheritdoc />
        public Transform Transform => transform;

        /// <summary>
        /// Binds the screen to a service and builds the calendar.
        /// </summary>
        /// <param name="service">The daily rewards service to render.</param>
        /// <param name="cToken">Token to cancel building.</param>
        public async UniTask BindAsync(IDailyRewardsService service, CancellationToken cToken = default)
        {
            _service = service;

            await BuildCellsAsync(cToken);

            // Nothing else drives the passage of time, so a session left open across the
            // reset hour only notices here. It never steals the claim button's tap — a fresh
            // claimable day is surfaced, not auto-collected; only a claim that failed earlier
            // today is retried.
            await service.RefreshAsync(cToken);

            service.OnChanged += OnSnapshotChanged;

            Repaint(service.Snapshot);
        }

        /// <inheritdoc />
        public void Initialize()
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            _claimButton.onClick.AddListener(() => ClaimAsync().Forget());

            // Resolved through the facade so the screen works whether the game installed the
            // service into the container, the facade, or both.
            if (UniDailyRewards.Service != null) BindAsync(UniDailyRewards.Service, _cts.Token).Forget();
        }

        /// <inheritdoc />
        public void Reset()
        {
            if (_service != null) _service.OnChanged -= OnSnapshotChanged;

            _claimButton.onClick.RemoveAllListeners();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void OnDestroy() => Reset();

        private async UniTask BuildCellsAsync(CancellationToken cToken)
        {
            foreach (var cell in _cells)
            {
                Destroy(cell.gameObject);
            }

            _cells.Clear();

            var calendar = _service?.Calendar;

            if (calendar == null || _dayCellPrefab == null || _calendarContent == null) return;

            for (var index = 0; index < calendar.SlotCount; index++)
            {
                var cell = Instantiate(_dayCellPrefab, _calendarContent);

                await cell.BindAsync(_service, index, cToken);

                _cells.Add(cell);
            }
        }

        private void OnSnapshotChanged(DailyRewardsSnapshot snapshot) => Repaint(snapshot);

        private void Repaint(DailyRewardsSnapshot snapshot)
        {
            var calendar = _service?.Calendar;

            if (_calendarNameLabel != null)
            {
                _calendarNameLabel.text = calendar != null ? calendar.DisplayName : "No calendar";
            }

            if (_streakLabel != null) _streakLabel.text = $"Streak: {snapshot.Streak}";

            if (_countdownLabel != null) _countdownLabel.text = FormatCountdown(snapshot);

            if (_claimBadge != null && snapshot.CurrentSlot != null)
            {
                _claimBadge.text = $"Day {snapshot.CurrentSlot.Day}";
            }

            _claimButton.gameObject.SetActive(snapshot.State == DailyRewardsState.Claimable);

            foreach (var cell in _cells)
            {
                cell.Refresh();
            }
        }

        private static string FormatCountdown(DailyRewardsSnapshot snapshot) => snapshot.State switch
        {
            DailyRewardsState.None => "No calendar",
            DailyRewardsState.Finished => "All claimed",
            DailyRewardsState.Claimable => "Claim now",
            _ when snapshot.RemainingSeconds >= 3600 =>
                $"{snapshot.RemainingSeconds / 3600}h {(snapshot.RemainingSeconds % 3600) / 60}m",
            _ => $"{Mathf.Max(1, snapshot.RemainingSeconds / 60)}m",
        };

        private async UniTaskVoid ClaimAsync()
        {
            _claimButton.interactable = false;

            try
            {
                var result = await _service.ClaimAsync(_cts.Token);

                if (result != DailyClaimResult.Claimed)
                {
                    // GrantFailed is the interesting one: the reward is still owed and will be
                    // retried on the next refresh — a very different message from
                    // AlreadyClaimed.
                    Debug.Log($"[DailyRewards] Claim refused: {result}.");
                }
            }
            catch (Exception exception)
            {
                // Fire-and-forget logs its own failures (async-unity.md) — a granter that
                // throws is a bug in the game's economy code, not a reason to drop the error.
                Debug.LogError($"[DailyRewards] Claim failed: {exception}");
            }
            finally
            {
                // Re-enabled in a finally so a granter that refuses does not leave the player
                // staring at a permanently dead button.
                _claimButton.interactable = true;
            }
        }
    }
}
