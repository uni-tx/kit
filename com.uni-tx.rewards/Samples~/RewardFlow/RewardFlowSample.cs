using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Content;
using UniTx.Currency;
using UniTx.Entity;
using UniTx.IoC;
using UniTx.Rewards;
using UnityEngine;

namespace UniTx.Rewards.Samples
{
    /// <summary>
    /// Currency and entity rewards delivered through the reward service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Headless: attach it anywhere, watch the console. A currency reward lands in the
    /// entity-based currency system; an item reward lands on the registered
    /// <see cref="IRewardConsumer"/> entity whose id matches the reward's item id.
    /// </para>
    /// <para>
    /// The demo inventory is a plain entity that consumes item rewards by printing what
    /// it received. A real game's inventory would persist what it collects.
    /// </para>
    /// </remarks>
    public sealed class RewardFlowSample : MonoBehaviour
    {
        private IRewardService _rewards;
        private ICurrencyService _currency;

        private void Start() => LoadAsync().Forget();

        private async UniTaskVoid LoadAsync()
        {
            try
            {
                var cToken = this.GetCancellationTokenOnDestroy();

                ContentRegistry.Register<RewardData>("rewards");
                await IoCStatics.Resolver.Resolve<IContentLoader>()
                    .LoadContentAsync(new[] { "content" }, cToken);

                var entities = IoCStatics.Resolver.Resolve<IEntityService>();
                await entities.LoadEntitiesAsync(cToken);

                var inventory = new DemoInventory("inventory");
                entities.Register(inventory);

                _currency = new CurrencyService(entities);
                await _currency.InitializeAsync(cToken);

                _rewards = new RewardService(_currency, entities);
                await _rewards.InitializeAsync(cToken);

                var coins = IoCStatics.Resolver.Resolve<IContentService>()
                    .GetData<RewardData>("coins_50");
                var sword = IoCStatics.Resolver.Resolve<IContentService>()
                    .GetData<RewardData>("sword");

                await _rewards.GrantAsync(coins, "chest:1", cToken);
                await _rewards.GrantAsync(sword, "chest:1", cToken);
                await _rewards.GrantAsync(coins, "chest:1", cToken); // duplicate id, ignored

                Debug.Log($"coins: {_currency.GetBalance("coins")}, " +
                          $"inventory: {string.Join(", ", inventory.Items)}");
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
        /// A reward consumer that records what it was handed.
        /// </summary>
        private sealed class DemoInventory : IEntity, IRewardConsumer
        {
            public DemoInventory(string id) => Id = id;

            public string Id { get; }

            public string DataId => Id;

            public bool IsReady => true;

            public System.Collections.Generic.List<string> Items { get; } = new();

            public void Save() { }

            public UniTask SaveAsync(bool immediate = false, CancellationToken cToken = default) =>
                UniTask.CompletedTask;

            public void Inject(IResolver resolver) { }

            public UniTask InitializeAsync(CancellationToken cToken = default) =>
                UniTask.CompletedTask;

            public void Reset() => Items.Clear();

            public UniTask<bool> ConsumeAsync(RewardData reward, string grantId = null,
                CancellationToken cToken = default)
            {
                Items.Add($"{reward.Amount}x {reward.ItemId}");

                return UniTask.FromResult(true);
            }
        }
    }
}
