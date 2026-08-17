using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniTx.Currency;
using UniTx.Entity;
using UniTx.Rewards;
using UnityEngine;
using UnityEngine.TestTools;

namespace UniTx.Rewards.Tests.EditMode
{
    /// <summary>
    /// A currency service that records what it was asked to grant.
    /// </summary>
    internal sealed class RecordingCurrencyService : ICurrencyService
    {
        public List<(string CurrencyId, int Amount, string GrantId)> Granted { get; } = new();

        public bool IsReady { get; private set; }

        public bool ShouldFail { get; set; }

        public void Inject(UniTx.IoC.IResolver resolver) { }

        public UniTask InitializeAsync(CancellationToken cToken = default)
        {
            IsReady = true;
            return UniTask.CompletedTask;
        }

        public void Reset() => IsReady = false;

        public int GetBalance(string currencyId) => 0;

        public bool TryGetBalance(string currencyId, out int balance)
        {
            balance = 0;
            return true;
        }

        public bool TrySpend(string currencyId, int amount) => true;

        public UniTask<CurrencyGrantResult> GrantAsync(string currencyId, int amount,
            string grantId = null, CancellationToken cToken = default)
        {
            if (ShouldFail) return UniTask.FromResult(CurrencyGrantResult.UnknownCurrency);

            Granted.Add((currencyId, amount, grantId));

            return UniTask.FromResult(CurrencyGrantResult.Granted);
        }
    }

    /// <summary>
    /// An entity service with a single registrable entity.
    /// </summary>
    internal sealed class FakeEntityService : IEntityService
    {
        private readonly Dictionary<string, IEntity> _entities = new();

        public void Register(IEntity entity) => _entities[entity.Id] = entity;

        public void Unregister(IEntity entity)
        {
            if (entity != null) _entities.Remove(entity.Id);
        }

        public UniTask LoadEntitiesAsync(CancellationToken cToken = default) =>
            UniTask.CompletedTask;

        public void UnloadEntities() => _entities.Clear();

        public TEntity Get<TEntity>(string id) where TEntity : IEntity
        {
            if (_entities.TryGetValue(id, out var entity) && entity is TEntity typed) return typed;

            throw new KeyNotFoundException(id);
        }

        public bool TryGet<TEntity>(string id, out TEntity entity) where TEntity : IEntity
        {
            if (id != null && _entities.TryGetValue(id, out var found) && found is TEntity typed)
            {
                entity = typed;
                return true;
            }

            entity = default;
            return false;
        }

        public IEnumerable<TEntity> GetAll<TEntity>() where TEntity : IEntity
        {
            foreach (var entity in _entities.Values)
            {
                if (entity is TEntity typed) yield return typed;
            }
        }
    }

    /// <summary>
    /// A minimal entity that consumes rewards, for the entity-backed delivery path.
    /// </summary>
    internal sealed class ConsumerEntity : IEntity, IRewardConsumer
    {
        public ConsumerEntity(string id) => Id = id;

        public string Id { get; }

        public string DataId => Id;

        public bool IsReady => true;

        public List<string> Consumed { get; } = new();

        public void Save() { }

        public UniTask SaveAsync(bool immediate = false, CancellationToken cToken = default) =>
            UniTask.CompletedTask;

        public void Inject(UniTx.IoC.IResolver resolver) { }

        public UniTask InitializeAsync(CancellationToken cToken = default) =>
            UniTask.CompletedTask;

        public void Reset() => Consumed.Clear();

        public UniTask<bool> ConsumeAsync(RewardData reward, string grantId = null,
            CancellationToken cToken = default)
        {
            Consumed.Add(reward.Id);
            return UniTask.FromResult(true);
        }
    }

    [TestFixture]
    public sealed class RewardServiceTests
    {
        private RecordingCurrencyService _currency;
        private FakeEntityService _entities;
        private RewardService _service;

        [SetUp]
        public void SetUp()
        {
            _currency = new RecordingCurrencyService();
            _entities = new FakeEntityService();
            _service = new RewardService(_currency, _entities);
            Run(_service.InitializeAsync(CancellationToken.None));
        }

        [Test]
        public void Grant_CurrencyReward_RoutesToTheCurrencyService()
        {
            var reward = new RewardData("coins-50", RewardKind.Currency, "coins", 50, null);

            Assert.That(RunFor(_service.GrantAsync(reward, "gr-1")),
                Is.EqualTo(RewardGrantResult.Granted));
            Assert.That(_currency.Granted, Has.Count.EqualTo(1));
            Assert.That(_currency.Granted[0], Is.EqualTo(("coins", 50, "gr-1")));
        }

