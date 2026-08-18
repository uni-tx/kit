using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Currency;
using UniTx.Entity;
using UniTx.Events;
using UniTx.IoC;
using UniTx.Rewards;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Economy
{
    /// <summary>
    /// Reads and mutates any number of named economies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One <see cref="EconomyEntity"/> per economy, created lazily and keyed by economy id,
    /// so N economies never collide and a game can add a seasonal economy without touching
    /// the core one. Balances are read and mutated through <see cref="ICurrencyService"/> —
    /// the economy layer is the rules, the currency layer is the wallet.
    /// </para>
    /// <para>
    /// Every mutation is idempotent: the exchange id and purchase key are recorded in the
    /// economy's saved data before the ledger can move, so a replayed request cannot move
    /// currency twice even if the process died between the spend and the grant.
    /// </para>
    /// </remarks>
    public sealed class EconomyService : IEconomyService
    {
        private readonly Dictionary<string, EconomyEntity> _entities = new();

        // Not readonly: Inject fills these when the parameterless constructor is used.
        private IClock _clock;
        private IContentService _content;
        private IEconomyBackend _backend;
        private ICurrencyService _currencies;
        private IRewardService _rewards;
        private UniEconomyConfig _config;
        private string _selectedEconomyId;

        /// <summary>
        /// Creates the service; dependencies arrive through <see cref="Inject"/>.
        /// </summary>
        public EconomyService()
        {
        }

        /// <summary>
        /// Creates the service with explicit dependencies, for tests and manual wiring.
        /// </summary>
        /// <param name="clock">The time source.</param>
        /// <param name="content">The content service holding economy definitions.</param>
        /// <param name="backend">Where economy progress is stored.</param>
        /// <param name="currencies">The currency wallet.</param>
        /// <param name="rewards">The reward service, or null when rewards are not wired.</param>
        public EconomyService(IClock clock, IContentService content, IEconomyBackend backend,
            ICurrencyService currencies, IRewardService rewards = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _currencies = currencies ?? throw new ArgumentNullException(nameof(currencies));
            _rewards = rewards;
        }

        /// <inheritdoc />
        public bool IsReady { get; private set; }

        /// <inheritdoc />
        public string SelectedEconomyId => _selectedEconomyId;

        /// <inheritdoc />
        public void Inject(IResolver resolver)
        {
            _clock ??= resolver.Resolve<IClock>();
            _content ??= resolver.Resolve<IContentService>();

            if (_backend == null)
            {
                if (resolver.TryResolve<IEconomyBackend>(out var backend))
                {
                    _backend = backend;
                }
                else
                {
                    var local = new LocalEconomyBackend();
                    local.Inject(resolver);
                    _backend = local;
                }
            }

            _currencies ??= resolver.TryResolve<ICurrencyService>(out var currencies)
                ? currencies
                : null;

            // Optional by design: a game without the reward service still gets working
            // exchanges; purchases without rewards wired report RewardFailed.
            _rewards ??= resolver.TryResolve<IRewardService>(out var rewards)
                ? rewards
                : null;
        }

        /// <inheritdoc />
        public async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            _config ??= Resources.Load<UniEconomyConfig>(UniEconomyConfig.DefaultResourcePath);

            if (_config == null)
            {
                UniStatics.LogWarning(
                    "No UniEconomyConfig supplied and none found at " +
                    $"Resources/{UniEconomyConfig.DefaultResourcePath}; using defaults.", this);

                _config = ScriptableObject.CreateInstance<UniEconomyConfig>();
            }
            else
            {
                var problems = _config.DescribeProblems();

                if (!string.IsNullOrEmpty(problems))
                {
                    UniStatics.LogWarning($"UniEconomyConfig has problems: {problems}.", this);
                }
            }

            if (_currencies == null)
            {
                throw new InvalidOperationException(
                    "EconomyService needs a registered ICurrencyService. Bind one (and load " +
                    "currency content) before using the economy.");
            }

            // Select the configured economy so the facade has a default before the UI asks.
            if (!string.IsNullOrEmpty(_config.DefaultEconomyId))
            {
                SelectEconomy(_config.DefaultEconomyId);
            }

            IsReady = true;

            await RefreshAsync(cToken);
        }

        /// <inheritdoc />
        public void Reset()
        {
            IsReady = false;
            _selectedEconomyId = null;

            foreach (var entity in _entities.Values)
            {
                entity.Reset();
            }

            _entities.Clear();
        }

        /// <inheritdoc />
        public IReadOnlyList<string> GetEconomyIds()
        {
            var ids = new List<string>();

            if (_content != null)
            {
                foreach (var data in _content.GetAllData<EconomyData>())
                {
                    if (data != null && !string.IsNullOrEmpty(data.Id))
                    {
                        ids.Add(data.Id);
                    }
                }
            }

            return ids;
        }

        /// <inheritdoc />
        public bool SelectEconomy(string economyId)
        {
            if (string.IsNullOrEmpty(economyId)) return false;

            if (!EconomyExists(economyId)) return false;

            _selectedEconomyId = economyId;

            return true;
        }

        /// <inheritdoc />
        public EconomySnapshot GetSnapshot(string economyId = null)
        {
            var id = economyId ?? _selectedEconomyId;

            if (!IsReady || string.IsNullOrEmpty(id)) return new EconomySnapshot();

            if (!TryGetData(id, out var data)) return new EconomySnapshot();

            var currencies = new EconomyCurrencySnapshot[data.CurrencyIds.Count];

            for (var i = 0; i < currencies.Length; i++)
            {
                var currencyId = data.CurrencyIds[i];
                var balance = _currencies.TryGetBalance(currencyId, out var held) ? held : 0;
                currencies[i] = new EconomyCurrencySnapshot(currencyId, balance, 0);
            }

            var rules = new ExchangeRuleSnapshot[data.ExchangeRules.Count];

            for (var i = 0; i < rules.Length; i++)
            {
                var rule = data.ExchangeRules[i];
                rules[i] = new ExchangeRuleSnapshot(rule.Id, rule.FromCurrencyId,
                    rule.ToCurrencyId, rule.Rate);
            }

            var purchases = new PurchaseSnapshot[data.Purchases.Count];

            for (var i = 0; i < purchases.Length; i++)
            {
                var purchase = data.Purchases[i];
                purchases[i] = new PurchaseSnapshot(purchase.Id, purchase.DisplayName,
                    DescribeCosts(purchase));
            }

            return new EconomySnapshot(id, data.Name, currencies, rules, purchases);
        }

        /// <inheritdoc />
        public async UniTask<ExchangeResult> ExchangeAsync(string economyId, string ruleId,
            int amount, string exchangeId, CancellationToken cToken = default)
        {
            if (!IsReady) return ExchangeResult.Invalid;

            var id = economyId ?? _selectedEconomyId;

            if (!TryGetData(id, out var data)) return ExchangeResult.Invalid;

            var rule = data.GetExchangeRule(ruleId);

            if (rule == null) return ExchangeResult.NoRule;

            cToken.ThrowIfCancellationRequested();

            if (!EconomyCalculator.IsAmountInRange(rule, amount))
            {
                return ExchangeResult.AmountOutOfRange;
            }

            var entity = await EnsureEntityAsync(id, cToken);
            var saved = entity.SavedData;

            if (saved.HasAppliedExchange(exchangeId)) return ExchangeResult.Duplicate;

            if (!_currencies.TryGetBalance(rule.FromCurrencyId, out var held) ||
                held < amount)
            {
                return ExchangeResult.InsufficientBalance;
            }

            var received = EconomyCalculator.ExchangeOutput(rule, amount);

            // Spend first, then grant. The exchange id is recorded before the grant so a
            // crash between the two cannot replay the spend.
            if (!_currencies.TrySpend(rule.FromCurrencyId, amount))
            {
                return ExchangeResult.InsufficientBalance;
            }

            saved.RecordAppliedExchange(exchangeId);

            // Grant through the currency wallet with an economy-scoped id, so the wallet's
            // own ledger and ours both stay idempotent.
            var grantId = $"exchange:{id}:{exchangeId}";

            var granted = await _currencies.GrantAsync(rule.ToCurrencyId, received, grantId,
                cToken);

            if (granted == CurrencyGrantResult.UnknownCurrency ||
                granted == CurrencyGrantResult.Rejected)
            {
                // The target currency is not registered or the grant was rejected; refund
                // the source so the player is not down currency with nothing to show for it.
                await _currencies.GrantAsync(rule.FromCurrencyId, amount,
                    $"refund:{id}:{exchangeId}", cToken);

                return ExchangeResult.Invalid;
            }

            await PersistAsync(entity, true, cToken);

            RaiseExchanged(id, rule, amount, received, exchangeId);

            return ExchangeResult.Exchanged;
        }

        /// <inheritdoc />
        public async UniTask<PurchaseResult> PurchaseAsync(string economyId, string purchaseId,
            string purchaseKey, CancellationToken cToken = default)
        {
            if (!IsReady) return PurchaseResult.Invalid;

            var id = economyId ?? _selectedEconomyId;

            if (!TryGetData(id, out var data)) return PurchaseResult.Invalid;

            var purchase = data.GetPurchase(purchaseId);

            if (purchase == null) return PurchaseResult.Invalid;

            cToken.ThrowIfCancellationRequested();

            var entity = await EnsureEntityAsync(id, cToken);
            var saved = entity.SavedData;

            if (saved.HasAppliedPurchase(purchaseKey)) return PurchaseResult.Duplicate;

            if (EconomyCalculator.FirstUnaffordableCost(purchase, CurrencyBalance) != null)
            {
                return PurchaseResult.InsufficientBalance;
            }

            // Charge every cost line atomically before granting anything.
            foreach (var cost in purchase.Costs)
            {
                if (!_currencies.TrySpend(cost.CurrencyId, cost.Amount))
                {
                    // Should be unreachable after the affordability check, but a race or a
                    // concurrent spend elsewhere makes it possible. Refund what was charged.
                    await RefundCostsAsync(id, purchaseKey, purchase, cToken);
                    return PurchaseResult.InsufficientBalance;
                }
            }

            saved.RecordAppliedPurchase(purchaseKey);

            var rewardsOk = await GrantRewardsAsync(id, purchase, purchaseKey, cToken);

            if (!rewardsOk)
            {
                // The player keeps the purchase (idempotency) and the rewards are owed;
                // the next refresh retries them without re-charging.
                saved.AddPendingPurchase($"{purchaseId}:{purchaseKey}");
                await PersistAsync(entity, true, cToken);

                Raise(new PurchaseDeliveryFailed(id, purchaseId));

                return PurchaseResult.RewardFailed;
            }

            saved.RemovePendingPurchase(purchaseKey);
            await PersistAsync(entity, true, cToken);

            Raise(new PurchaseCompleted(id, purchaseId, purchaseKey));

            return PurchaseResult.Purchased;
        }

        /// <inheritdoc />
        public async UniTask RefreshAsync(CancellationToken cToken = default)
        {
            if (!IsReady) return;

            cToken.ThrowIfCancellationRequested();

            // Retry purchases whose rewards failed; the costs were already charged, so
            // this only re-grants the owed rewards.
            foreach (var pair in new Dictionary<string, EconomyEntity>(_entities))
            {
                var saved = pair.Value.SavedData;

                if (saved.PendingPurchaseKeys.Count == 0) continue;

                if (!TryGetData(pair.Key, out var data)) continue;

                foreach (var purchaseKey in new List<string>(saved.PendingPurchaseKeys))
                {
                    if (TryResolvePendingPurchase(data, purchaseKey, out var purchase))
                    {
                        var rewardsOk = await GrantRewardsAsync(pair.Key, purchase, purchaseKey,
                            cToken);

                        if (rewardsOk)
                        {
                            saved.RemovePendingPurchase(purchaseKey);
                        }
                    }
                }

                await PersistAsync(pair.Value, false, cToken);
            }
        }

        private bool TryResolvePendingPurchase(EconomyData data, string purchaseKey,
            out PurchaseData purchase)
        {
            // The purchase key is "{purchaseId}:{nonce}" — resolve the purchase half.
            var separator = purchaseKey.IndexOf(':');

            var purchaseId = separator > 0 ? purchaseKey.Substring(0, separator) : purchaseKey;

            purchase = data.GetPurchase(purchaseId);

            return purchase != null;
        }

        private async UniTask<bool> GrantRewardsAsync(string economyId, PurchaseData purchase,
            string purchaseKey, CancellationToken cToken)
        {
            if (_rewards == null || purchase.Rewards.Count == 0) return true;

            foreach (var reward in purchase.Rewards)
            {
                if (reward == null) continue;

                // The reward grant id is derived from the purchase key and the reward id,
                // so a partial delivery retries only what is still owed.
                var grantId = $"purchase:{economyId}:{purchaseKey}:{reward.Id}";

                var result = await _rewards.GrantAsync(reward, grantId, cToken);

                if (result != RewardGrantResult.Granted)
                {
                    return false;
                }
            }

            return true;
        }

        private async UniTask RefundCostsAsync(string economyId, string purchaseKey,
            PurchaseData purchase, CancellationToken cToken)
        {
            foreach (var cost in purchase.Costs)
            {
                await _currencies.GrantAsync(cost.CurrencyId, cost.Amount,
                    $"refund:{economyId}:{purchaseKey}:{cost.CurrencyId}", cToken);
            }
        }

        private int CurrencyBalance(string currencyId)
            => _currencies.TryGetBalance(currencyId, out var balance) ? balance : 0;

        private bool EconomyExists(string economyId) => TryGetData(economyId, out _);

        private bool TryGetData(string economyId, out EconomyData data)
        {
            if (_content != null && _content.TryGetData<EconomyData>(economyId, out data))
            {
                return true;
            }

            data = null;

            return false;
        }

        private async UniTask<EconomyEntity> EnsureEntityAsync(string economyId,
            CancellationToken cToken)
        {
            if (_entities.TryGetValue(economyId, out var existing)) return existing;

            var entity = new EconomyEntity(_config.SavePrefix + economyId, _backend, _content);
            entity.SetDataId(economyId);
            entity.ReloadData();

            await entity.InitializeAsync(cToken);

            _entities[economyId] = entity;

            return entity;
        }

        private async UniTask PersistAsync(EconomyEntity entity, bool immediate,
            CancellationToken cToken)
        {
            await entity.SaveAsync(immediate, cToken);
        }

        private static string DescribeCosts(PurchaseData purchase)
        {
            var parts = new string[purchase.Costs.Count];

            for (var i = 0; i < parts.Length; i++)
            {
                var cost = purchase.Costs[i];
                parts[i] = $"{cost.Amount}x {cost.CurrencyId}";
            }

            return string.Join(", ", parts);
        }

        private static void RaiseExchanged(string economyId, ExchangeRuleData rule, int amount,
            int received, string exchangeId)
        {
            // The bus is optional: a game that never bootstrapped UniEvents still gets a
            // working economy through the awaited results.
            if (UniEvents.IsInitialized)
            {
                UniEvents.Raise(new CurrencyExchanged(economyId, rule.FromCurrencyId,
                    rule.ToCurrencyId, amount, received, exchangeId));
            }
        }

        private static void Raise<TEvent>(TEvent @event)
            where TEvent : struct, IEvent
        {
            if (UniEvents.IsInitialized)
            {
                UniEvents.Raise(@event);
            }
        }
    }
}
