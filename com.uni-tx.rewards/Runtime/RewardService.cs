using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.Currency;
using UniTx.Entity;
using UniTx.Events;
using UniTx.IoC;

namespace UniTx.Rewards
{
    /// <summary>
    /// Delivers rewards into the game's economy, routing by <see cref="RewardData.Kind"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The service decides <i>which</i> handler a reward goes to, never <i>what</i> a
    /// reward means — that belongs to the handler. Currency rewards land in the
    /// entity-based currency system, item/cosmetic/booster rewards land on a registered
    /// <see cref="IRewardConsumer"/> entity, and a game can install its own handler per
    /// kind for anything else. A kind with no handler logs and succeeds, so the flow is
    /// playable before an economy exists.
    /// </para>
    /// <para>
    /// The return value is load-bearing: a caller records the reward as delivered only
    /// after <see cref="GrantAsync"/> reports <see cref="RewardGrantResult.Granted"/>.
    /// </para>
    /// </remarks>
    public sealed class RewardService : IRewardService
    {
        private readonly Dictionary<RewardKind, IRewardHandler> _handlers = new();

        private ICurrencyService _currency;
        private IEntityService _entities;
        private bool _isReady;

        /// <summary>
        /// Creates the service; dependencies arrive through <see cref="Inject"/>.
        /// </summary>
        public RewardService()
        {
        }

        /// <summary>
        /// Creates the service with explicit dependencies, for tests and manual wiring.
        /// </summary>
        /// <param name="currency">The currency service, or null when there is none.</param>
        /// <param name="entities">The entity service, or null when there is none.</param>
        public RewardService(ICurrencyService currency = null, IEntityService entities = null)
        {
            _currency = currency;
            _entities = entities;
        }

        /// <inheritdoc />
        public void Inject(IResolver resolver)
        {
            if (_currency == null) resolver.TryResolve(out _currency);
            if (_entities == null) resolver.TryResolve(out _entities);
        }

        /// <inheritdoc />
        public UniTask InitializeAsync(CancellationToken cToken = default)
        {
            cToken.ThrowIfCancellationRequested();

            // Built-in defaults, only where nothing was installed by hand. Games that own a
            // handler call SetHandler, which takes precedence over these.
            if (_currency != null) _handlers.TryAdd(RewardKind.Currency, new CurrencyRewardHandler(_currency));

            if (_entities != null)
            {
                var entityHandler = new EntityRewardHandler(_entities);

                foreach (var kind in new[]
                         {
                             RewardKind.Item, RewardKind.Cosmetic, RewardKind.Booster,
                             RewardKind.Custom,
                         })
                {
                    _handlers.TryAdd(kind, entityHandler);
                }
            }

            _isReady = true;

            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public void Reset()
        {
            _isReady = false;
            _handlers.Clear();
        }

        /// <inheritdoc />
        public void SetHandler(RewardKind kind, IRewardHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _handlers[kind] = handler;
        }

        /// <inheritdoc />
        public async UniTask<RewardGrantResult> GrantAsync(RewardData reward, string grantId = null,
            CancellationToken cToken = default)
        {
            if (reward == null || !reward.IsValid) return RewardGrantResult.Rejected;

            cToken.ThrowIfCancellationRequested();

            if (!_handlers.TryGetValue(reward.Kind, out var handler))
            {
                handler = LoggingRewardHandler.Instance;
            }

            var delivered = await handler.GrantAsync(reward, grantId, cToken);

            if (!delivered) return RewardGrantResult.Failed;

            RaiseGranted(reward, grantId);

            return RewardGrantResult.Granted;
        }

        private static void RaiseGranted(RewardData reward, string grantId)
        {
            // The bus is optional: a game that never bootstrapped UniEvents still gets the
            // awaited result. The event carries the reward's requested amount — handlers
            // report delivery as a bool, so a capped currency grant reports the full
            // amount; see the remarks on RewardGranted.Amount.
            if (UniEvents.IsInitialized)
            {
                UniEvents.Raise(new RewardGranted(reward.Id, reward.Kind, reward.ItemId,
                    reward.Amount, grantId));
            }
        }
    }
}
