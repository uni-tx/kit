using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace UniTx.Economy.Samples
{
    /// <summary>
    /// A wallet screen: one tab per economy, each listing its currencies with balances,
    /// an exchange button where a rule exists, and a buy button per purchase.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements <see cref="IWidget"/> so it can be pushed onto the kit's widget stack, and
    /// repaints on demand rather than polling in <c>Update</c> — the wallet changes a
    /// handful of times per session, and a per-frame rebuild of a list of rows is exactly
    /// the cost the mobile budget forbids.
    /// </para>
    /// <para>
    /// The tab strip is what the N-economy design looks like from the player's side: one
    /// economy per tab, isolated ledgers, no cross-contamination.
    /// </para>
    /// </remarks>
    public sealed class EconomyScreenSample : MonoBehaviour, IWidget
    {
        [Header("Header")]
        [SerializeField] private Text _economyNameLabel;

        [Header("Tabs")]
        [SerializeField] private Button _tabButtonPrefab;
        [SerializeField] private RectTransform _tabContent;

        [Header("List")]
        [SerializeField] private EconomyCurrencyRow _currencyRowPrefab;
        [SerializeField] private RectTransform _listContent;

        /// <summary>Gets the economy name label.</summary>
        public Text EconomyNameLabel => _economyNameLabel;

        /// <summary>Gets the tab button prefab.</summary>
        public Button TabButtonPrefab => _tabButtonPrefab;

        /// <summary>Gets the tab strip content transform.</summary>
        public RectTransform TabContent => _tabContent;

        /// <summary>Gets the currency row prefab.</summary>
        public EconomyCurrencyRow CurrencyRowPrefab => _currencyRowPrefab;

        /// <summary>Gets the list content transform.</summary>
        public RectTransform ListContent => _listContent;

        private readonly List<Button> _tabs = new();
        private readonly List<EconomyCurrencyRow> _rows = new();

        private IEconomyService _service;
        private string _selectedEconomyId;

        /// <inheritdoc />
        public GameObject GameObject => gameObject;

        /// <inheritdoc />
        public Transform Transform => transform;

        /// <summary>
        /// Binds the screen to a service and builds the tab strip.
        /// </summary>
        /// <param name="service">The economy service to render.</param>
        /// <param name="cToken">Token to cancel building.</param>
        public async UniTask BindAsync(IEconomyService service, CancellationToken cToken = default)
        {
            _service = service;

            BuildTabs(cToken);

            _selectedEconomyId = service.SelectedEconomyId ?? FirstEconomyId(service);

            if (_selectedEconomyId != null) service.SelectEconomy(_selectedEconomyId);

            BuildRows();

            Repaint();

            await UniTask.CompletedTask;
        }

        /// <summary>
        /// Repaints the current economy's rows.
        /// </summary>
        public void Repaint()
        {
            if (_service == null || _selectedEconomyId == null) return;

            var snapshot = _service.GetSnapshot(_selectedEconomyId);

            if (_economyNameLabel != null) _economyNameLabel.text = snapshot.DisplayName;

            for (var i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                var colors = tab.colors;
                colors.normalColor = tab.gameObject.activeSelf &&
                                     tab.name == _selectedEconomyId
                    ? new Color(1f, 0.92f, 0.6f)
                    : Color.white;
                tab.colors = colors;
            }

            foreach (var row in _rows)
            {
                row.Repaint();
            }
        }

        private static string FirstEconomyId(IEconomyService service)
        {
            var ids = service.GetEconomyIds();
            return ids.Count > 0 ? ids[0] : null;
        }

        private void BuildTabs(CancellationToken cToken)
        {
            foreach (var tab in _tabs)
            {
                if (tab != null) Destroy(tab.gameObject);
            }

            _tabs.Clear();

            if (_service == null || _tabButtonPrefab == null || _tabContent == null) return;

            foreach (var economyId in _service.GetEconomyIds())
            {
                cToken.ThrowIfCancellationRequested();

                var tab = Instantiate(_tabButtonPrefab, _tabContent);
                tab.name = economyId;

                var label = tab.GetComponentInChildren<Text>();
                if (label != null) label.text = economyId;

                tab.onClick.RemoveAllListeners();
                tab.onClick.AddListener(() => SelectEconomy(economyId));

                _tabs.Add(tab);
            }
        }

        private void SelectEconomy(string economyId)
        {
            _selectedEconomyId = economyId;

            _service?.SelectEconomy(economyId);

            BuildRows();
            Repaint();
        }

        private void BuildRows()
        {
            foreach (var row in _rows)
            {
                if (row != null) Destroy(row.gameObject);
            }

            _rows.Clear();

            if (_service == null || _selectedEconomyId == null || _currencyRowPrefab == null ||
                _listContent == null)
            {
                return;
            }

            var snapshot = _service.GetSnapshot(_selectedEconomyId);

            foreach (var currency in snapshot.Currencies)
            {
                var row = Instantiate(_currencyRowPrefab, _listContent);

                row.Bind(_service, _selectedEconomyId, currency.CurrencyId,
                    FirstRuleSpending(snapshot, currency.CurrencyId),
                    FirstPurchaseCosting(snapshot, currency.CurrencyId));

                _rows.Add(row);
            }
        }

        private static string FirstRuleSpending(EconomySnapshot snapshot, string currencyId)
        {
            foreach (var rule in snapshot.ExchangeRules)
            {
                if (string.Equals(rule.FromCurrencyId, currencyId, StringComparison.Ordinal))
                {
                    return rule.RuleId;
                }
            }

            return null;
        }

        private static string FirstPurchaseCosting(EconomySnapshot snapshot, string currencyId)
        {
            // Purchases do not carry their costs in the snapshot's summary, so the screen
            // matches the "{amount}x {currency}" cost line the summary is built from.
            var needle = $"x {currencyId}";

            foreach (var purchase in snapshot.Purchases)
            {
                if (purchase.CostSummary.Contains(needle, StringComparison.Ordinal))
                {
                    return purchase.PurchaseId;
                }
            }

            return null;
        }
    }
}
