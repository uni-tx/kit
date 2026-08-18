using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Currency;
using UniTx.IoC;
using UnityEngine;

namespace UniTx.Economy.Samples
{
    /// <summary>
    /// The whole economy on a manual clock: grant a currency, exchange it into another at
    /// a content-defined rate, then buy a purchase that costs the premium currency and
    /// pays rewards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs headless — no UI, no real purchases. It walks the lifecycle exactly as a game
    /// would, logging every step, so it doubles as an integration check that the economy
    /// package is wired: content loaded, currency wallet bound, rewards flowing.
    /// </para>
    /// <para>
    /// Add this component to any object in a scene with the kit bootstrapped. The economy
    /// service is resolved through the <see cref="UniEconomy"/> facade, so it works
    /// regardless of how the game wires its container.
    /// </para>
    /// </remarks>
    public sealed class EconomyFlowSample : MonoBehaviour
    {
        [Header("Content")]
        [Tooltip("Which economy to exercise. Must match a registered EconomyData id.")]
        [SerializeField] private string _economyId = "core";

        [Header("Flow")]
        [Tooltip("Coins granted to the wallet at the start, simulating play rewards.")]
        [SerializeField] private int _startingCoins = 100;

        [Tooltip("Coins handed in per exchange. Must respect the rule's bounds.")]
        [SerializeField] private int _exchangeAmount = 10;

        [Tooltip("Cooldown between steps, so the log is readable.")]
        [SerializeField] private float _stepDelaySeconds = 1f;

        private CancellationTokenSource _cts;

        private void OnEnable() => Run().Forget();

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async UniTaskVoid Run()
        {
            try
            {
                _cts = new CancellationTokenSource();
                var cToken = _cts.Token;

                await UniTask.Delay(TimeSpan.FromSeconds(_stepDelaySeconds),
                    cancellationToken: cToken);

                if (!UniEconomy.IsReady)
                {
                    Debug.LogError("[EconomyFlowSample] UniEconomy is not ready. Is the " +
                                   "EconomyStep in the bootstrap flow, after content load?");
                    return;
                }

                if (!UniEconomy.SelectEconomy(_economyId))
                {
                    Debug.LogError($"[EconomyFlowSample] Economy '{_economyId}' not found.");
                    return;
                }

                Debug.Log($"[EconomyFlowSample] Economy '{_economyId}' selected. " +
                          $"{UniEconomy.EconomyIds.Count} economies registered.");

                var snapshot = UniEconomy.Snapshot;

                Debug.Log($"[EconomyFlowSample] Currencies: " +
                          $"{string.Join(", ", System.Array.ConvertAll(snapshot.Currencies, c => $"{c.CurrencyId} ({c.Balance})"))}.");

                foreach (var rule in snapshot.ExchangeRules)
                {
                    Debug.Log($"[EconomyFlowSample] Exchange '{rule.RuleId}': " +
                              $"{rule.FromCurrencyId} -> {rule.ToCurrencyId} at 1:{rule.Rate}.");
                }

                foreach (var purchase in snapshot.Purchases)
                {
                    Debug.Log($"[EconomyFlowSample] Purchase '{purchase.PurchaseId}' " +
                              $"({purchase.DisplayName}) costs {purchase.CostSummary}.");
                }

                // 1. Grant the starting coins, as play rewards would.
                var grantResult = await UniIapLikeGrantAsync(cToken);
                Debug.Log($"[EconomyFlowSample] Grant: {grantResult}.");

                // 2. Exchange coins into gems at the content-defined rate.
                var exchange = await UniEconomy.ExchangeAsync(_economyId, "coins_to_gems",
                    _exchangeAmount, $"sample-{DateTime.UtcNow.Ticks}", cToken);
                Debug.Log($"[EconomyFlowSample] Exchange: {exchange}.");

                // 3. Buy the first purchase with the gems just earned.
                var purchaseResult = await UniEconomy.PurchaseAsync(_economyId, "power_up",
                    $"sample-{DateTime.UtcNow.Ticks}", cToken);
                Debug.Log($"[EconomyFlowSample] Purchase 'power_up': {purchaseResult}.");

                var after = UniEconomy.Snapshot;

                Debug.Log($"[EconomyFlowSample] After: " +
                          $"{string.Join(", ", System.Array.ConvertAll(after.Currencies, c => $"{c.CurrencyId} ({c.Balance})"))}.");
                Debug.Log("[EconomyFlowSample] Done. The economy is working.");
            }
            catch (OperationCanceledException)
            {
                // Disabled mid-run; nothing to report.
            }
            catch (Exception exception)
            {
                Debug.LogError($"[EconomyFlowSample] {exception}");
            }
        }

        /// <summary>
        /// Grants the starting coins through the currency wallet — the same path quests,
        /// ladders and stores use, so the sample proves the wiring end to end.
        /// </summary>
        private async UniTask<CurrencyGrantResult> UniIapLikeGrantAsync(CancellationToken cToken)
        {
            var wallet = IoCStatics.Resolver.Resolve<ICurrencyService>();

            return await wallet.GrantAsync("coins", _startingCoins,
                $"sample-{DateTime.UtcNow.Ticks}", cToken);
        }
    }
}
