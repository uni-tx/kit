using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace UniTx.Quests.Samples
{
    /// <summary>
    /// A quests screen: header with the set name and countdown, one row per quest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements <see cref="IWidget"/> so it can be pushed onto the kit's widget stack, and
    /// repaints from <see cref="UniQuests.OnChanged"/> rather than polling in
    /// <c>Update</c> — the board changes a handful of times per session, and a per-frame
    /// rebuild of a list of rows is exactly the cost the mobile budget forbids.
    /// </para>
    /// <para>
    /// Restyle it, do not extend it. The value here is the wiring: which state each control
    /// reads, and what it does with a refusal.
    /// </para>
    /// </remarks>
    public sealed class QuestsScreenSample : MonoBehaviour, IWidget
    {
        [Header("Header")]
        [SerializeField] private Text _setNameLabel;
        [SerializeField] private Text _countdownLabel;

        [Header("List")]
        [SerializeField] private QuestRowCell _rowPrefab;
        [SerializeField] private RectTransform _listContent;

        /// <summary>Gets the set name label.</summary>
        public Text SetNameLabel => _setNameLabel;

        /// <summary>Gets the countdown label.</summary>
        public Text CountdownLabel => _countdownLabel;

        /// <summary>Gets the quest row prefab.</summary>
        public QuestRowCell RowPrefab => _rowPrefab;

        /// <summary>Gets the list content transform.</summary>
        public RectTransform ListContent => _listContent;

        private readonly List<QuestRowCell> _rows = new();

        private IQuestsService _service;
        private CancellationTokenSource _cts;

        /// <inheritdoc />
        public GameObject GameObject => gameObject;

        /// <inheritdoc />
        public Transform Transform => transform;

        /// <summary>
        /// Binds the screen to a service and builds the list.
        /// </summary>
        /// <param name="service">The quests service to render.</param>
        /// <param name="cToken">Token to cancel building.</param>
        public async UniTask BindAsync(IQuestsService service, CancellationToken cToken = default)
        {
            _service = service;

            await BuildRowsAsync(cToken);

            // Nothing else drives the passage of time, so a session left open across the
            // reset hour only notices here. It never steals a claim button's tap — a fresh
            // board is surfaced, not auto-collected; only a claim that failed earlier is
            // retried.
            await service.RefreshAsync(cToken);

            service.OnChanged += OnSnapshotChanged;

            Repaint(service.Snapshot);
        }

        /// <inheritdoc />
        public void Initialize()
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            // Resolved through the facade so the screen works whether the game installed the
            // service into the container, the facade, or both.
            if (UniQuests.Service != null) BindAsync(UniQuests.Service, _cts.Token).Forget();
        }

        /// <inheritdoc />
        public void Reset()
        {
            if (_service != null) _service.OnChanged -= OnSnapshotChanged;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void OnDestroy() => Reset();

        private async UniTask BuildRowsAsync(CancellationToken cToken)
        {
            foreach (var row in _rows)
            {
                Destroy(row.gameObject);
            }

            _rows.Clear();

            var set = _service?.Set;

            if (set == null || _rowPrefab == null || _listContent == null) return;

            for (var index = 0; index < set.Quests.Count; index++)
            {
                var row = Instantiate(_rowPrefab, _listContent);

                await row.BindAsync(_service, set.Quests[index].Id, cToken);

                _rows.Add(row);
            }
        }

        private void OnSnapshotChanged(QuestsSnapshot snapshot) => Repaint(snapshot);

        private void Repaint(QuestsSnapshot snapshot)
        {
            var set = _service?.Set;

            if (_setNameLabel != null)
            {
                _setNameLabel.text = set != null ? set.DisplayName : "No quests";
            }

            if (_countdownLabel != null) _countdownLabel.text = FormatCountdown(snapshot);

            foreach (var row in _rows)
            {
                row.Refresh();
            }
        }

        private static string FormatCountdown(QuestsSnapshot snapshot)
        {
            if (snapshot.SetId == null) return "No quests";
            if (snapshot.NextResetUnix == 0) return "No resets";

            if (snapshot.RemainingSeconds >= 3600)
            {
                return $"Resets in {snapshot.RemainingSeconds / 3600}h " +
                       $"{(snapshot.RemainingSeconds % 3600) / 60}m";
            }

            return $"Resets in {Mathf.Max(1, snapshot.RemainingSeconds / 60)}m";
        }
    }
}
