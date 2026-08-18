using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace UniTx.Store.Samples
{
    /// <summary>
    /// A shop screen: header with the store name, one scrollable feed of offers grouped
    /// under their section headers, and a claim/buy/watch button per row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements <see cref="IWidget"/> so it can be pushed onto the kit's widget stack, and
    /// repaints from <see cref="UniStore.OnChanged"/> rather than polling in
    /// <c>Update</c> — the shop changes a handful of times per session, and a per-frame
    /// rebuild of a list of rows is exactly the cost the mobile budget forbids.
    /// </para>
    /// <para>
    /// The feed follows the researched shop pattern: offers keep their content order, so a
    /// designer puts high-conversion daily deals first and the free offer last — the
    /// arrangement that maximizes scroll-through and repeat visits.
    /// </para>
    /// <para>
    /// Restyle it, do not extend it. The value here is the wiring: which state each control
    /// reads, and what it does with a refusal.
    /// </para>
    /// </remarks>
    public sealed class StoreScreenSample : MonoBehaviour, IWidget
    {
        [Header("Header")]
        [SerializeField] private Text _storeNameLabel;

        [Header("List")]
        [SerializeField] private StoreOfferCell _offerPrefab;
        [SerializeField] private RectTransform _listContent;

        /// <summary>Gets the store name label.</summary>
        public Text StoreNameLabel => _storeNameLabel;

        /// <summary>Gets the offer row prefab.</summary>
        public StoreOfferCell OfferPrefab => _offerPrefab;

        /// <summary>Gets the list content transform.</summary>
        public RectTransform ListContent => _listContent;

        private readonly List<StoreOfferCell> _rows = new();
        private readonly List<RectTransform> _sectionHeaders = new();

        private IStoreService _service;
        private CancellationTokenSource _cts;

        /// <inheritdoc />
        public GameObject GameObject => gameObject;

        /// <inheritdoc />
        public Transform Transform => transform;

        /// <summary>
        /// Binds the screen to a service and builds the list.
        /// </summary>
        /// <param name="service">The store service to render.</param>
        /// <param name="cToken">Token to cancel building.</param>
        public async UniTask BindAsync(IStoreService service, CancellationToken cToken = default)
        {
            _service = service;

            await BuildRowsAsync(cToken);

            // Nothing else drives selection, so a store replaced server-side only notices
            // here. It never steals a claim button's tap — a fresh store is surfaced, not
            // auto-collected; only a claim that failed earlier is retried.
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
            if (UniStore.Service != null) BindAsync(UniStore.Service, _cts.Token).Forget();
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

            foreach (var header in _sectionHeaders)
            {
                Destroy(header.gameObject);
            }

            _sectionHeaders.Clear();

            var store = _service?.Store;

            if (store == null || _offerPrefab == null || _listContent == null) return;

            var currentSection = string.Empty;
            var wroteHeader = false;

            foreach (var offer in store.Offers)
            {
                if (offer == null || !offer.IsValid) continue;

                // A new section gets a header row — a plain text label parented into the
                // same content rect. The screen keeps it cheap: one label per section, no
                // prefab required.
                var section = offer.Section ?? string.Empty;

                if (!string.Equals(section, currentSection, StringComparison.Ordinal))
                {
                    currentSection = section;
                    wroteHeader = false;
                }

                if (!string.IsNullOrEmpty(section) && !wroteHeader)
                {
                    wroteHeader = true;
                    CreateSectionHeader(section);
                }

                var row = Instantiate(_offerPrefab, _listContent);

                await row.BindAsync(_service, offer.Id, cToken);

                _rows.Add(row);
            }
        }

        private void CreateSectionHeader(string section)
        {
            var header = new GameObject("Section: " + section, typeof(RectTransform),
                typeof(Text));

            header.transform.SetParent(_listContent, false);

            var text = header.GetComponent<Text>();
            text.text = section;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;

            _sectionHeaders.Add(header.GetComponent<RectTransform>());
        }

        private void OnSnapshotChanged(StoreSnapshot snapshot) => Repaint(snapshot);

        private void Repaint(StoreSnapshot snapshot)
        {
            var store = _service?.Store;

            if (_storeNameLabel != null)
            {
                _storeNameLabel.text = store != null ? store.DisplayName : "No shop";
            }

            foreach (var row in _rows)
            {
                row.Refresh();
            }
        }
    }
}
