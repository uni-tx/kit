using System;
using UnityEngine;

namespace UniTx.Economy
{
    /// <summary>
    /// One cost line of a purchase: a currency and how much of it.
    /// </summary>
    [Serializable]
    public sealed class CostData
    {
        [Tooltip("The currency charged.")]
        [SerializeField] private string _currencyId;

        [Tooltip("How much of it the purchase costs.")]
        [SerializeField, Min(1)] private int _amount = 1;

        /// <summary>
        /// Gets the currency charged.
        /// </summary>
        public string CurrencyId => _currencyId;

        /// <summary>
        /// Gets how much the purchase costs.
        /// </summary>
        public int Amount => _amount;
    }
}
