using UniTx.Core;

namespace UniTx.SeasonPass
{
    /// <summary>
    /// Spends in-game currency on behalf of the season pass.
    /// </summary>
    /// <remarks>
    /// Games already own a currency system, so the pass borrows it rather than shipping a
    /// second one that would need reconciling. Only the paid track and tier skips route
    /// through here; real-money purchases never do.
    /// </remarks>
    public interface ISeasonPassWallet
    {
        /// <summary>
        /// Returns the player's balance of a currency.
        /// </summary>
        /// <param name="currencyId">The currency to read.</param>
        int GetBalance(string currencyId);

        /// <summary>
        /// Deducts a cost if the player can afford it.
        /// </summary>
        /// <param name="currencyId">The currency to charge.</param>
        /// <param name="amount">How much to deduct.</param>
        /// <returns><c>true</c> when the charge went through.</returns>
        /// <remarks>
        /// Must be atomic: returning <c>true</c> without deducting hands out a free pass, and
        /// deducting while returning <c>false</c> charges for nothing.
        /// </remarks>
        bool TrySpend(string currencyId, int amount);
    }

    /// <summary>
    /// A wallet that owns no currency and refuses every charge.
    /// </summary>
    /// <remarks>
    /// The default. Currency purchases fail cleanly with
    /// <see cref="TrackUnlockResult.InsufficientFunds"/> until a real wallet is registered,
    /// rather than silently granting a paid track for free.
    /// </remarks>
    public sealed class NoOpSeasonPassWallet : ISeasonPassWallet
    {
        private bool _hasWarned;

        /// <inheritdoc />
        public int GetBalance(string currencyId) => 0;

        /// <inheritdoc />
        public bool TrySpend(string currencyId, int amount)
        {
            if (!_hasWarned)
            {
                _hasWarned = true;
                UniStatics.LogWarning(
                    $"No ISeasonPassWallet is registered, so the charge of {amount} " +
                    $"'{currencyId}' was refused. Register one to sell the pass for currency.", this);
            }

            return false;
        }
    }
}
