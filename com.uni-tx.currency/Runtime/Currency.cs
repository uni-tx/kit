using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Entity;
using UniTx.IoC;

namespace UniTx.Currency
{
    /// <summary>
    /// One currency as an entity: static <see cref="CurrencyData"/> joined with a per-player
    /// <see cref="CurrencySavedData"/> balance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The balance-mutation rules live here, next to the data they constrain: grants respect
    /// the content-defined cap, spends never go below zero, and a first-ever load seeds the
    /// configured starting balance. The <see cref="CurrencyService"/> orchestrates, persists
    /// and raises events; it never touches the numbers directly.
    /// </para>
    /// </remarks>
    public sealed class Currency : EntityBase<CurrencyData, CurrencySavedData>
    {
        /// <summary>
        /// Creates a currency bound to a content id (also its save key).
        /// </summary>
        /// <param name="id">The currency id.</param>
        public Currency(string id) : base(id)
        {
        }

        /// <summary>
        /// Gets the player's balance, or zero before initialization.
        /// </summary>
        public int Balance => SavedData?.Balance ?? 0;

        /// <summary>
        /// Indicates whether this is soft (earned through play) currency.
        /// </summary>
        public bool IsSoftCurrency => Data != null && Data.IsSoftCurrency;

        /// <inheritdoc />
        protected override void OnInject(IResolver resolver)
        {
        }

        /// <inheritdoc />
        protected override async UniTask OnInitAsync(CancellationToken cToken)
        {
            // Custom backends may not migrate; the built-in ones do, so this is a no-op
            // there. Migrate is idempotent, so double-running it costs nothing.
            SavedData.Migrate();

            // A save that has never been written is a fresh player; seed the configured
            // starting balance once, and persist it so the seed itself is not replayed.
            if (SavedData.ModifiedTimestamp == 0 && Data != null && Data.InitialBalance > 0)
            {
                SavedData.SetBalance(Data.InitialBalance);
                await SaveAsync(false, cToken);
            }
        }

        /// <inheritdoc />
        protected override void OnReset()
        {
        }

        /// <summary>
        /// Adds to the balance, stopping at the content-defined maximum.
        /// </summary>
        /// <param name="amount">How much to add. Non-positive values are ignored.</param>
        /// <returns>How much was actually granted, possibly trimmed by the cap.</returns>
        /// <remarks>
        /// Grants nothing before the entity is initialized: the balance lives in the saved
        /// data, so there is nothing to add to yet.
        /// </remarks>
        public int Grant(int amount)
        {
            if (amount <= 0 || SavedData == null) return 0;

            var granted = amount;

            if (Data != null && Data.MaxBalance > 0)
            {
                granted = Math.Min(amount, Math.Max(0, Data.MaxBalance - Balance));

                if (granted <= 0) return 0;
            }

            SavedData.SetBalance(Balance + granted);

            return granted;
        }

        /// <summary>
        /// Deducts from the balance if the player can afford it.
        /// </summary>
        /// <param name="amount">How much to deduct. Non-positive values are ignored.</param>
        /// <returns><c>true</c> when the charge went through.</returns>
        /// <remarks>
        /// Atomic: returning <c>true</c> always means the balance was reduced, and a
        /// <c>false</c> means nothing changed. Before initialization there is no balance
        /// to charge, so the spend is refused rather than throwing.
        /// </remarks>
        public bool TrySpend(int amount)
        {
            if (amount <= 0 || SavedData == null || Balance < amount) return false;

            SavedData.SetBalance(Balance - amount);

            return true;
        }

        /// <summary>
        /// Raises the balance to at least the given value, never lowering it.
        /// </summary>
        /// <param name="balance">The candidate balance, usually from a backend.</param>
        public void RaiseTo(int balance)
        {
            if (SavedData != null && balance > Balance) SavedData.SetBalance(balance);
        }
    }
}
