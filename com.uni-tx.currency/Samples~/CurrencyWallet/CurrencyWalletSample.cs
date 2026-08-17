using System;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Currency;
using UniTx.Entity;
using UniTx.IoC;
using UnityEngine;

namespace UniTx.Currency.Samples
{
    /// <summary>
    /// Two currencies built as entities from content, then granted and spent.
    /// </summary>
    /// <remarks>
    /// The sample is headless: attach it anywhere, watch the console. It loads content
    /// through the content service, lets the entity service build the currency entities,
    /// then drives the wallet with a grant, a spend and an idempotent re-grant.
    /// </remarks>
    public sealed class CurrencyWalletSample : MonoBehaviour
    {
        private ICurrencyService _wallet;

        private void Start() => LoadAsync().Forget();

        private async UniTaskVoid LoadAsync()
        {
            try
            {
                var cToken = this.GetCancellationTokenOnDestroy();

                // Content must be loaded before entities can be built from it.
                ContentRegistry.Register<CurrencyData>("currencies");
                await IoCStatics.Resolver.Resolve<IContentLoader>()
                    .LoadContentAsync(new[] { "content" }, cToken);

                var entities = IoCStatics.Resolver.Resolve<IEntityService>();
                await entities.LoadEntitiesAsync(cToken);

                _wallet = new CurrencyService(entities);
                await _wallet.InitializeAsync(cToken);

                // A fresh player starts with the content-defined starting balance.
                Debug.Log($"Fresh install — coins: {_wallet.GetBalance("coins")}, " +
                          $"gems: {_wallet.GetBalance("gems")}");

                await _wallet.GrantAsync("coins", 100, "welcome", cToken);

                // The same grant id again changes nothing — the ledger remembers it.
                await _wallet.GrantAsync("coins", 100, "welcome", cToken);

                Debug.Log($"After welcome grant: coins: {_wallet.GetBalance("coins")}");

                var spent = _wallet.TrySpend("coins", 30);

                Debug.Log($"Spent 30 coins ({(spent ? "charged" : "refused")}) — " +
                          $"coins: {_wallet.GetBalance("coins")}");
            }
            catch (OperationCanceledException)
            {
                // Expected when the sample is destroyed mid-load.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Grants 50 more coins from a context menu, so the flow is exercisable in the editor.
        /// </summary>
        [ContextMenu("Grant 50 Coins")]
        public void GrantCoins() =>
            _wallet.GrantAsync("coins", 50, $"menu-{Time.frameCount}").Forget();

        /// <summary>
        /// Spends 10 coins from a context menu.
        /// </summary>
        [ContextMenu("Spend 10 Coins")]
        public void SpendCoins() => Debug.Log($"Spent: {_wallet.TrySpend("coins", 10)}");
    }
}
