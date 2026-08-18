using UniTx.Events;

namespace UniTx.Economy
{
    /// <summary>
    /// What an exchange attempt resolved to.
    /// </summary>
    public enum ExchangeResult
    {
        /// <summary>
        /// The exchange went through.
        /// </summary>
        Exchanged = 0,

        /// <summary>
        /// The source currency had too little balance.
        /// </summary>
        InsufficientBalance = 1,

        /// <summary>
        /// The amount was outside the rule's minimum/maximum bounds.
        /// </summary>
        AmountOutOfRange = 2,

        /// <summary>
        /// No exchange rule matched the currency pair.
        /// </summary>
        NoRule = 3,

        /// <summary>
        /// The same exchange id was applied before; nothing was moved.
        /// </summary>
        Duplicate = 4,

        /// <summary>
        /// The economy, rule or a currency was not found.
        /// </summary>
        Invalid = 5,
    }

    /// <summary>
    /// What a virtual purchase attempt resolved to.
    /// </summary>
    public enum PurchaseResult
    {
        /// <summary>
        /// The costs were charged and the rewards granted.
        /// </summary>
        Purchased = 0,

        /// <summary>
        /// One of the costs exceeded the player's balance.
        /// </summary>
        InsufficientBalance = 1,

        /// <summary>
        /// The same purchase id was applied before; nothing was charged or granted.
        /// </summary>
        Duplicate = 2,

        /// <summary>
        /// The economy or purchase was not found.
        /// </summary>
        Invalid = 3,

        /// <summary>
        /// The costs were charged but a reward could not be delivered; the purchase
        /// is recorded as pending and retried on the next refresh.
        /// </summary>
        RewardFailed = 4,
    }

    /// <summary>
    /// Raised after a currency exchange completed.
    /// </summary>
    public readonly struct CurrencyExchanged : IEvent
    {
        /// <summary>
        /// The economy the exchange happened in.
        /// </summary>
        public readonly string EconomyId;

        /// <summary>
        /// The source currency id.
        /// </summary>
        public readonly string FromCurrencyId;

        /// <summary>
        /// The target currency id.
        /// </summary>
        public readonly string ToCurrencyId;

        /// <summary>
        /// How much was converted.
        /// </summary>
        public readonly int Amount;

        /// <summary>
        /// How much the target currency received.
        /// </summary>
        public readonly int Received;

        /// <summary>
        /// The idempotency id of the exchange.
        /// </summary>
        public readonly string ExchangeId;

        public CurrencyExchanged(string economyId, string fromCurrencyId, string toCurrencyId,
            int amount, int received, string exchangeId)
        {
            EconomyId = economyId;
            FromCurrencyId = fromCurrencyId;
            ToCurrencyId = toCurrencyId;
            Amount = amount;
            Received = received;
            ExchangeId = exchangeId;
        }
    }

    /// <summary>
    /// Raised after a virtual purchase completed.
    /// </summary>
    public readonly struct PurchaseCompleted : IEvent
    {
        /// <summary>
        /// The economy the purchase was made in.
        /// </summary>
        public readonly string EconomyId;

        /// <summary>
        /// The purchase id.
        /// </summary>
        public readonly string PurchaseId;

        /// <summary>
        /// The idempotency id of the purchase.
        /// </summary>
        public readonly string PurchaseKey;

        public PurchaseCompleted(string economyId, string purchaseId, string purchaseKey)
        {
            EconomyId = economyId;
            PurchaseId = purchaseId;
            PurchaseKey = purchaseKey;
        }
    }

    /// <summary>
    /// Raised when a purchase could not deliver its rewards; it stays pending.
    /// </summary>
    public readonly struct PurchaseDeliveryFailed : IEvent
    {
        /// <summary>
        /// The economy the purchase belongs to.
        /// </summary>
        public readonly string EconomyId;

        /// <summary>
        /// The purchase id.
        /// </summary>
        public readonly string PurchaseId;

        public PurchaseDeliveryFailed(string economyId, string purchaseId)
        {
            EconomyId = economyId;
            PurchaseId = purchaseId;
        }
    }

    /// <summary>
    /// One currency's balance as the wallet screen needs it.
    /// </summary>
    public readonly struct EconomyCurrencySnapshot
    {
        /// <summary>
        /// The currency id.
        /// </summary>
        public readonly string CurrencyId;

        /// <summary>
        /// The player's balance.
        /// </summary>
        public readonly int Balance;

        /// <summary>
        /// The currency's configured maximum, or zero when uncapped.
        /// </summary>
        public readonly int MaxBalance;

        public EconomyCurrencySnapshot(string currencyId, int balance, int maxBalance)
        {
            CurrencyId = currencyId;
            Balance = balance;
            MaxBalance = maxBalance;
        }
    }

    /// <summary>
    /// One exchange rule, as the UI needs it.
    /// </summary>
    public readonly struct ExchangeRuleSnapshot
    {
        /// <summary>
        /// The rule id.
        /// </summary>
        public readonly string RuleId;

        /// <summary>
        /// The source currency id.
        /// </summary>
        public readonly string FromCurrencyId;

        /// <summary>
        /// The target currency id.
        /// </summary>
        public readonly string ToCurrencyId;

        /// <summary>
        /// How much target currency one source unit buys.
        /// </summary>
        public readonly int Rate;

        public ExchangeRuleSnapshot(string ruleId, string fromCurrencyId, string toCurrencyId,
            int rate)
        {
            RuleId = ruleId;
            FromCurrencyId = fromCurrencyId;
            ToCurrencyId = toCurrencyId;
            Rate = rate;
        }
    }

    /// <summary>
    /// One purchase, as the UI needs it.
    /// </summary>
    public readonly struct PurchaseSnapshot
    {
        /// <summary>
        /// The purchase id.
        /// </summary>
        public readonly string PurchaseId;

        /// <summary>
        /// The purchase's display name.
        /// </summary>
        public readonly string DisplayName;

        /// <summary>
        /// The costs, as "currencyId x amount" display strings.
        /// </summary>
        public readonly string CostSummary;

        public PurchaseSnapshot(string purchaseId, string displayName, string costSummary)
        {
            PurchaseId = purchaseId;
            DisplayName = displayName;
            CostSummary = costSummary;
        }
    }

    /// <summary>
    /// A read-only view of one economy: its currencies with balances, exchange rules
    /// and purchases.
    /// </summary>
    public readonly struct EconomySnapshot
    {
        /// <summary>
        /// The economy id.
        /// </summary>
        public readonly string EconomyId;

        /// <summary>
        /// The economy's display name.
        /// </summary>
        public readonly string DisplayName;

        /// <summary>
        /// The currencies in this economy, with current balances.
        /// </summary>
        public readonly EconomyCurrencySnapshot[] Currencies;

        /// <summary>
        /// The exchange rules in this economy.
        /// </summary>
        public readonly ExchangeRuleSnapshot[] ExchangeRules;

        /// <summary>
        /// The purchases in this economy.
        /// </summary>
        public readonly PurchaseSnapshot[] Purchases;

        public EconomySnapshot(string economyId, string displayName,
            EconomyCurrencySnapshot[] currencies, ExchangeRuleSnapshot[] exchangeRules,
            PurchaseSnapshot[] purchases)
        {
            EconomyId = economyId;
            DisplayName = displayName;
            Currencies = currencies;
            ExchangeRules = exchangeRules;
            Purchases = purchases;
        }
    }
}
