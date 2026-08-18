using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace UniTx.Ladder.Samples
{
    /// <summary>
    /// A ladder screen: header with the ladder name and total steps, a progress bar toward
    /// the next rung, and one row per rung.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements <see cref="IWidget"/> so it can be pushed onto the kit's widget stack, and
    /// repaints from <see cref="UniLadder.OnChanged"/> rather than polling in
    /// <c>Update</c> — the climb changes a handful of times per session, and a per-frame
    /// rebuild of a list of rows is exactly the cost the mobile budget forbids.
    /// </para>
    /// <para>
    /// Restyle it, do not extend it. The value here is the wiring: which state each control
    /// reads, and what it does with a refusal.
    /// </para>
    /// </remarks>
    public sealed class LadderScreenSample : MonoBehaviour, IWidget
    {
        [Header("Header")]
        [SerializeField] private Text _ladderNameLabel;
        [SerializeField] private Text _stepsLabel;

        [Header("Progress")]
        [SerializeField] private Image _progressFill;
        [SerializeField] private Text _progressLabel;

        [Header("List")]
        [SerializeField] private LadderRungCell _rungPrefab;
        [SerializeField] private RectTransform _listContent;

        /// <summary>Gets the ladder name label.</summary>
        public Text LadderNameLabel => _ladderNameLabel;

        /// <summary>Gets the total steps label.</summary>
        public Text StepsLabel => _stepsLabel;

        /// <summary>Gets the progress bar fill.</summary>
        public Image ProgressFill => _progressFill;

        /// <summary>Gets the progress label.</summary>
        public Text ProgressLabel => _progressLabel;

        /// <summary>Gets the rung row prefab.</summary>
        public LadderRungCell RungPrefab => _rungPrefab;

        /// <summary>Gets the list content transform.</summary>
        public RectTransform ListContent => _listContent;

        private readonly List<LadderRungCell> _rows = new();

        private ILadderService _service;
        private CancellationTokenSource _cts;

        /// <inheritdoc />
        public GameObject GameObject => gameObject;

        /// <inheritdoc />
        public Transform Transform => transform;

        /// <summary>
        /// Binds the screen to a service and builds the list.
        /// </summary>
        /// <param name="service">The ladder service to render.</param>
        /// <param name="cToken">Token to cancel building.</param>
        public async UniTask BindAsync(ILadderService service, CancellationToken cToken = default)
        {
            _service = service;

            await BuildRowsAsync(cToken);

            // Nothing else drives the selection, so a ladder replaced server-side only
            // notices here. It never steals a claim button's tap — a fresh ladder is
            // surfaced, not auto-collected; only a claim that failed earlier is retried.
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
            if (UniLadder.Service != null) BindAsync(UniLadder.Service, _cts.Token).Forget();
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

            var ladder = _service?.Ladder;

            if (ladder == null || _rungPrefab == null || _listContent == null) return;

            for (var index = 0; index < ladder.Rungs.Count; index++)
            {
                var row = Instantiate(_rungPrefab, _listContent);

                await row.BindAsync(_service, ladder.Rungs[index].Id, cToken);

                _rows.Add(row);
            }
        }

        private void OnSnapshotChanged(LadderSnapshot snapshot) => Repaint(snapshot);

        private void Repaint(LadderSnapshot snapshot)
        {
            var ladder = _service?.Ladder;

            if (_ladderNameLabel != null)
            {
                _ladderNameLabel.text = ladder != null ? ladder.DisplayName : "No ladder";
            }

            if (_stepsLabel != null) _stepsLabel.text = $"{snapshot.TotalSteps} steps";

            if (_progressFill != null)
            {
                _progressFill.fillAmount = snapshot.Progress;

                // Dimmed while nothing is loaded, so an empty ladder does not look like
                // zero progress on a real one.
                _progressFill.color = snapshot.LadderId == null
                    ? new Color(1f, 1f, 1f, 0.25f)
                    : Color.white;
            }

            if (_progressLabel != null)
            {
                _progressLabel.text = snapshot.IsComplete
                    ? "Complete!"
                    : snapshot.NextRungSteps == 0
                        ? "—"
                        : $"{snapshot.TotalSteps}/{snapshot.NextRungSteps}";
            }

            foreach (var row in _rows)
            {
                row.Refresh();
            }
        }
    }
}
