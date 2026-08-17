using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniTx.Currency;
using UniTx.IoC;

namespace UniTx.SeasonPass.Tests
{
    /// <summary>
    /// A currency service that answers only for the currencies it was given, and behaves
    /// like the real one for the rest: <c>GetBalance</c> throws, <c>TryGetBalance</c> does not.
    /// </summary>
    internal sealed class SparseCurrencyService : ICurrencyService
    {
        private readonly Dictionary<string, int> _balances = new();

        public bool IsReady { get; private set; }

        public void Add(string currencyId, int balance) => _balances[currencyId] = balance;

        public void Inject(IResolver resolver) { }

        public UniTask InitializeAsync(CancellationToken cToken = default)
        {
            IsReady = true;
            return UniTask.CompletedTask;
        }

        public void Reset() => IsReady = false;

        public int GetBalance(string currencyId) =>
            _balances.TryGetValue(currencyId, out var balance)
                ? balance
                : throw new KeyNotFoundException(currencyId);

        public bool TryGetBalance(string currencyId, out int balance) =>
            _balances.TryGetValue(currencyId, out balance);

        public bool TrySpend(string currencyId, int amount)
        {
            if (!_balances.TryGetValue(currencyId, out var balance) || balance < amount) return false;

            _balances[currencyId] = balance - amount;

            return true;
        }

        public UniTask<CurrencyGrantResult> GrantAsync(string currencyId, int amount,
            string grantId = null, CancellationToken cToken = default)
        {
            if (!_balances.ContainsKey(currencyId))
            {
                return UniTask.FromResult(CurrencyGrantResult.UnknownCurrency);
            }

            _balances[currencyId] += amount;

            return UniTask.FromResult(CurrencyGrantResult.Granted);
        }
    }

    [TestFixture]
    public sealed class SeasonPassCurrencyWalletTests
    {
        private SparseCurrencyService _currency;
        private SeasonPassCurrencyWallet _wallet;

        [SetUp]
        public void SetUp()
        {
            _currency = new SparseCurrencyService();
            _wallet = new SeasonPassCurrencyWallet(_currency);
        }

        [Test]
        public void GetBalance_AKnownCurrency_ReadsThrough()
        {
            _currency.Add("gems", 120);

            Assert.That(_wallet.GetBalance("gems"), Is.EqualTo(120));
        }

        [Test]
        public void GetBalance_AnUnregisteredCurrency_IsZeroRatherThanAThrow()
        {
            // UI reads a tier-skip price before content has loaded; the wallet this
            // replaced answered zero, and throwing there takes the screen down.
            Assert.That(_wallet.GetBalance("gems"), Is.Zero);
        }

        [Test]
        public void TrySpend_AnUnregisteredCurrency_ChargesNothing()
        {
            Assert.That(_wallet.TrySpend("gems", 10), Is.False);
        }

        [Test]
        public void TrySpend_WithEnoughBalance_Deducts()
        {
            _currency.Add("gems", 50);

            Assert.That(_wallet.TrySpend("gems", 20), Is.True);
            Assert.That(_wallet.GetBalance("gems"), Is.EqualTo(30));
        }
    }
}
