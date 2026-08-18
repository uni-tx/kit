using System;
using UnityEngine;
using UnityEngine.UI;

namespace UniTx.Economy.Samples
{
    /// <summary>
    /// One row in the economy wallet: currency name, balance, and a buy button per purchase
    /// that costs that currency.
    /// </summary>
    /// <remarks>
    /// A plain row, not a widget: the wallet screen builds one per currency. Restyle it,
    /// do not extend it — the value here is which state each control reads.
    /// </remarks>
    public sealed class EconomyCurrencyRow : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private Text _currencyNameLabel;
        [SerializeField] private Text _balanceLabel;

        [Header("Actions")]
        [SerializeField] private Button _exchangeButton;
        [SerializeField] private Text _exchangeLabel;
        [SerializeField] private Button _buyButton;
        [SerializeField] private Text _buyLabel;

        private IEconomyService _service;
        private string _economyId;
        private string _currencyId;

        /// <summary>Gets the currency name label.</summary>
        public Text CurrencyNameLabel => _currencyNameLabel;

        /// <summary>Gets the balance label.</summary>
        public Text BalanceLabel => _balanceLabel;

        /// <summary>Gets the exchange button.</summary>
        public Button ExchangeButton => _exchangeButton;

        /// <summary>Gets the exchange label.</summary>
        public Text ExchangeLabel => _exchangeLabel;

        /// <summary>Gets the buy button.</summary>
        public Button BuyButton => _buyButton;

        /// <summary>Gets the buy label.</summary>
        public Text BuyLabel => _buyLabel;

        /// <summary>
        /// Binds the row to a currency and its first exchange rule and purchase.
        /// </summary>
        /// <param name="service">The economy service.</param>
        /// <param name="economyId">The economy this currency belongs to.</param>
        /// <param name="currencyId">The currency to show.</param>
        /// <param name="exchangeRuleId">An exchange rule spending this currency, or null.</param>
        /// <param name="purchaseId">A purchase costing this currency, or null.</param>
        public void Bind(IEconomyService service, string economyId, string currencyId,
            string exchangeRuleId, string purchaseId)
        {
            _service = service;
            _economyId = economyId;
            _currencyId = currencyId;

            if (_currencyNameLabel != null) _currencyNameLabel.text = currencyId;

            if (_exchangeButton != null)
            {
                _exchangeButton.gameObject.SetActive(exchangeRuleId != null);

                if (exchangeRuleId != null)
                {
                    _exchangeButton.onClick.RemoveAllListeners();
                    _exchangeButton.onClick.AddListener(() => Exchange(exchangeRuleId));
                }
            }

            if (_buyButton != null)
            {
                _buyButton.gameObject.SetActive(purchaseId != null);

                if (purchaseId != null)
                {
                    _buyButton.onClick.RemoveAllListeners();
                    _buyButton.onClick.AddListener(() => Buy(purchaseId));
                }
            }

            Repaint();
        }

        /// <summary>
        /// Refreshes the balance and button labels from the current snapshot.
        /// </summary>
        public void Repaint()
        {
            if (_service == null) return;

            var snapshot = _service.GetSnapshot(_economyId);

            foreach (var currency in snapshot.Currencies)
            {
                if (!string.Equals(currency.CurrencyId, _currencyId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (_balanceLabel != null) _balanceLabel.text = currency.Balance.ToString();

                break;
            }
        }

        private void Exchange(string ruleId)
        {
            // A fixed amount per tap; the rule's minimum makes the floor explicit.
            _service.ExchangeAsync(_economyId, ruleId, 10, $"row-{Guid.NewGuid():N}")
                .Forget();
        }

        private void Buy(string purchaseId)
        {
            _service.PurchaseAsync(_economyId, purchaseId, $"row-{Guid.NewGuid():N}")
                .Forget();
        }
    }
}