        [Test]
        public void Grant_CurrencyReward_WhenTheCurrencyServiceRefuses_IsFailed()
        {
            _currency.ShouldFail = true;

            var reward = new RewardData("coins-50", RewardKind.Currency, "coins", 50, null);

            Assert.That(RunFor(_service.GrantAsync(reward)), Is.EqualTo(RewardGrantResult.Failed));
            Assert.That(_currency.Granted, Is.Empty);
        }

        [Test]
        public void Grant_EntityReward_RoutesToTheConsumerEntity()
        {
            var inventory = new ConsumerEntity("inventory");
            _entities.Register(inventory);

            var reward = new RewardData("sword", RewardKind.Item, "inventory", 1, null);

            Assert.That(RunFor(_service.GrantAsync(reward)), Is.EqualTo(RewardGrantResult.Granted));
            Assert.That(inventory.Consumed, Has.Count.EqualTo(1));
        }

        [Test]
        public void Grant_EntityReward_WithNoMatchingEntity_IsFailedAndStaysClaimable()
        {
            var reward = new RewardData("sword", RewardKind.Item, "missing", 1, null);

            Assert.That(RunFor(_service.GrantAsync(reward)), Is.EqualTo(RewardGrantResult.Failed));
        }

        [Test]
        public void Grant_InvalidReward_IsRejected()
        {
            Assert.That(RunFor(_service.GrantAsync(null)), Is.EqualTo(RewardGrantResult.Rejected));
            Assert.That(RunFor(_service.GrantAsync(new RewardData("bad", RewardKind.Item, "", 1, null))),
                Is.EqualTo(RewardGrantResult.Rejected));
        }

        [Test]
        public void Grant_KindWithNoBuiltInHandler_FallsBackToLoggingAndSucceeds()
        {
            // No entity service wired for this instance, so Item has no handler either.
            var service = new RewardService(_currency, null);
            Run(service.InitializeAsync(CancellationToken.None));

            var reward = new RewardData("custom", RewardKind.Custom, "anything", 1, null);

            Assert.That(RunFor(service.GrantAsync(reward)), Is.EqualTo(RewardGrantResult.Granted));
        }

        [Test]
        public void SetHandler_ReplacesTheBuiltInDefault()
        {
            var custom = new ConsumerEntity("inventory");
            _entities.Register(custom);
            _service.SetHandler(RewardKind.Item, new EntityRewardHandler(_entities));

            var reward = new RewardData("sword", RewardKind.Item, "inventory", 1, null);

            Assert.That(RunFor(_service.GrantAsync(reward)), Is.EqualTo(RewardGrantResult.Granted));
            Assert.That(custom.Consumed, Has.Count.EqualTo(1));
        }

        [Test]
        public void LoggingRewardHandler_ResetsItsWarnOnceFlagAtSubsystemRegistration()
        {
            // The shared instance is a mutable static: with Reload Domain off it survives
            // entering play mode, and without this reset the "no handler installed"
            // warning fires once in the editor's lifetime instead of once per session.
            var reset = typeof(LoggingRewardHandler).GetMethod(
                "ResetStatics", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(reset, Is.Not.Null,
                "LoggingRewardHandler must clear its warn-once flag on a domain-reload-off run.");
            Assert.That(
                reset.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false)
                    .Cast<RuntimeInitializeOnLoadMethodAttribute>()
                    .Any(attribute => attribute.loadType == RuntimeInitializeLoadType.SubsystemRegistration),
                Is.True, "The reset must run at SubsystemRegistration.");

            reset.Invoke(null, null);

            // Having been reset, the handler warns again on the next undelivered kind.
            LogAssert.Expect(LogType.Warning, new Regex("No IRewardHandler is installed"));
            RunFor(LoggingRewardHandler.Instance.GrantAsync(
                new RewardData("custom", RewardKind.Custom, "anything", 1, null)));

            // Leave the shared instance clean for whatever runs next.
            reset.Invoke(null, null);
        }

        private static void Run(UniTask task) => task.GetAwaiter().GetResult();

        private static T RunFor<T>(UniTask<T> task) => task.GetAwaiter().GetResult();
    }
}
