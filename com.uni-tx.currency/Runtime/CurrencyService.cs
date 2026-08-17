using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.Entity;
using UniTx.Events;
using UniTx.IoC;

namespace UniTx.Currency
{
    /// <summary>
    /// Reads and mutates player currency balances through the registered currency entities.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The service is a thin orchestration layer over <see cref="Currency"/> entities: the
    /// entity owns the balance and the mutation rules, the service locates it, persists it
    /// and raises <see cref="CurrencyChanged"/> on the kit bus. Currency entities are
    /// content-driven, so content must be loaded — and <c>EntityService.LoadEntitiesAsync</c>
    /// run — before balances can be read or written.
    /// </para>
    /// <para>
    /// Grants are idempotent by an optional grant id, so a replayed delivery — a retried
    /// purchase, a server resend — cannot mint currency twice.
    /// </para>
    /// </remarks>
    public sealed class CurrencyService : ICurrencyService
    {
        private IEntityService _entities;
        private bool _hasWarnedUnknownCurrency;

        /// <summary>
        /// Creates the service; dependencies arrive through <see cref="Inject"/>.
        /// </summary>
        public CurrencyService()
        {
        }

        /// <summary>
        /// Creates the service with an explicit entity service, for tests and manual wiring.
        /// </summary>
        /// <param name="entities">The entity service holding the currency entities.</param>
        public CurrencyService(IEntityService entities)
        {
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        }

        /// <inheritdoc />
        public bool IsReady { get; private set; }

        /// <inheritdoc />
        public void Inject(IResolver resolver) => _entities ??= resolver.Resolve<IEntityService>();

        /// <inheritdoc />
        public UniTask InitializeAsync(CancellationToken cToken = default)
        {
            cToken.ThrowIfCancellationRequested();

            // Currency entities are built by the entity service from content. Nothing to
            // load here — readiness just means the service is usable and the lookup is wired.
            IsReady = true;

            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public void Reset() => IsReady = false;

        /// <inheritdoc />
        public int GetBalance(string currencyId) => Get(currencyId).Balance;

        /// <inheritdoc />
        public bool TryGetBalance(string currencyId, out int balance)
        {
            if (TryGet(currencyId, out var currency))
            {
                balance = currency.Balance;
                return true;
            }

            balance = 0;
            return false;
        }

        /// <inheritdoc />
        public bool TrySpend(string currencyId, int amount)
        {
            if (amount <= 0) return false;

            if (!TryGet(currencyId, out var currency)) return false;

            var oldBalance = currency.Balance;

            if (!currency.TrySpend(amount)) return false;

            currency.Save();
            RaiseChanged(currencyId, oldBalance, currency.Balance, "spend");

            return true;
        }

        /// <inheritdoc />
        public async UniTask<CurrencyGrantResult> GrantAsync(string currencyId, int amount,
            string grantId = null, CancellationToken cToken = default)
        {
            if (amount <= 0) return CurrencyGrantResult.Rejected;

            if (!TryGet(currencyId, out var currency)) return CurrencyGrantResult.UnknownCurrency;

            cToken.ThrowIfCancellationRequested();

            if (currency.SavedData.HasAppliedGrant(grantId)) return CurrencyGrantResult.Duplicate;

            var oldBalance = currency.Balance;
            var granted = currency.Grant(amount);

            if (granted <= 0) return CurrencyGrantResult.Capped;

            currency.SavedData.RecordGrantId(grantId);

            await currency.SaveAsync(false, cToken);

            RaiseChanged(currencyId, oldBalance, currency.Balance, "grant");

            return granted < amount ? CurrencyGrantResult.Capped : CurrencyGrantResult.Granted;
        }

        private Currency Get(string currencyId)
        {
            if (TryGet(currencyId, out var currency)) return currency;

            throw new KeyNotFoundException(
                $"Currency '{currencyId}' is not registered. Load content and run " +
                "EntityService.LoadEntitiesAsync before reading balances.");
        }

        private bool TryGet(string currencyId, out Currency currency)
        {
            if (_entities != null && _entities.TryGet<Currency>(currencyId, out currency))
            {
                return true;
            }

            currency = null;

            if (!_hasWarnedUnknownCurrency)
            {
                _hasWarnedUnknownCurrency = true;
                UniStatics.LogWarning(
                    $"Currency '{currencyId}' is not registered. Load content and run " +
                    "EntityService.LoadEntitiesAsync before reading balances.", this);
            }

            return false;
        }

        private static void RaiseChanged(string currencyId, int oldBalance, int newBalance, string reason)
        {
            // The bus is optional: a game that never bootstrapped UniEvents still gets a
            // working wallet through the awaited results.
            if (UniEvents.IsInitialized)
            {
                UniEvents.Raise(new CurrencyChanged(currencyId, oldBalance, newBalance, reason));
            }
        }
    }
}
